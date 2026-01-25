using HoneyDrunk.Transport.Abstractions;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

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
    /// <summary>
    /// Cache for resolved message types to avoid repeated assembly scanning.
    /// Uses a sentinel value approach: null values are stored to cache failed resolutions.
    /// </summary>
    private static readonly ConcurrentDictionary<string, Type?> TypeCache = new();

    private readonly IReadOnlyList<IMessageMiddleware> _reversedMiddlewares = [.. middlewares.Reverse()];
    private readonly IMessageSerializer _serializer = serializer;
    private readonly MessageHandlerInvoker _handlerInvoker = new(serviceProvider);
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
        catch (OperationCanceledException)
        {
            // Cancellation is special - re-throw to allow caller to handle
            throw;
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
        // Check cache first - this includes both successful and failed resolutions
        if (TypeCache.TryGetValue(typeName, out var cachedType))
        {
            return cachedType;
        }

        // Try direct resolution first (works for assembly-qualified names and mscorlib types)
        var type = Type.GetType(typeName, throwOnError: false);
        if (type != null)
        {
            TypeCache.TryAdd(typeName, type);
            return type;
        }

        // Fall back to searching loaded assemblies for the type by full name
        // This is necessary because Type.GetType only searches the calling assembly and mscorlib
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            type = assembly.GetType(typeName);
            if (type != null)
            {
                TypeCache.TryAdd(typeName, type);
                return type;
            }
        }

        // Cache the failed resolution to avoid repeated assembly scans
        TypeCache.TryAdd(typeName, null);
        return null;
    }

    private Func<Task> BuildPipeline(
        ITransportEnvelope envelope,
        MessageContext context,
        CancellationToken cancellationToken)
    {
        // The final handler in the pipeline
        Func<Task> handler = async () =>
        {
            // Check cancellation before invoking handler
            cancellationToken.ThrowIfCancellationRequested();
            await InvokeMessageHandlerAsync(envelope, context, cancellationToken);
        };

        // Apply middleware in reverse order (already reversed in constructor)
        foreach (var middleware in _reversedMiddlewares)
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

        // Invoke the handler using the optimized invoker
        var task = _handlerInvoker.InvokeHandlerAsync(message, messageType, context, cancellationToken);

        if (task == null)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning(
                    "No handler registered for message type {MessageType}",
                    envelope.MessageType);
            }

            return;
        }

        await task;
    }
}
