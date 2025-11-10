namespace HoneyDrunk.Transport.Abstractions;

/// <summary>
/// Context information available during message handling.
/// </summary>
public sealed class MessageContext
{
    /// <summary>
    /// Gets or initializes the envelope containing metadata about the message.
    /// </summary>
    public required ITransportEnvelope Envelope { get; init; }

    /// <summary>
    /// Gets or initializes the transaction context for this message.
    /// </summary>
    public required ITransportTransaction Transaction { get; init; }

    /// <summary>
    /// Gets or initializes the number of delivery attempts for this message.
    /// </summary>
    public int DeliveryCount { get; init; }

    /// <summary>
    /// Gets or initializes the additional context properties set by middleware.
    /// </summary>
    public Dictionary<string, object> Properties { get; init; } = [];
}
