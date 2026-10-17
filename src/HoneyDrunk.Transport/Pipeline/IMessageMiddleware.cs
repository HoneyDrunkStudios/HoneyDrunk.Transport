using HoneyDrunk.Transport.Abstractions;

namespace HoneyDrunk.Transport.Pipeline;

/// <summary>
/// Middleware component in the message processing pipeline.
/// </summary>
public interface IMessageMiddleware
{
    /// <summary>
    /// Processes the envelope and invokes the next middleware in the pipeline.
    /// </summary>
    /// <param name="envelope">The message envelope.</param>
    /// <param name="context">The message context.</param>
    /// <param name="next">The next middleware delegate to invoke.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task InvokeAsync(
        ITransportEnvelope envelope,
        MessageContext context,
        Func<Task> next,
        CancellationToken cancellationToken = default);
}
