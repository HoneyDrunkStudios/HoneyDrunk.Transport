using HoneyDrunk.Transport.Abstractions;

using TransportGridContextFactory = HoneyDrunk.Transport.Context.IGridContextFactory;

namespace HoneyDrunk.Transport.Pipeline.Middleware;

/// <summary>
/// Middleware that propagates Grid context across Node boundaries.
/// Creates an initialized abstractions-only Grid context from envelope metadata and populates MessageContext for handlers.
/// This is the canonical bridge between Kernel Grid context and Transport messaging.
/// </summary>
/// <remarks>
/// <para>
/// Transport owns message-scope propagation and uses Kernel Abstractions only. It does not reference
/// Kernel runtime implementation types or require a DI-scoped runtime GridContext.
/// </para>
/// <para>
/// Initializes a new instance of the <see cref="GridContextPropagationMiddleware"/> class.
/// </para>
/// </remarks>
/// <param name="gridContextFactory">The Grid context factory for creating context from envelope.</param>
public sealed class GridContextPropagationMiddleware(TransportGridContextFactory gridContextFactory) : IMessageMiddleware
{
    private readonly TransportGridContextFactory _gridContextFactory = gridContextFactory
        ?? throw new ArgumentNullException(nameof(gridContextFactory));

    /// <inheritdoc/>
    public Task InvokeAsync(
        ITransportEnvelope envelope,
        MessageContext context,
        Func<Task> next,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var gridContext = _gridContextFactory.CreateFromEnvelope(envelope, cancellationToken);

        context.GridContext = gridContext;
        context.Properties["GridContext"] = gridContext;
        context.Properties["CorrelationId"] = gridContext.CorrelationId ?? string.Empty;
        context.Properties["CausationId"] = gridContext.CausationId ?? string.Empty;
        context.Properties["NodeId"] = gridContext.NodeId;
        context.Properties["StudioId"] = gridContext.StudioId;
        context.Properties["Environment"] = gridContext.Environment;

        return next();
    }
}
