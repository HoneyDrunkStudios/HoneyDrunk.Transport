using HoneyDrunk.Transport.Abstractions;
using Microsoft.Extensions.Logging;

namespace HoneyDrunk.Transport.Pipeline;

/// <summary>
/// Default implementation of the message processing pipeline.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="MessagePipeline"/> class.
/// </remarks>
/// <param name="middlewares">The collection of middleware components.</param>
/// <param name="serializer">The message serializer.</param>
/// <param name="serviceProvider">The service provider for resolving handlers.</param>
/// <param name="logger">The logger instance.</param>
public sealed class MessagePipeline(
    IEnumerable<IMessageMiddleware> middlewares,
    IMessageSerializer serializer,
    IServiceProvider serviceProvider,
    ILogger<MessagePipeline> logger) : IMessagePipeline
{
    private readonly IEnumerable<IMessageMiddleware> _middlewares = middlewares;
    private readonly IMessageSerializer _serializer = serializer;
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<MessagePipeline> _logger = logger;

    /// <inheritdoc/>
    public async Task<MessageProcessingResult> ProcessAsync(
        ITransportEnvelope envelope,
        MessageContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Processing message {MessageId} of type {MessageType}",
                    envelope.MessageId,
                    envelope.MessageType);
            }

            // Build the pipeline with middleware
            var pipeline = BuildPipeline(envelope, context, cancellationToken);

            // Execute the pipeline
            await pipeline();

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Successfully processed message {MessageId}",
                    envelope.MessageId);
            }

            return MessageProcessingResult.Success;
        }
        catch (MessageHandlerException ex)
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(
                    ex,
                    "Message handler error for {MessageId}: {ErrorMessage}",
                    envelope.MessageId,
                    ex.Message);
            }

            return ex.Result;
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(
                    ex,
                    "Unexpected error processing message {MessageId}",
                    envelope.MessageId);
            }

            return MessageProcessingResult.Retry;
        }
    }

    private static Type? ResolveMessageType(string typeName)
    {
        try
        {
            return Type.GetType(typeName, throwOnError: false);
        }
        catch
        {
            return null;
        }
    }

    private Func<Task> BuildPipeline(
        ITransportEnvelope envelope,
        MessageContext context,
        CancellationToken cancellationToken)
    {
        // The final handler in the pipeline
        Func<Task> handler = async () =>
        {
            await InvokeMessageHandlerAsync(envelope, context, cancellationToken);
        };

        // Apply middleware in reverse order
        foreach (var middleware in _middlewares.Reverse())
        {
            var next = handler;
            handler = () => middleware.InvokeAsync(envelope, context, next, cancellationToken);
        }

        return handler;
    }

    private async Task InvokeMessageHandlerAsync(
        ITransportEnvelope envelope,
        MessageContext context,
        CancellationToken cancellationToken)
    {
        // Resolve the message type
        var messageType = ResolveMessageType(envelope.MessageType) ?? throw new MessageHandlerException(
                $"Cannot resolve message type: {envelope.MessageType}",
                MessageProcessingResult.DeadLetter);

        // Deserialize the message
        object message;
        try
        {
            message = _serializer.Deserialize(envelope.Payload, messageType);
        }
        catch (Exception ex)
        {
            throw new MessageHandlerException(
                $"Failed to deserialize message: {ex.Message}",
                MessageProcessingResult.DeadLetter,
                ex);
        }

        // Resolve and invoke the handler
        var handlerType = typeof(IMessageHandler<>).MakeGenericType(messageType);
        var handler = _serviceProvider.GetService(handlerType);

        if (handler == null)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning(
                    "No handler registered for message type {MessageType}",
                    envelope.MessageType);
            }
            return;
        }

        var handleMethod = handlerType.GetMethod(nameof(IMessageHandler<>.HandleAsync));
        if (handleMethod != null)
        {
            var task = (Task)handleMethod.Invoke(handler, [message, context, cancellationToken])!;
            await task;
        }
    }
}
