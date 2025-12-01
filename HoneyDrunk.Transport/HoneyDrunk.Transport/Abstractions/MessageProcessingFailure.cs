namespace HoneyDrunk.Transport.Abstractions;

/// <summary>
/// Structured failure metadata for message processing results.
/// Provides richer error context than simple MessageProcessingResult enum.
/// </summary>
public sealed class MessageProcessingFailure
{
    /// <summary>
    /// Gets the processing result (Success, Retry, or DeadLetter).
    /// </summary>
    public required MessageProcessingResult Result { get; init; }

    /// <summary>
    /// Gets the human-readable reason for the failure.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Gets the failure category for grouping and analytics.
    /// Examples: "Validation", "Timeout", "DatabaseError", "BusinessRule".
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// Gets the exception that caused the failure, if applicable.
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    /// Gets additional metadata about the failure for diagnostic purposes.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }

    /// <summary>
    /// Creates a success result.
    /// </summary>
    /// <returns>A MessageProcessingFailure indicating successful processing.</returns>
    public static MessageProcessingFailure Success() =>
        new() { Result = MessageProcessingResult.Success };

    /// <summary>
    /// Creates a retry result with a reason and optional exception.
    /// </summary>
    /// <param name="reason">The reason for retrying.</param>
    /// <param name="exception">The exception that caused the retry, if any.</param>
    /// <param name="category">Optional category for the failure.</param>
    /// <param name="metadata">Optional additional metadata.</param>
    /// <returns>A MessageProcessingFailure indicating retry is needed.</returns>
    public static MessageProcessingFailure Retry(
        string reason,
        Exception? exception = null,
        string? category = null,
        IReadOnlyDictionary<string, object>? metadata = null) =>
        new()
        {
            Result = MessageProcessingResult.Retry,
            Reason = reason,
            Exception = exception,
            Category = category,
            Metadata = metadata
        };

    /// <summary>
    /// Creates a dead-letter result with a reason.
    /// </summary>
    /// <param name="reason">The reason for dead-lettering the message.</param>
    /// <param name="category">Optional category for the failure.</param>
    /// <param name="exception">The exception that caused the dead-letter, if any.</param>
    /// <param name="metadata">Optional additional metadata.</param>
    /// <returns>A MessageProcessingFailure indicating the message should be dead-lettered.</returns>
    public static MessageProcessingFailure DeadLetter(
        string reason,
        string? category = null,
        Exception? exception = null,
        IReadOnlyDictionary<string, object>? metadata = null) =>
        new()
        {
            Result = MessageProcessingResult.DeadLetter,
            Reason = reason,
            Category = category,
            Exception = exception,
            Metadata = metadata
        };

    /// <summary>
    /// Creates a failure result from an exception using default categorization.
    /// </summary>
    /// <param name="exception">The exception to create a failure from.</param>
    /// <param name="deliveryCount">The current delivery count for retry limit checking.</param>
    /// <param name="maxRetries">The maximum number of retries before dead-lettering.</param>
    /// <returns>A MessageProcessingFailure with appropriate result based on exception type.</returns>
    public static MessageProcessingFailure FromException(
        Exception exception,
        int deliveryCount,
        int maxRetries = 5)
    {
        // Max retries exceeded
        if (deliveryCount > maxRetries)
        {
            return DeadLetter(
                $"Maximum retry attempts ({maxRetries}) exceeded",
                category: "MaxRetriesExceeded",
                exception: exception);
        }

        // Transient exceptions - retry
        if (exception is TimeoutException or
            HttpRequestException { StatusCode: System.Net.HttpStatusCode.ServiceUnavailable })
        {
            return Retry(
                exception.Message,
                exception: exception,
                category: "TransientError");
        }

        // Permanent exceptions - dead letter
        if (exception is ArgumentException or
            ArgumentNullException or
            InvalidOperationException)
        {
            return DeadLetter(
                exception.Message,
                category: "PermanentError",
                exception: exception);
        }

        // Unknown exception - retry with caution
        return Retry(
            exception.Message,
            exception: exception,
            category: "UnknownError");
    }
}
