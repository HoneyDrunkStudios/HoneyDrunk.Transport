using HoneyDrunk.Transport.Abstractions;

namespace HoneyDrunk.Transport.Pipeline;

/// <summary>
/// Function for message middleware processing.
/// </summary>
/// <param name="envelope">The message envelope.</param>
/// <param name="context">The message context.</param>
/// <param name="next">The next middleware in the pipeline.</param>
/// <param name="cancellationToken">Cancellation token.</param>
/// <returns>A task representing the asynchronous operation.</returns>
public delegate Task MessageMiddleware(
    ITransportEnvelope envelope,
    MessageContext context,
    Func<Task> next,
    CancellationToken cancellationToken);
