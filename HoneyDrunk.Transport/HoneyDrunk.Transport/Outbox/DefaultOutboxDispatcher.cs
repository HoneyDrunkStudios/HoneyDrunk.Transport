using HoneyDrunk.Transport.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HoneyDrunk.Transport.Outbox;

/// <summary>
/// Default outbox dispatcher implementation that runs as a background service.
/// Polls the outbox store for pending messages and publishes them to the transport.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="DefaultOutboxDispatcher"/> class.
/// </remarks>
/// <param name="store">The outbox store for loading pending messages.</param>
/// <param name="publisher">The transport publisher for dispatching messages.</param>
/// <param name="options">Configuration options for the dispatcher.</param>
/// <param name="logger">Logger instance.</param>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1873:Use cached 'SearchValues' instance", Justification = "LoggerMessage source generator creates optimized logging code that only evaluates arguments when logging is enabled.")]
public sealed partial class DefaultOutboxDispatcher(
    IOutboxStore store,
    ITransportPublisher publisher,
    IOptions<OutboxDispatcherOptions> options,
    ILogger<DefaultOutboxDispatcher> logger) : BackgroundService, IOutboxDispatcher
{
    private readonly OutboxDispatcherOptions _options = options.Value;

    /// <inheritdoc/>
    public async Task DispatchPendingAsync(
        int batchSize = 100,
        CancellationToken cancellationToken = default)
    {
        var pending = await store.LoadPendingAsync(batchSize, cancellationToken);
        var messages = pending.ToList();

        if (messages.Count == 0)
        {
            return;
        }

        LogDispatchingMessages(logger, messages.Count);

        var successCount = 0;
        var failureCount = 0;

        foreach (var message in messages)
        {
            try
            {
                await publisher.PublishAsync(
                    message.Envelope,
                    message.Destination,
                    cancellationToken);

                await store.MarkDispatchedAsync(message.Id, cancellationToken);
                successCount++;

                LogMessageDispatched(logger, message.Id);
            }
            catch (Exception ex)
            {
                failureCount++;

                LogDispatchFailed(logger, message.Id, ex);

                // Check if we've exceeded max retries
                if (message.Attempts >= _options.MaxRetryAttempts)
                {
                    await store.MarkPoisonedAsync(
                        message.Id,
                        $"Exceeded maximum retry attempts ({_options.MaxRetryAttempts})",
                        cancellationToken);

                    LogMessagePoisoned(logger, message.Id, message.Attempts);
                }
                else
                {
                    // Calculate next retry time with exponential backoff
                    var retryDelay = CalculateRetryDelay(message.Attempts);
                    var retryAt = DateTimeOffset.UtcNow.Add(retryDelay);

                    await store.MarkFailedAsync(
                        message.Id,
                        ex.Message,
                        retryAt,
                        cancellationToken);
                }
            }
        }

        if (successCount > 0 || failureCount > 0)
        {
            LogDispatchCompleted(logger, successCount, failureCount);
        }
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogDispatcherStarting(logger, _options.PollInterval);

        // Wait for startup delay if configured
        if (_options.StartupDelay > TimeSpan.Zero)
        {
            await Task.Delay(_options.StartupDelay, stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchPendingAsync(_options.BatchSize, stoppingToken);

                // Wait for poll interval before next iteration
                await Task.Delay(_options.PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Expected during shutdown
                break;
            }
            catch (Exception ex)
            {
                LogDispatcherLoopError(logger, ex);

                // Wait for error delay before retrying
                await Task.Delay(_options.ErrorDelay, stoppingToken);
            }
        }

        LogDispatcherStopped(logger);
    }

    // LoggerMessage source generator methods for high-performance logging
    [LoggerMessage(Level = LogLevel.Debug, Message = "Dispatching {Count} pending outbox messages")]
    private static partial void LogDispatchingMessages(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Dispatched outbox message {MessageId}")]
    private static partial void LogMessageDispatched(ILogger logger, string messageId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to dispatch outbox message {MessageId}")]
    private static partial void LogDispatchFailed(ILogger logger, string messageId, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Marked outbox message {MessageId} as poisoned after {Attempts} attempts")]
    private static partial void LogMessagePoisoned(ILogger logger, string messageId, int attempts);

    [LoggerMessage(Level = LogLevel.Information, Message = "Outbox dispatch completed: {Success} succeeded, {Failed} failed")]
    private static partial void LogDispatchCompleted(ILogger logger, int success, int failed);

    [LoggerMessage(Level = LogLevel.Information, Message = "Outbox dispatcher starting with poll interval {PollInterval}")]
    private static partial void LogDispatcherStarting(ILogger logger, TimeSpan pollInterval);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error in outbox dispatcher loop")]
    private static partial void LogDispatcherLoopError(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Outbox dispatcher stopped")]
    private static partial void LogDispatcherStopped(ILogger logger);

    private TimeSpan CalculateRetryDelay(int attemptCount)
    {
        // Exponential backoff: 2^attempt * base delay, capped at max delay
        var exponentialMultiplier = Math.Pow(2, attemptCount);
        var delay = TimeSpan.FromMilliseconds(_options.BaseRetryDelay.TotalMilliseconds * exponentialMultiplier);
        return delay > _options.MaxRetryDelay ? _options.MaxRetryDelay : delay;
    }
}
