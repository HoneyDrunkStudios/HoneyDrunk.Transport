namespace HoneyDrunk.Transport.Abstractions;

/// <summary>
/// Default no-op transaction implementation for transports that don't support transactions.
/// </summary>
public sealed class NoOpTransportTransaction : ITransportTransaction
{
    /// <summary>
    /// Gets the singleton instance of the no-op transaction.
    /// </summary>
    public static readonly ITransportTransaction Instance = new NoOpTransportTransaction();

    private static readonly IReadOnlyDictionary<string, object> EmptyContext = new Dictionary<string, object>();

    /// <summary>
    /// Prevents a default instance of the <see cref="NoOpTransportTransaction"/> class from being created.
    /// </summary>
    private NoOpTransportTransaction()
    {
        TransactionId = Guid.NewGuid().ToString();
    }

    /// <inheritdoc/>
    public string TransactionId { get; }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, object> Context => EmptyContext;

    /// <inheritdoc/>
    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
