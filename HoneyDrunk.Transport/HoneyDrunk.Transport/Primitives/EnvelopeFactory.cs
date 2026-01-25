using HoneyDrunk.Kernel.Abstractions.Context;
using HoneyDrunk.Kernel.Abstractions.Identity;
using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.Exceptions;

namespace HoneyDrunk.Transport.Primitives;

/// <summary>
/// Factory for creating transport envelopes from typed messages.
/// Uses Kernel primitives for deterministic ID generation, timestamps, and Grid context propagation.
/// </summary>
/// <remarks>
/// <para>
/// Initializes a new instance of the <see cref="EnvelopeFactory"/> class.
/// </para>
/// <para>
/// This factory validates outbound envelopes before they leave the process. Invalid envelopes
/// (oversized headers, missing required context) fail fast with clear exceptions rather than
/// failing at the broker level with cryptic errors.
/// </para>
/// </remarks>
/// <param name="timeProvider">The time provider for timestamps.</param>
public sealed class EnvelopeFactory(TimeProvider timeProvider)
{
    /// <summary>
    /// Maximum allowed size for headers/baggage aggregate in bytes.
    /// Azure Service Bus has a 64KB limit on application properties.
    /// We use a conservative 48KB limit to leave room for system properties.
    /// </summary>
    public const int MaxHeadersSizeBytes = 48 * 1024;

    private readonly TimeProvider _timeProvider = timeProvider;

    /// <summary>
    /// Creates an envelope from a typed message and serialized payload.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <param name="payload">The serialized payload.</param>
    /// <param name="correlationId">Optional correlation identifier.</param>
    /// <param name="causationId">Optional causation identifier.</param>
    /// <param name="headers">Optional message headers.</param>
    /// <returns>A new transport envelope.</returns>
    /// <exception cref="EnvelopeValidationException">Thrown if headers exceed size limits.</exception>
    public ITransportEnvelope CreateEnvelope<TMessage>(
        ReadOnlyMemory<byte> payload,
        string? correlationId = null,
        string? causationId = null,
        IDictionary<string, string>? headers = null)
        where TMessage : class
    {
        var finalHeaders = headers != null
            ? new Dictionary<string, string>(headers)
            : [];

        ValidateHeadersSize(finalHeaders);

        return new TransportEnvelope
        {
            MessageId = CorrelationId.NewId().ToString(),
            CorrelationId = correlationId,
            CausationId = causationId,
            Timestamp = _timeProvider.GetUtcNow(),
            MessageType = typeof(TMessage).FullName ?? typeof(TMessage).Name,
            Headers = finalHeaders,
            Payload = payload
        };
    }

    /// <summary>
    /// Creates an envelope from a typed message with Grid context.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <param name="payload">The serialized payload.</param>
    /// <param name="gridContext">The Grid context to propagate.</param>
    /// <param name="headers">Optional additional message headers.</param>
    /// <returns>A new transport envelope with Grid context.</returns>
    /// <exception cref="ArgumentNullException">Thrown if gridContext is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if gridContext is not initialized.</exception>
    /// <exception cref="EnvelopeValidationException">Thrown if required context fields are missing or headers exceed size limits.</exception>
    public ITransportEnvelope CreateEnvelopeWithGridContext<TMessage>(
        ReadOnlyMemory<byte> payload,
        IGridContext gridContext,
        IDictionary<string, string>? headers = null)
        where TMessage : class
    {
        ArgumentNullException.ThrowIfNull(gridContext);

        // FAIL FAST: Context must be initialized
        if (!gridContext.IsInitialized)
        {
            throw new InvalidOperationException(
                "GridContext is not initialized. Cannot create envelope from uninitialized context. " +
                "Ensure the context is properly initialized via Kernel before publishing messages.");
        }

        // FAIL FAST: CorrelationId is required for distributed tracing
        if (string.IsNullOrEmpty(gridContext.CorrelationId))
        {
            throw new EnvelopeValidationException(
                "CorrelationId is required but was null or empty. " +
                "GridContext must have a valid CorrelationId for message tracing.");
        }

        // Merge baggage into headers
        var allHeaders = new Dictionary<string, string>();
        if (gridContext.Baggage != null)
        {
            foreach (var kvp in gridContext.Baggage)
            {
                allHeaders[kvp.Key] = kvp.Value;
            }
        }

        if (headers != null)
        {
            foreach (var kvp in headers)
            {
                allHeaders[kvp.Key] = kvp.Value;
            }
        }

        // FAIL FAST: Validate headers size before creating envelope
        ValidateHeadersSize(allHeaders);

        return new TransportEnvelope
        {
            MessageId = CorrelationId.NewId().ToString(),
            CorrelationId = gridContext.CorrelationId,
            CausationId = gridContext.CausationId,
            NodeId = gridContext.NodeId,
            StudioId = gridContext.StudioId,
            TenantId = gridContext.TenantId,
            ProjectId = gridContext.ProjectId,
            Environment = gridContext.Environment,
            Timestamp = _timeProvider.GetUtcNow(),
            MessageType = typeof(TMessage).FullName ?? typeof(TMessage).Name,
            Headers = allHeaders,
            Payload = payload
        };
    }

    /// <summary>
    /// Creates an envelope with a specified message ID.
    /// </summary>
    /// <param name="messageId">The message identifier.</param>
    /// <param name="messageType">The message type name.</param>
    /// <param name="payload">The serialized payload.</param>
    /// <param name="correlationId">Optional correlation identifier.</param>
    /// <param name="causationId">Optional causation identifier.</param>
    /// <param name="headers">Optional message headers.</param>
    /// <returns>A new transport envelope.</returns>
    /// <exception cref="ArgumentException">Thrown if messageId or messageType is null or empty.</exception>
    /// <exception cref="EnvelopeValidationException">Thrown if headers exceed size limits.</exception>
    public ITransportEnvelope CreateEnvelopeWithId(
        string messageId,
        string messageType,
        ReadOnlyMemory<byte> payload,
        string? correlationId = null,
        string? causationId = null,
        IDictionary<string, string>? headers = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(messageId);
        ArgumentException.ThrowIfNullOrEmpty(messageType);

        var finalHeaders = headers != null
            ? new Dictionary<string, string>(headers)
            : [];

        ValidateHeadersSize(finalHeaders);

        return new TransportEnvelope
        {
            MessageId = messageId,
            CorrelationId = correlationId,
            CausationId = causationId,
            Timestamp = _timeProvider.GetUtcNow(),
            MessageType = messageType,
            Headers = finalHeaders,
            Payload = payload
        };
    }

    /// <summary>
    /// Creates a reply envelope derived from an original envelope.
    /// </summary>
    /// <param name="original">The original envelope to reply to.</param>
    /// <param name="replyMessageType">The reply message type name.</param>
    /// <param name="payload">The serialized reply payload.</param>
    /// <param name="additionalHeaders">Optional additional headers.</param>
    /// <returns>A new reply envelope.</returns>
    /// <exception cref="ArgumentNullException">Thrown if original is null.</exception>
    /// <exception cref="ArgumentException">Thrown if replyMessageType is null or empty.</exception>
    /// <exception cref="EnvelopeValidationException">Thrown if headers exceed size limits.</exception>
    public ITransportEnvelope CreateReply(
        ITransportEnvelope original,
        string replyMessageType,
        ReadOnlyMemory<byte> payload,
        IDictionary<string, string>? additionalHeaders = null)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentException.ThrowIfNullOrEmpty(replyMessageType);

        var headers = new Dictionary<string, string>(original.Headers);
        if (additionalHeaders != null)
        {
            foreach (var kvp in additionalHeaders)
            {
                headers[kvp.Key] = kvp.Value;
            }
        }

        ValidateHeadersSize(headers);

        return new TransportEnvelope
        {
            MessageId = CorrelationId.NewId().ToString(),
            CorrelationId = original.CorrelationId ?? original.MessageId,
            CausationId = original.MessageId,
            NodeId = original.NodeId,
            StudioId = original.StudioId,
            TenantId = original.TenantId,
            ProjectId = original.ProjectId,
            Environment = original.Environment,
            Timestamp = _timeProvider.GetUtcNow(),
            MessageType = replyMessageType,
            Headers = headers,
            Payload = payload
        };
    }

    /// <summary>
    /// Validates that headers do not exceed the maximum allowed size.
    /// </summary>
    /// <param name="headers">The headers to validate.</param>
    /// <exception cref="EnvelopeValidationException">Thrown if headers exceed size limits.</exception>
    private static void ValidateHeadersSize(Dictionary<string, string> headers)
    {
        if (headers.Count == 0)
        {
            return;
        }

        // Calculate approximate size: sum of (key length + value length) * 2 (UTF-16) + overhead
        var estimatedSize = 0;
        foreach (var kvp in headers)
        {
            estimatedSize += (kvp.Key?.Length ?? 0) * 2;
            estimatedSize += (kvp.Value?.Length ?? 0) * 2;
            estimatedSize += 16; // Per-property overhead estimate
        }

        if (estimatedSize > MaxHeadersSizeBytes)
        {
            throw new EnvelopeValidationException(
                $"Headers/baggage size ({estimatedSize:N0} bytes) exceeds maximum allowed " +
                $"({MaxHeadersSizeBytes:N0} bytes). Reduce baggage content or header count. " +
                "This validation prevents broker-level failures from oversized application properties.");
        }
    }
}
