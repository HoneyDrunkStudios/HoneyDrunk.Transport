namespace HoneyDrunk.Transport.Configuration;

/// <summary>
/// Configurable error handling strategy with rule-based decision making.
/// Allows customization of retry/dead-letter behavior per exception type.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ConfigurableErrorHandlingStrategy"/> class.
/// </remarks>
/// <param name="maxDeliveryCount">Maximum delivery count before dead-lettering (default: 5).</param>
public sealed class ConfigurableErrorHandlingStrategy(int maxDeliveryCount = 5) : IErrorHandlingStrategy
{
    private readonly Dictionary<Type, ErrorHandlingDecision> _exceptionMap = [];
    private readonly int _maxDeliveryCount = maxDeliveryCount;

    /// <summary>
    /// Registers an exception type to be retried.
    /// </summary>
    /// <typeparam name="TException">The exception type.</typeparam>
    /// <param name="retryDelay">The delay before retrying.</param>
    /// <returns>This strategy for fluent configuration.</returns>
    public ConfigurableErrorHandlingStrategy RetryOn<TException>(TimeSpan? retryDelay = null)
        where TException : Exception
    {
        _exceptionMap[typeof(TException)] = ErrorHandlingDecision.Retry(
            retryDelay ?? TimeSpan.FromSeconds(2));
        return this;
    }

    /// <summary>
    /// Registers an exception type to be dead-lettered immediately.
    /// </summary>
    /// <typeparam name="TException">The exception type.</typeparam>
    /// <returns>This strategy for fluent configuration.</returns>
    public ConfigurableErrorHandlingStrategy DeadLetterOn<TException>()
        where TException : Exception
    {
        _exceptionMap[typeof(TException)] = ErrorHandlingDecision.DeadLetter();
        return this;
    }

    /// <inheritdoc/>
    public Task<ErrorHandlingDecision> HandleErrorAsync(
        Exception exception,
        int deliveryCount,
        CancellationToken cancellationToken = default)
    {
        // Max retries exceeded
        if (deliveryCount >= _maxDeliveryCount)
        {
            return Task.FromResult(ErrorHandlingDecision.DeadLetter(
                $"Maximum delivery count ({_maxDeliveryCount}) exceeded"));
        }

        // Check exact type match
        var exceptionType = exception.GetType();
        if (_exceptionMap.TryGetValue(exceptionType, out var decision))
        {
            return Task.FromResult(decision);
        }

        // Check for base type match
        foreach (var kvp in _exceptionMap)
        {
            if (kvp.Key.IsAssignableFrom(exceptionType))
            {
                return Task.FromResult(kvp.Value);
            }
        }

        // Default: retry unknown exceptions with exponential backoff
        var delay = TimeSpan.FromSeconds(Math.Pow(2, Math.Min(deliveryCount - 1, 5)));
        return Task.FromResult(ErrorHandlingDecision.Retry(
            delay,
            "Unknown exception type - retry with backoff"));
    }
}
