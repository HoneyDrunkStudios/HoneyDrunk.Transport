namespace HoneyDrunk.Transport.Configuration;

/// <summary>
/// Strategy for handling failed messages.
/// </summary>
public interface IErrorHandlingStrategy
{
    /// <summary>
    /// Determines how to handle a failed message.
    /// </summary>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="deliveryCount">The number of delivery attempts.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The error handling decision.</returns>
    Task<ErrorHandlingDecision> HandleErrorAsync(
        Exception exception,
        int deliveryCount,
        CancellationToken cancellationToken = default);
}
