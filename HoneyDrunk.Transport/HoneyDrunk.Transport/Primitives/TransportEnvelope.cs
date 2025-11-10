using HoneyDrunk.Transport.Abstractions;

namespace HoneyDrunk.Transport.Primitives;

/// <summary>
/// Default implementation of a transport envelope.
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
            Timestamp = Timestamp,
            MessageType = MessageType,
            Headers = combined,
            Payload = Payload
        };
    }

    /// <inheritdoc/>
    public ITransportEnvelope WithCorrelation(string? correlationId, string? causationId)
    {
        return new TransportEnvelope
        {
            MessageId = MessageId,
            CorrelationId = correlationId ?? CorrelationId,
            CausationId = causationId ?? CausationId,
            Timestamp = Timestamp,
            MessageType = MessageType,
            Headers = Headers,
            Payload = Payload
        };
    }
}
