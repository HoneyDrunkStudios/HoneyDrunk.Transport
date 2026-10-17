using HoneyDrunk.Transport.Abstractions;
using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace HoneyDrunk.Transport.Pipeline;

/// <summary>
/// Provides high-performance message handler invocation using compiled delegates.
/// </summary>
/// <remarks>
/// <para>
/// This class caches compiled expression trees per message type to avoid reflection overhead
/// on every message processing operation. The delegates are thread-safe and built lazily.
/// </para>
/// <para>
/// <b>Kernel vNext (v0.4.0+):</b> Handlers are resolved from <see cref="MessageContext.ServiceProvider"/>
/// (the scoped provider) to ensure they share the same DI scope as middleware. This is critical
/// for the one-GridContext-per-scope invariant.
/// </para>
/// </remarks>
/// <param name="rootServiceProvider">The root service provider (fallback only).</param>
internal sealed class MessageHandlerInvoker(IServiceProvider rootServiceProvider)
{
    private readonly IServiceProvider _rootServiceProvider = rootServiceProvider;
    private readonly ConcurrentDictionary<Type, Func<object, object, MessageContext, CancellationToken, Task>> _invokerCache = new();

    /// <summary>
    /// Attempts to invoke the message handler for the specified message type.
    /// </summary>
    /// <param name="message">The message instance to handle.</param>
    /// <param name="messageType">The message type.</param>
    /// <param name="context">The message context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="handlerTask">When this method returns <c>true</c>, the task representing the
    /// handler's asynchronous work; otherwise <see cref="Task.CompletedTask"/>.</param>
    /// <returns><c>true</c> when a handler was registered and invoked; <c>false</c> when no handler
    /// is registered for <paramref name="messageType"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when message or messageType is null.</exception>
    public bool TryInvokeHandler(
        object message,
        Type messageType,
        MessageContext context,
        CancellationToken cancellationToken,
        out Task handlerTask)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(messageType);

        // Kernel vNext: Resolve handler from scoped provider to share DI scope with middleware
        // This ensures IGridContext injected into handler is the same instance as middleware used
        var scopedProvider = context.ServiceProvider ?? _rootServiceProvider;
        var handlerType = typeof(IMessageHandler<>).MakeGenericType(messageType);
        var handler = scopedProvider.GetService(handlerType);

        if (handler is null)
        {
            handlerTask = Task.CompletedTask;
            return false;
        }

        // Get or compile the invoker for this message type, then invoke via the compiled delegate.
        var invoker = _invokerCache.GetOrAdd(messageType, BuildInvoker);
        handlerTask = invoker(handler, message, context, cancellationToken);
        return true;
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
