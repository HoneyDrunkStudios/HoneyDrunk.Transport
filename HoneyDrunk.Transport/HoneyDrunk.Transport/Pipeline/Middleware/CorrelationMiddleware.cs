using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.Context;

namespace HoneyDrunk.Transport.Pipeline.Middleware;

/// <summary>
/// Middleware that enriches the message context with correlation information.
/// DEPRECATED: Use GridContextPropagationMiddleware for full Grid-aware context propagation.
/// This middleware remains for backward compatibility.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="CorrelationMiddleware"/> class.
/// </remarks>
/// <param name="gridContextFactory">The Grid context factory.</param>
[Obsolete("Use GridContextPropagationMiddleware for full Grid context propagation. This will be removed in v1.0.")]
public sealed class CorrelationMiddleware(IGridContextFactory gridContextFactory) : IMessageMiddleware
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

        // Create Grid context from envelope (backward compatible behavior)
        var gridContext = _gridContextFactory.CreateFromEnvelope(envelope, cancellationToken);

        // Store in properties for backward compatibility
        context.Properties["KernelContext"] = gridContext; // Legacy key
        context.Properties["GridContext"] = gridContext;
        context.Properties["CorrelationId"] = gridContext.CorrelationId ?? string.Empty;
        context.Properties["CausationId"] = gridContext.CausationId ?? string.Empty;

        return next();
    }
}
