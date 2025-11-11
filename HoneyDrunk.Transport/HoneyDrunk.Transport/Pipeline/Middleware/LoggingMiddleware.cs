using HoneyDrunk.Transport.Abstractions;
using Microsoft.Extensions.Logging;

namespace HoneyDrunk.Transport.Pipeline.Middleware;

/// <summary>
/// Middleware that logs message processing activities.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="LoggingMiddleware"/> class.
/// </remarks>
/// <param name="logger">The logger instance.</param>
public sealed class LoggingMiddleware(ILogger<LoggingMiddleware> logger) : IMessageMiddleware
{
    private readonly ILogger<LoggingMiddleware> _logger = logger;

    /// <inheritdoc/>
    public async Task InvokeAsync(
        ITransportEnvelope envelope,
        MessageContext context,
        Func<Task> next,
        CancellationToken cancellationToken = default)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Received message {MessageId} of type {MessageType} (CorrelationId: {CorrelationId})",
                envelope.MessageId,
                envelope.MessageType,
                envelope.CorrelationId ?? "none");
        }

        await next();

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Completed processing message {MessageId}",
                envelope.MessageId);
        }
    }
}
