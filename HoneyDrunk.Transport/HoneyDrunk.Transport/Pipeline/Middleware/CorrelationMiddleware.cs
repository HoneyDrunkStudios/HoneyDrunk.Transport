using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.Context;

namespace HoneyDrunk.Transport.Pipeline.Middleware;

/// <summary>
/// Middleware that enriches the message context with correlation information.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="CorrelationMiddleware"/> class.
/// </remarks>
/// <param name="contextFactory">The kernel context factory.</param>
public sealed class CorrelationMiddleware(IKernelContextFactory contextFactory) : IMessageMiddleware
{
    private readonly IKernelContextFactory _contextFactory = contextFactory;

    /// <inheritdoc/>
    public Task InvokeAsync(
        ITransportEnvelope envelope,
        MessageContext context,
        Func<Task> next,
        CancellationToken cancellationToken = default)
    {
        // Create kernel context from envelope
        var kernelContext = _contextFactory.CreateFromEnvelope(envelope, cancellationToken);

        // Store in properties for downstream middleware/handlers
        context.Properties["KernelContext"] = kernelContext;
        context.Properties["CorrelationId"] = kernelContext.CorrelationId ?? string.Empty;
        context.Properties["CausationId"] = kernelContext.CausationId ?? string.Empty;

        return next();
    }
}
