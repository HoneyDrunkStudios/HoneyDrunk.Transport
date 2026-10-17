using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.Pipeline;

namespace HoneyDrunk.Transport.DependencyInjection;

/// <summary>
/// No-op middleware for conditional registration.
/// </summary>
internal sealed class NoOpMiddleware : IMessageMiddleware
{
    /// <inheritdoc/>
    public Task InvokeAsync(
        ITransportEnvelope envelope,
        MessageContext context,
        Func<Task> next,
        CancellationToken cancellationToken = default)
    {
        return next();
    }
}
