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
    /// <remarks>
    /// <b>Kernel vNext (v0.4.0+):</b> This property holds a reference to the DI-scoped
    /// <see cref="IGridContext"/> owned by Kernel. Middleware initializes the existing
    /// context rather than creating a new one.
    /// </remarks>
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
    /// Gets or initializes the service provider for resolving scoped services.
    /// </summary>
    /// <remarks>
    /// <b>Kernel vNext (v0.4.0+):</b> This is the scoped <see cref="IServiceProvider"/>
    /// for the current message processing operation. Middleware uses this to resolve
    /// the DI-owned <see cref="IGridContext"/> for initialization.
    /// </remarks>
    public IServiceProvider? ServiceProvider { get; init; }

    /// <summary>
    /// Gets or initializes the additional context properties set by middleware.
    /// </summary>
    public Dictionary<string, object> Properties { get; init; } = [];
}
