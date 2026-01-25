using HoneyDrunk.Kernel.Abstractions.Context;
using HoneyDrunk.Transport.Abstractions;
using Microsoft.Extensions.DependencyInjection;

using TransportGridContextFactory = HoneyDrunk.Transport.Context.IGridContextFactory;

namespace HoneyDrunk.Transport.Pipeline.Middleware;

/// <summary>
/// Middleware that propagates Grid context across Node boundaries.
/// Initializes the DI-scoped IGridContext from envelope metadata and populates MessageContext for handlers.
/// This is the canonical bridge between Kernel Grid context and Transport messaging.
/// </summary>
/// <remarks>
/// <para>
/// <b>Kernel vNext (v0.4.0+):</b> This middleware honors the one-GridContext-per-scope invariant.
/// Instead of creating a Transport-owned context, it resolves the existing DI-scoped
/// <see cref="IGridContext"/> from Kernel and initializes it with envelope metadata.
/// </para>
/// <para>
/// Initializes a new instance of the <see cref="GridContextPropagationMiddleware"/> class.
/// </para>
/// </remarks>
/// <param name="gridContextFactory">The Grid context factory for initializing context from envelope.</param>
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
        // Check for cancellation before processing
        cancellationToken.ThrowIfCancellationRequested();

        // Kernel vNext: Resolve the DI-scoped GridContext and initialize it
        // This ensures exactly one GridContext per scope, owned by Kernel
        if (context.ServiceProvider is null)
        {
            throw new InvalidOperationException(
                "MessageContext.ServiceProvider is null. Transport consumers must provide a scoped " +
                "IServiceProvider for Kernel vNext GridContext integration. Ensure the consumer " +
                "creates a DI scope for each message and sets MessageContext.ServiceProvider.");
        }

        // Resolve the DI-scoped IGridContext (owned by Kernel)
        var gridContext = context.ServiceProvider.GetRequiredService<IGridContext>();

        // Initialize it with envelope metadata
        _gridContextFactory.InitializeFromEnvelope(gridContext, envelope, cancellationToken);

        // Populate MessageContext.GridContext - this is the SAME instance as DI's
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
