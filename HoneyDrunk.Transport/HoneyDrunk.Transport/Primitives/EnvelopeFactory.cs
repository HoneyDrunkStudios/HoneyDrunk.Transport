using HoneyDrunk.Kernel.Abstractions.Context;
using HoneyDrunk.Kernel.Abstractions.Identity;
using HoneyDrunk.Transport.Abstractions;

namespace HoneyDrunk.Transport.Primitives;

/// <summary>
/// Factory for creating transport envelopes from typed messages.
/// Uses Kernel primitives for deterministic ID generation, timestamps, and Grid context propagation.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="EnvelopeFactory"/> class.
/// </remarks>
/// <param name="timeProvider">The time provider for timestamps.</param>
public sealed class EnvelopeFactory(TimeProvider timeProvider)
{
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
    public ITransportEnvelope CreateEnvelope<TMessage>(
        ReadOnlyMemory<byte> payload,
        string? correlationId = null,
        string? causationId = null,
        IDictionary<string, string>? headers = null)
        where TMessage : class
    {
        return new TransportEnvelope
        {
            MessageId = CorrelationId.NewId().ToString(),
            CorrelationId = correlationId,
            CausationId = causationId,
            Timestamp = _timeProvider.GetUtcNow(),
            MessageType = typeof(TMessage).FullName ?? typeof(TMessage).Name,
            Headers = headers != null
                ? new Dictionary<string, string>(headers)
                : [],
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
    public ITransportEnvelope CreateEnvelopeWithGridContext<TMessage>(
        ReadOnlyMemory<byte> payload,
        IGridContext gridContext,
        IDictionary<string, string>? headers = null)
        where TMessage : class
    {
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
    public ITransportEnvelope CreateEnvelopeWithId(
        string messageId,
        string messageType,
        ReadOnlyMemory<byte> payload,
        string? correlationId = null,
        string? causationId = null,
        IDictionary<string, string>? headers = null)
    {
        return new TransportEnvelope
        {
            MessageId = messageId,
            CorrelationId = correlationId,
            CausationId = causationId,
            Timestamp = _timeProvider.GetUtcNow(),
            MessageType = messageType,
            Headers = headers != null
                ? new Dictionary<string, string>(headers)
                : [],
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
    public ITransportEnvelope CreateReply(
        ITransportEnvelope original,
        string replyMessageType,
        ReadOnlyMemory<byte> payload,
        IDictionary<string, string>? additionalHeaders = null)
    {
        var headers = new Dictionary<string, string>(original.Headers);
        if (additionalHeaders != null)
        {
            foreach (var kvp in additionalHeaders)
            {
                headers[kvp.Key] = kvp.Value;
            }
        }

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
}
