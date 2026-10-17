using HoneyDrunk.Transport.Abstractions;

namespace HoneyDrunk.Transport.Outbox;

/// <summary>
/// Storage abstraction for the transactional outbox pattern.
/// </summary>
public interface IOutboxStore
{
    /// <summary>
    /// Saves an outgoing message to the outbox within a transaction.
    /// </summary>
    /// <param name="destination">The destination endpoint.</param>
    /// <param name="envelope">The message envelope to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SaveAsync(
        IEndpointAddress destination,
        ITransportEnvelope envelope,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves multiple outgoing messages as a batch within a transaction.
    /// </summary>
    /// <param name="messages">The collection of message tuples containing destination and envelope.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SaveBatchAsync(
        IEnumerable<(IEndpointAddress destination, ITransportEnvelope envelope)> messages,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads pending messages ready for dispatch.
    /// </summary>
    /// <param name="batchSize">Maximum number of messages to load.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of pending outbox messages.</returns>
    Task<IEnumerable<IOutboxMessage>> LoadPendingAsync(
        int batchSize = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a message as successfully dispatched.
    /// </summary>
    /// <param name="outboxMessageId">The outbox message identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task MarkDispatchedAsync(
        string outboxMessageId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a message as failed with error details and schedules retry.
    /// </summary>
    /// <param name="outboxMessageId">The outbox message identifier.</param>
    /// <param name="errorMessage">The error message.</param>
    /// <param name="retryAt">Optional timestamp for next retry attempt.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task MarkFailedAsync(
        string outboxMessageId,
        string errorMessage,
        DateTimeOffset? retryAt = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a message as poisoned when it exceeds retry limits.
    /// </summary>
    /// <param name="outboxMessageId">The outbox message identifier.</param>
    /// <param name="errorMessage">The error message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task MarkPoisonedAsync(
        string outboxMessageId,
        string errorMessage,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes old dispatched messages for cleanup.
    /// </summary>
    /// <param name="olderThan">Timestamp threshold for cleanup.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CleanupDispatchedAsync(
        DateTimeOffset olderThan,
        CancellationToken cancellationToken = default);
}
