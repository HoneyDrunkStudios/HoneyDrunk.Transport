namespace HoneyDrunk.Transport.Abstractions;

/// <summary>
/// Handler function for processing messages of a specific type.
/// </summary>
/// <typeparam name="TMessage">The message type.</typeparam>
/// <param name="message">The message to process.</param>
/// <param name="context">The message context.</param>
/// <param name="cancellationToken">Cancellation token.</param>
/// <returns>A task representing the asynchronous operation.</returns>
public delegate Task MessageHandler<in TMessage>(
    TMessage message,
    MessageContext context,
    CancellationToken cancellationToken)
    where TMessage : class;
