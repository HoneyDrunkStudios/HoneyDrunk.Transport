namespace HoneyDrunk.Transport.Abstractions;

/// <summary>
/// Represents a transport-specific transaction context for message processing.
/// </summary>
public interface ITransportTransaction
{
    /// <summary>
    /// Gets the unique identifier for this transaction context.
    /// </summary>
    string TransactionId { get; }

    /// <summary>
    /// Gets the transport-specific transaction context data.
    /// </summary>
    IReadOnlyDictionary<string, object> Context { get; }

    /// <summary>
    /// Commits the transaction, acknowledging successful message processing.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back the transaction, typically abandoning the message for reprocessing.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
