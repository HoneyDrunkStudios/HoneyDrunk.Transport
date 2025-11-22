using HoneyDrunk.Transport.Abstractions;
using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace HoneyDrunk.Transport.Pipeline;

/// <summary>
/// Provides high-performance message handler invocation using compiled delegates.
/// </summary>
/// <remarks>
/// This class caches compiled expression trees per message type to avoid reflection overhead
/// on every message processing operation. The delegates are thread-safe and built lazily.
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="MessageHandlerInvoker"/> class.
/// </remarks>
/// <param name="serviceProvider">The service provider for resolving handlers.</param>
internal sealed class MessageHandlerInvoker(IServiceProvider serviceProvider)
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ConcurrentDictionary<Type, Func<object, object, MessageContext, CancellationToken, Task>> _invokerCache = new();

    /// <summary>
    /// Invokes the message handler for the specified message type.
    /// </summary>
    /// <param name="message">The message instance to handle.</param>
    /// <param name="messageType">The message type.</param>
    /// <param name="context">The message context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, or null if no handler is registered.</returns>
    /// <exception cref="ArgumentNullException">Thrown when message or messageType is null.</exception>
    public Task? InvokeHandlerAsync(
        object message,
        Type messageType,
        MessageContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(messageType);

        // Get or compile the invoker for this message type
        var invoker = _invokerCache.GetOrAdd(messageType, BuildInvoker);

        // Resolve the handler instance
        var handlerType = typeof(IMessageHandler<>).MakeGenericType(messageType);
        var handler = _serviceProvider.GetService(handlerType);

        // Return null if no handler is registered
        if (handler == null)
        {
            return null;
        }

        // Invoke the handler using the compiled delegate
        return invoker(handler, message, context, cancellationToken);
    }

    /// <summary>
    /// Builds a compiled delegate for invoking the handler's HandleAsync method.
    /// </summary>
    /// <param name="messageType">The message type.</param>
    /// <returns>A compiled delegate that can invoke the handler.</returns>
    private static Func<object, object, MessageContext, CancellationToken, Task> BuildInvoker(Type messageType)
    {
        // Create the handler interface type: IMessageHandler<TMessage>
        var handlerInterfaceType = typeof(IMessageHandler<>).MakeGenericType(messageType);

        // Get the HandleAsync method from the interface
        var handleMethod = handlerInterfaceType.GetMethod(
            nameof(IMessageHandler<>.HandleAsync),
            [messageType, typeof(MessageContext), typeof(CancellationToken)])
            ?? throw new InvalidOperationException(
                $"HandleAsync method not found on {handlerInterfaceType.Name}");

        // Create parameters for the expression
        // Signature: (object handler, object message, MessageContext context, CancellationToken ct) => Task
        var handlerParam = Expression.Parameter(typeof(object), "handler");
        var messageParam = Expression.Parameter(typeof(object), "message");
        var contextParam = Expression.Parameter(typeof(MessageContext), "context");
        var cancellationTokenParam = Expression.Parameter(typeof(CancellationToken), "cancellationToken");

        // Cast the handler to the correct interface type
        var handlerCast = Expression.Convert(handlerParam, handlerInterfaceType);

        // Cast the message to the correct message type
        var messageCast = Expression.Convert(messageParam, messageType);

        // Create the method call expression
        var methodCall = Expression.Call(
            handlerCast,
            handleMethod,
            messageCast,
            contextParam,
            cancellationTokenParam);

        // Compile the expression into a delegate
        var lambda = Expression.Lambda<Func<object, object, MessageContext, CancellationToken, Task>>(
            methodCall,
            handlerParam,
            messageParam,
            contextParam,
            cancellationTokenParam);

        return lambda.Compile();
    }
}
