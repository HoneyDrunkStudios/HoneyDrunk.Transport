namespace HoneyDrunk.Transport.Abstractions;

/// <summary>
/// Handles a specific message type.
/// </summary>
/// <typeparam name="TMessage">The message type to handle.</typeparam>
public interface IMessageHandler<in TMessage>
    where TMessage : class
{
    /// <summary>
    /// Handles the message.
    /// </summary>
    /// <param name="message">The message to handle.</param>
    /// <param name="context">The message context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task HandleAsync(TMessage message, MessageContext context, CancellationToken cancellationToken = default);
}
