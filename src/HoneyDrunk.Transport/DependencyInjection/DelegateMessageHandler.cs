using HoneyDrunk.Transport.Abstractions;

namespace HoneyDrunk.Transport.DependencyInjection;

/// <summary>
/// Adapter for delegate-based message handlers.
/// </summary>
/// <typeparam name="TMessage">The message type.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="DelegateMessageHandler{TMessage}"/> class.
/// </remarks>
/// <param name="handler">The handler delegate.</param>
internal sealed class DelegateMessageHandler<TMessage>(MessageHandler<TMessage> handler) : IMessageHandler<TMessage>
    where TMessage : class
{
    private readonly MessageHandler<TMessage> _handler = handler;

    /// <inheritdoc/>
    public Task HandleAsync(TMessage message, MessageContext context, CancellationToken cancellationToken = default)
    {
        return _handler(message, context, cancellationToken);
    }
}
