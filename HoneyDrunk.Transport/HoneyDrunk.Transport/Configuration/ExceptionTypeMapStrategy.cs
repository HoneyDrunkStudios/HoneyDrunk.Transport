namespace HoneyDrunk.Transport.Configuration;

/// <summary>
/// Exception type mapping strategy that maps specific exception types to decisions.
/// Provides a simple dictionary-based approach for error handling configuration.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ExceptionTypeMapStrategy"/> class.
/// </remarks>
/// <param name="typeMap">Mapping of exception types to actions.</param>
/// <param name="maxDeliveryCount">Maximum delivery count before dead-lettering.</param>
/// <param name="defaultAction">Default action for unmapped exception types.</param>
public sealed class ExceptionTypeMapStrategy(
    Dictionary<Type, ErrorHandlingAction> typeMap,
    int maxDeliveryCount = 5,
    ErrorHandlingAction defaultAction = ErrorHandlingAction.Retry) : IErrorHandlingStrategy
{
    /// <summary>
    /// Creates a builder for fluent configuration.
    /// </summary>
    /// <returns>A new builder instance.</returns>
    public static Builder CreateBuilder() => new();

    /// <inheritdoc/>
    public Task<ErrorHandlingDecision> HandleErrorAsync(
        Exception exception,
        int deliveryCount,
        CancellationToken cancellationToken = default)
    {
        // Max retries exceeded
        if (deliveryCount >= maxDeliveryCount)
        {
            return Task.FromResult(ErrorHandlingDecision.DeadLetter(
                $"Maximum delivery count ({maxDeliveryCount}) exceeded"));
        }

        // Check for exact match
        var exceptionType = exception.GetType();
        if (typeMap.TryGetValue(exceptionType, out var action))
        {
            return Task.FromResult(CreateDecision(
                action,
                deliveryCount,
                $"Mapped action for {exceptionType.Name}"));
        }

        // Check for base type match
        foreach (var kvp in typeMap)
        {
            if (kvp.Key.IsAssignableFrom(exceptionType))
            {
                return Task.FromResult(CreateDecision(
                    kvp.Value,
                    deliveryCount,
                    $"Inherited action from {kvp.Key.Name}"));
            }
        }

        // Use default action
        return Task.FromResult(CreateDecision(
            defaultAction,
            deliveryCount,
            "Default action for unmapped exception type"));
    }

    private static ErrorHandlingDecision CreateDecision(
        ErrorHandlingAction action,
        int deliveryCount,
        string reason)
    {
        return action switch
        {
            ErrorHandlingAction.Retry => ErrorHandlingDecision.Retry(
                TimeSpan.FromSeconds(Math.Pow(2, Math.Min(deliveryCount - 1, 5))),
                reason),
            ErrorHandlingAction.DeadLetter => ErrorHandlingDecision.DeadLetter(reason),
            ErrorHandlingAction.Abandon => ErrorHandlingDecision.Abandon(reason),
            _ => ErrorHandlingDecision.Retry(TimeSpan.FromSeconds(2), reason)
        };
    }

    /// <summary>
    /// Builder for creating ExceptionTypeMapStrategy with fluent API.
    /// </summary>
    public sealed class Builder
    {
        private readonly Dictionary<Type, ErrorHandlingAction> _typeMap = [];
        private int _maxDeliveryCount = 5;
        private ErrorHandlingAction _defaultAction = ErrorHandlingAction.Retry;

        /// <summary>
        /// Maps an exception type to retry.
        /// </summary>
        /// <typeparam name="TException">The exception type.</typeparam>
        /// <returns>This builder for chaining.</returns>
        public Builder MapToRetry<TException>()
            where TException : Exception
        {
            _typeMap[typeof(TException)] = ErrorHandlingAction.Retry;
            return this;
        }

        /// <summary>
        /// Maps an exception type to dead-letter.
        /// </summary>
        /// <typeparam name="TException">The exception type.</typeparam>
        /// <returns>This builder for chaining.</returns>
        public Builder MapToDeadLetter<TException>()
            where TException : Exception
        {
            _typeMap[typeof(TException)] = ErrorHandlingAction.DeadLetter;
            return this;
        }

        /// <summary>
        /// Sets the maximum delivery count.
        /// </summary>
        /// <param name="maxDeliveryCount">The maximum delivery count.</param>
        /// <returns>This builder for chaining.</returns>
        public Builder WithMaxDeliveryCount(int maxDeliveryCount)
        {
            _maxDeliveryCount = maxDeliveryCount;
            return this;
        }

        /// <summary>
        /// Sets the default action for unmapped exceptions.
        /// </summary>
        /// <param name="defaultAction">The default action.</param>
        /// <returns>This builder for chaining.</returns>
        public Builder WithDefaultAction(ErrorHandlingAction defaultAction)
        {
            _defaultAction = defaultAction;
            return this;
        }

        /// <summary>
        /// Builds the strategy.
        /// </summary>
        /// <returns>A configured ExceptionTypeMapStrategy.</returns>
        public ExceptionTypeMapStrategy Build()
        {
            return new ExceptionTypeMapStrategy(_typeMap, _maxDeliveryCount, _defaultAction);
        }
    }
}
