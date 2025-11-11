using HoneyDrunk.Transport.Abstractions;
using Microsoft.Extensions.Logging;

namespace HoneyDrunk.Transport.Pipeline.Middleware;

/// <summary>
/// Middleware that handles retries with exponential backoff.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="RetryMiddleware"/> class.
/// </remarks>
/// <param name="logger">The logger instance.</param>
/// <param name="maxAttempts">Maximum number of retry attempts.</param>
public sealed class RetryMiddleware(ILogger<RetryMiddleware> logger, int maxAttempts = 3) : IMessageMiddleware
{
    private readonly ILogger<RetryMiddleware> _logger = logger;
    private readonly int _maxAttempts = maxAttempts;

    /// <inheritdoc/>
    public async Task InvokeAsync(
        ITransportEnvelope envelope,
        MessageContext context,
        Func<Task> next,
        CancellationToken cancellationToken = default)
    {
        if (context.DeliveryCount > _maxAttempts)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning(
                    "Message {MessageId} exceeded max retry attempts ({MaxAttempts})",
                    envelope.MessageId,
                    _maxAttempts);
            }

            throw new MessageHandlerException(
                "Max retry attempts exceeded",
                MessageProcessingResult.DeadLetter);
        }

        await next();
    }
}
