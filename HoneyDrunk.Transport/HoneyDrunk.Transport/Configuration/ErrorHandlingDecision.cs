namespace HoneyDrunk.Transport.Configuration;

/// <summary>
/// Decision on how to handle a failed message.
/// </summary>
public sealed class ErrorHandlingDecision
{
    /// <summary>
    /// Gets or initializes the action to take for the failed message.
    /// </summary>
    public required ErrorHandlingAction Action { get; init; }

    /// <summary>
    /// Gets or initializes the optional retry delay.
    /// </summary>
    public TimeSpan? RetryDelay { get; init; }

    /// <summary>
    /// Gets or initializes the optional reason for the decision.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Creates a retry decision with the specified delay.
    /// </summary>
    /// <param name="delay">The retry delay duration.</param>
    /// <param name="reason">Optional reason for retry.</param>
    /// <returns>An error handling decision.</returns>
    public static ErrorHandlingDecision Retry(TimeSpan delay, string? reason = null)
        => new() { Action = ErrorHandlingAction.Retry, RetryDelay = delay, Reason = reason };

    /// <summary>
    /// Creates a dead letter decision.
    /// </summary>
    /// <param name="reason">Optional reason for dead lettering.</param>
    /// <returns>An error handling decision.</returns>
    public static ErrorHandlingDecision DeadLetter(string? reason = null)
        => new() { Action = ErrorHandlingAction.DeadLetter, Reason = reason };

    /// <summary>
    /// Creates an abandon decision.
    /// </summary>
    /// <param name="reason">Optional reason for abandoning.</param>
    /// <returns>An error handling decision.</returns>
    public static ErrorHandlingDecision Abandon(string? reason = null)
        => new() { Action = ErrorHandlingAction.Abandon, Reason = reason };
}
