using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.Context;

namespace HoneyDrunk.Transport.Pipeline.Middleware;

/// <summary>
/// Middleware that propagates Grid context across Node boundaries.
/// Extracts IGridContext from envelope metadata and populates MessageContext for handlers.
/// This is the canonical bridge between Kernel Grid context and Transport messaging.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="GridContextPropagationMiddleware"/> class.
/// </remarks>
/// <param name="gridContextFactory">The Grid context factory.</param>
public sealed class GridContextPropagationMiddleware(IGridContextFactory gridContextFactory) : IMessageMiddleware
{
    private readonly IGridContextFactory _gridContextFactory = gridContextFactory;

    /// <inheritdoc/>
    public Task InvokeAsync(
        ITransportEnvelope envelope,
        MessageContext context,
        Func<Task> next,
        CancellationToken cancellationToken = default)
    {
        // Check for cancellation before processing
        cancellationToken.ThrowIfCancellationRequested();

        // Create Grid context from envelope - this propagates correlation, causation,
        // node ID, studio ID, environment, and baggage across Node boundaries
        var gridContext = _gridContextFactory.CreateFromEnvelope(envelope, cancellationToken);

        // Populate MessageContext.GridContext as first-class property
        context.GridContext = gridContext;

        // Also store in Properties for backward compatibility and convenience access
        context.Properties["GridContext"] = gridContext;
        context.Properties["CorrelationId"] = gridContext.CorrelationId ?? string.Empty;
        context.Properties["CausationId"] = gridContext.CausationId ?? string.Empty;
        context.Properties["NodeId"] = gridContext.NodeId ?? string.Empty;
        context.Properties["StudioId"] = gridContext.StudioId ?? string.Empty;
        context.Properties["Environment"] = gridContext.Environment ?? string.Empty;

        return next();
    }
}
