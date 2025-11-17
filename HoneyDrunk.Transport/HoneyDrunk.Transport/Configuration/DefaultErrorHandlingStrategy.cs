using Microsoft.Extensions.Logging;

namespace HoneyDrunk.Transport.Configuration;

/// <summary>
/// Default error handling strategy implementing simple classification rules.
/// Retries transient exceptions with exponential backoff; dead letters permanent; unknown retries linear then dead letters.
/// </summary>
public sealed class DefaultErrorHandlingStrategy(ILogger<DefaultErrorHandlingStrategy> logger) : IErrorHandlingStrategy
{
    // Pre-compiled logging delegates to minimize formatting overhead (CA1873)
    private static readonly Action<ILogger, int, Exception?> PermanentFailureLog = LoggerMessage.Define<int>(
        LogLevel.Warning,
        new EventId(1001, nameof(PermanentFailureLog)),
        "Permanent failure detected (deliveryCount={DeliveryCount}). Dead-lettering message.");

    private static readonly Action<ILogger, TimeSpan, int, Exception?> TransientFailureLog = LoggerMessage.Define<TimeSpan, int>(
        LogLevel.Information,
        new EventId(1002, nameof(TransientFailureLog)),
        "Transient failure will retry after {Delay} (deliveryCount={DeliveryCount}).");

    private static readonly Action<ILogger, int, Exception?> UnknownExceededLog = LoggerMessage.Define<int>(
        LogLevel.Warning,
        new EventId(1003, nameof(UnknownExceededLog)),
        "Unknown failure exceeded max attempts (deliveryCount={DeliveryCount}). Dead-lettering message.");

    private static readonly Action<ILogger, TimeSpan, int, Exception?> UnknownRetryLog = LoggerMessage.Define<TimeSpan, int>(
        LogLevel.Information,
        new EventId(1004, nameof(UnknownRetryLog)),
        "Unknown failure will retry after {Delay} (deliveryCount={DeliveryCount}).");

    private readonly ILogger<DefaultErrorHandlingStrategy> _logger = logger;

    /// <inheritdoc />
    public Task<ErrorHandlingDecision> HandleErrorAsync(
        Exception exception,
        int deliveryCount,
        CancellationToken cancellationToken = default)
    {
        if (IsPermanent(exception))
        {
            PermanentFailureLog(_logger, deliveryCount, exception);
            return Task.FromResult(ErrorHandlingDecision.DeadLetter(exception.Message));
        }

        if (IsTransient(exception))
        {
            var delay = CalculateExponentialDelay(deliveryCount, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30));
            TransientFailureLog(_logger, delay, deliveryCount, exception);
            return Task.FromResult(ErrorHandlingDecision.Retry(delay, exception.Message));
        }

        if (deliveryCount >= 5)
        {
            UnknownExceededLog(_logger, deliveryCount, exception);
            return Task.FromResult(ErrorHandlingDecision.DeadLetter(exception.Message));
        }

        var linearDelay = TimeSpan.FromSeconds(Math.Min(deliveryCount * 2, 20));
        UnknownRetryLog(_logger, linearDelay, deliveryCount, exception);
        return Task.FromResult(ErrorHandlingDecision.Retry(linearDelay, exception.Message));
    }

    private static bool IsTransient(Exception ex) => ex switch
    {
        TimeoutException => true,
        TaskCanceledException => true,
        OperationCanceledException => true,
        IOException => true,
        System.Net.Sockets.SocketException => true,
        _ => false
    };

    private static bool IsPermanent(Exception ex) => ex switch
    {
        ArgumentException => true,
        InvalidOperationException => true,
        NotImplementedException => true,
        _ => false
    };

    private static TimeSpan CalculateExponentialDelay(int attempt, TimeSpan initial, TimeSpan max)
    {
        if (attempt <= 0)
        {
            return initial;
        }

        var factor = Math.Pow(2, Math.Min(attempt - 1, 10));
        var delayMs = initial.TotalMilliseconds * factor;
        if (delayMs > max.TotalMilliseconds)
        {
            delayMs = max.TotalMilliseconds;
        }

        // Add jitter (±25%)
        var jitter = 0.75 + (Random.Shared.NextDouble() * 0.5); // 0.75 - 1.25
        delayMs *= jitter;
        return TimeSpan.FromMilliseconds(delayMs);
    }
}
