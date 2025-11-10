using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.Pipeline;

namespace HoneyDrunk.Transport.DependencyInjection;

/// <summary>
/// Adapter for delegate-based middleware.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="DelegateMessageMiddleware"/> class.
/// </remarks>
/// <param name="middleware">The middleware delegate.</param>
internal sealed class DelegateMessageMiddleware(MessageMiddleware middleware) : IMessageMiddleware
{
    private readonly MessageMiddleware _middleware = middleware;

    /// <inheritdoc/>
    public Task InvokeAsync(
        ITransportEnvelope envelope,
        MessageContext context,
        Func<Task> next,
        CancellationToken cancellationToken = default)
    {
        return _middleware(envelope, context, next, cancellationToken);
    }
}
