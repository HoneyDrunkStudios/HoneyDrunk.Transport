using HoneyDrunk.Transport.Abstractions;

namespace HoneyDrunk.Transport.Tests.Core.Transactions;

/// <summary>
/// Tests for the no-op transport transaction implementation.
/// </summary>
public sealed class NoOpTransportTransactionTests
{
    /// <summary>
    /// Ensures commit and rollback complete without side effects.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Instance_CommitAndRollback_DoNothing()
    {
        // Arrange
        var tx = NoOpTransportTransaction.Instance;

        // Act & Assert
        Assert.NotNull(tx.TransactionId);
        Assert.NotNull(tx.Context);
        Assert.Empty(tx.Context);

        await tx.CommitAsync();
        await tx.RollbackAsync();

        // No exceptions and context remains empty
        Assert.Empty(tx.Context);
    }
}
