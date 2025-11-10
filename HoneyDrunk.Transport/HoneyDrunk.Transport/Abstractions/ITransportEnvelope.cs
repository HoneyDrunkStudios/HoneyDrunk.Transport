namespace HoneyDrunk.Transport.Abstractions;

/// <summary>
/// Represents a transport-agnostic message envelope that wraps all messages moving through the system.
/// </summary>
public interface ITransportEnvelope
{
    /// <summary>
    /// Gets the unique identifier for this message instance.
    /// </summary>
    string MessageId { get; }

    /// <summary>
    /// Gets the identifier that groups related messages together (e.g., all messages in a workflow).
    /// </summary>
    string? CorrelationId { get; }

    /// <summary>
    /// Gets the identifier of the message that caused this message to be created.
    /// </summary>
    string? CausationId { get; }

    /// <summary>
    /// Gets the UTC timestamp when the message was created.
    /// </summary>
    DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Gets the logical type or name of the message content.
    /// </summary>
    string MessageType { get; }

    /// <summary>
    /// Gets the custom headers for extensibility (routing hints, security tokens, etc.).
    /// </summary>
    IReadOnlyDictionary<string, string> Headers { get; }

    /// <summary>
    /// Gets the serialized message body.
    /// </summary>
    ReadOnlyMemory<byte> Payload { get; }

    /// <summary>
    /// Creates a new envelope with updated headers.
    /// </summary>
    /// <param name="additionalHeaders">Headers to add or update.</param>
    /// <returns>A new envelope instance with the updated headers.</returns>
    ITransportEnvelope WithHeaders(IDictionary<string, string> additionalHeaders);

    /// <summary>
    /// Creates a new envelope with updated correlation context.
    /// </summary>
    /// <param name="correlationId">The correlation identifier.</param>
    /// <param name="causationId">The causation identifier.</param>
    /// <returns>A new envelope instance with the updated correlation context.</returns>
    ITransportEnvelope WithCorrelation(string? correlationId, string? causationId);
}
