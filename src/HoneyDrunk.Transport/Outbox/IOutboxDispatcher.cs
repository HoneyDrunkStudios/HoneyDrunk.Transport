namespace HoneyDrunk.Transport.Outbox;

/// <summary>
/// Dispatches outbox messages to the transport.
/// </summary>
public interface IOutboxDispatcher
{
    /// <summary>
    /// Processes pending outbox messages and dispatches them.
    /// </summary>
    /// <param name="batchSize">Maximum number of messages to process.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DispatchPendingAsync(
        int batchSize = 100,
        CancellationToken cancellationToken = default);
}
