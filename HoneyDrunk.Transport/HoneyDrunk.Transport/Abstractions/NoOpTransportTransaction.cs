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

    /// <summary>
    /// Prevents a default instance of the <see cref="NoOpTransportTransaction"/> class from being created.
    /// </summary>
    private NoOpTransportTransaction()
    {
    }

    /// <inheritdoc/>
    public string TransactionId => Guid.NewGuid().ToString();

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, object> Context => new Dictionary<string, object>();

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
