using HoneyDrunk.Kernel.Abstractions.Context;

namespace HoneyDrunk.Transport.Abstractions;

/// <summary>
/// Context information available during message handling.
/// Includes Grid context for distributed context propagation.
/// </summary>
public sealed class MessageContext
{
    /// <summary>
    /// Gets or initializes the envelope containing metadata about the message.
    /// </summary>
    public required ITransportEnvelope Envelope { get; init; }

    /// <summary>
    /// Gets or sets the Grid context for distributed tracing and correlation.
    /// Populated by GridContextPropagationMiddleware from envelope metadata.
    /// </summary>
    public IGridContext? GridContext { get; set; }

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
