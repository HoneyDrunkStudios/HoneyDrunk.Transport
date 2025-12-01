using HoneyDrunk.Kernel.Abstractions.Context;
using HoneyDrunk.Transport.Abstractions;

namespace HoneyDrunk.Transport.Pipeline;

/// <summary>
/// Execution context for middleware pipeline with richer metadata than MessageContext.
/// Provides middleware with access to adapter-specific broker properties and execution state.
/// </summary>
public sealed class TransportExecutionContext
{
    /// <summary>
    /// Gets or initializes the transport envelope being processed.
    /// </summary>
    public required ITransportEnvelope Envelope { get; init; }

    /// <summary>
    /// Gets or sets the Grid context extracted from the envelope.
    /// Populated by GridContextPropagationMiddleware.
    /// </summary>
    public IGridContext? GridContext { get; set; }

    /// <summary>
    /// Gets or initializes the transaction context for this message.
    /// </summary>
    public required ITransportTransaction Transaction { get; init; }

    /// <summary>
    /// Gets the adapter-specific broker properties (lock tokens, visibility timeouts, etc.).
    /// Middleware can use this to access or modify transport-specific metadata.
    /// </summary>
    public Dictionary<string, object> BrokerProperties { get; init; } = [];

    /// <summary>
    /// Gets or sets the current retry attempt (0 = first attempt, 1 = first retry, etc.).
    /// Incremented by RetryMiddleware on each retry.
    /// </summary>
    public int RetryAttempt { get; set; }

    /// <summary>
    /// Gets or sets the total processing duration so far.
    /// Updated by middleware to track cumulative processing time across retries.
    /// </summary>
    public TimeSpan ProcessingDuration { get; set; }

    /// <summary>
    /// Gets the delivery count (how many times the message has been delivered).
    /// This is typically RetryAttempt + 1.
    /// </summary>
    public int DeliveryCount => RetryAttempt + 1;

    /// <summary>
    /// Converts this execution context to a MessageContext for handler invocation.
    /// Handlers see a simplified view without middleware-specific details.
    /// </summary>
    /// <returns>A MessageContext instance suitable for handler consumption.</returns>
    public MessageContext ToMessageContext() => new()
    {
        Envelope = Envelope,
        GridContext = GridContext,
        Transaction = Transaction,
        Properties = new Dictionary<string, object>(BrokerProperties),
        DeliveryCount = DeliveryCount
    };
}
