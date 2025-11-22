using HoneyDrunk.Transport.Abstractions;

namespace HoneyDrunk.Transport.Primitives;

/// <summary>
/// Default implementation of a transport envelope aligned with Kernel v0.2 IGridContext.
/// </summary>
public sealed class TransportEnvelope : ITransportEnvelope
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TransportEnvelope"/> class.
    /// </summary>
    public TransportEnvelope()
    {
        Timestamp = DateTimeOffset.UtcNow;
    }

    /// <inheritdoc/>
    public string MessageId { get; init; } = string.Empty;

    /// <inheritdoc/>
    public string? CorrelationId { get; init; }

    /// <inheritdoc/>
    public string? CausationId { get; init; }

    /// <inheritdoc/>
    public string? NodeId { get; init; }

    /// <inheritdoc/>
    public string? StudioId { get; init; }

    /// <inheritdoc/>
    public string? Environment { get; init; }

    /// <inheritdoc/>
    public DateTimeOffset Timestamp { get; init; }

    /// <inheritdoc/>
    public string MessageType { get; init; } = string.Empty;

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();

    /// <inheritdoc/>
    public ReadOnlyMemory<byte> Payload { get; init; }

    /// <inheritdoc/>
    public ITransportEnvelope WithHeaders(IDictionary<string, string> additionalHeaders)
    {
        var combined = new Dictionary<string, string>(Headers);
        foreach (var kvp in additionalHeaders)
        {
            combined[kvp.Key] = kvp.Value;
        }

        return new TransportEnvelope
        {
            MessageId = MessageId,
            CorrelationId = CorrelationId,
            CausationId = CausationId,
            NodeId = NodeId,
            StudioId = StudioId,
            Environment = Environment,
            Timestamp = Timestamp,
            MessageType = MessageType,
            Headers = combined,
            Payload = Payload
        };
    }

    /// <inheritdoc/>
    public ITransportEnvelope WithGridContext(
        string? correlationId = null,
        string? causationId = null,
        string? nodeId = null,
        string? studioId = null,
        string? environment = null)
    {
        return new TransportEnvelope
        {
            MessageId = MessageId,
            CorrelationId = correlationId ?? CorrelationId,
            CausationId = causationId ?? CausationId,
            NodeId = nodeId ?? NodeId,
            StudioId = studioId ?? StudioId,
            Environment = environment ?? Environment,
            Timestamp = Timestamp,
            MessageType = MessageType,
            Headers = Headers,
            Payload = Payload
        };
    }
}
