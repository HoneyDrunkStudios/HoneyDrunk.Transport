using HoneyDrunk.Transport.StorageQueue.Configuration;
using HoneyDrunk.Transport.StorageQueue.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HoneyDrunk.Transport.Tests.Transports.StorageQueue;

/// <summary>
/// Tests for QueueClientFactory lifecycle and initialization.
/// </summary>
public sealed class QueueClientFactoryTests
{
    /// <summary>
    /// Verifies GetOrCreatePrimaryQueueClientAsync returns same instance on multiple calls.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetOrCreatePrimaryQueueClientAsync_CalledMultipleTimes_ReturnsSameInstance()
    {
        // Arrange
        var options = Options.Create(new StorageQueueOptions
        {
            ConnectionString = "UseDevelopmentStorage=true",
            QueueName = "test-queue",
            CreateIfNotExists = false // Don't actually create
        });

        await using var factory = new QueueClientFactory(options, NullLogger<QueueClientFactory>.Instance);

        // Act
        var client1 = await factory.GetOrCreatePrimaryQueueClientAsync();
        var client2 = await factory.GetOrCreatePrimaryQueueClientAsync();
        var client3 = await factory.GetOrCreatePrimaryQueueClientAsync();

        // Assert - all should be the same instance (reference equality)
        Assert.Same(client1, client2);
        Assert.Same(client2, client3);
    }

    /// <summary>
    /// Verifies GetOrCreatePoisonQueueClientAsync returns same instance on multiple calls.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetOrCreatePoisonQueueClientAsync_CalledMultipleTimes_ReturnsSameInstance()
    {
        // Arrange
        var options = Options.Create(new StorageQueueOptions
        {
            ConnectionString = "UseDevelopmentStorage=true",
            QueueName = "test-queue",
            PoisonQueueName = "test-queue-poison",
            CreateIfNotExists = false // Don't actually create
        });

        await using var factory = new QueueClientFactory(options, NullLogger<QueueClientFactory>.Instance);

        // Act
        var client1 = await factory.GetOrCreatePoisonQueueClientAsync();
        var client2 = await factory.GetOrCreatePoisonQueueClientAsync();
        var client3 = await factory.GetOrCreatePoisonQueueClientAsync();

        // Assert - all should be the same instance (reference equality)
        Assert.Same(client1, client2);
        Assert.Same(client2, client3);
    }

    /// <summary>
    /// Verifies primary and poison queue clients are different instances.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetPrimaryAndPoison_ReturnsDifferentInstances()
    {
        // Arrange
        var options = Options.Create(new StorageQueueOptions
        {
            ConnectionString = "UseDevelopmentStorage=true",
            QueueName = "test-queue",
            PoisonQueueName = "test-queue-poison",
            CreateIfNotExists = false
        });

        await using var factory = new QueueClientFactory(options, NullLogger<QueueClientFactory>.Instance);

        // Act
        var primaryClient = await factory.GetOrCreatePrimaryQueueClientAsync();
        var poisonClient = await factory.GetOrCreatePoisonQueueClientAsync();

        // Assert
        Assert.NotSame(primaryClient, poisonClient);
    }

    /// <summary>
    /// Verifies DisposeAsync can be called safely.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task DisposeAsync_WhenCalled_CompletesSuccessfully()
    {
        // Arrange
        var options = Options.Create(new StorageQueueOptions
        {
            ConnectionString = "UseDevelopmentStorage=true",
            QueueName = "test-queue",
            CreateIfNotExists = false
        });

        var factory = new QueueClientFactory(options, NullLogger<QueueClientFactory>.Instance);

        try
        {
            // Act — explicit single dispose (no `await using` shadow-cleanup) so the
            // test exercises the post-dispose path without relying on idempotence.
            await factory.DisposeAsync();

            // Assert — a follow-up operation must surface ObjectDisposedException, proving the dispose
            // path completed and flipped the internal sentinel.
            await Assert.ThrowsAsync<ObjectDisposedException>(() => factory.GetOrCreatePrimaryQueueClientAsync());
        }
        finally
        {
            // Defensive: idempotent (Interlocked sentinel) so the happy-path second call is a no-op.
            await factory.DisposeAsync();
        }
    }

    /// <summary>
    /// Verifies GetOrCreatePrimaryQueueClientAsync throws ObjectDisposedException after disposal.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetOrCreatePrimaryQueueClientAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var options = Options.Create(new StorageQueueOptions
        {
            ConnectionString = "UseDevelopmentStorage=true",
            QueueName = "test-queue",
            CreateIfNotExists = false
        });

        var factory = new QueueClientFactory(options, NullLogger<QueueClientFactory>.Instance);
        await factory.DisposeAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => factory.GetOrCreatePrimaryQueueClientAsync());
    }

    /// <summary>
    /// Verifies GetOrCreatePoisonQueueClientAsync throws ObjectDisposedException after disposal.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetOrCreatePoisonQueueClientAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var options = Options.Create(new StorageQueueOptions
        {
            ConnectionString = "UseDevelopmentStorage=true",
            QueueName = "test-queue",
            PoisonQueueName = "test-queue-poison",
            CreateIfNotExists = false
        });

        var factory = new QueueClientFactory(options, NullLogger<QueueClientFactory>.Instance);
        await factory.DisposeAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => factory.GetOrCreatePoisonQueueClientAsync());
    }

    /// <summary>
    /// Verifies DisposeAsync is idempotent (can be called multiple times safely).
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task DisposeAsync_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        var options = Options.Create(new StorageQueueOptions
        {
            ConnectionString = "UseDevelopmentStorage=true",
            QueueName = "test-queue",
            CreateIfNotExists = false
        });

        await using var factory = new QueueClientFactory(options, NullLogger<QueueClientFactory>.Instance);

        // Act — multiple Dispose calls must not throw.
        await factory.DisposeAsync();
        await factory.DisposeAsync();
        await factory.DisposeAsync();

        // Assert — factory is disposed, subsequent operation throws.
        await Assert.ThrowsAsync<ObjectDisposedException>(() => factory.GetOrCreatePrimaryQueueClientAsync());
    }

    /// <summary>
    /// Verifies constructor throws when neither ConnectionString nor AccountEndpoint is provided.
    /// </summary>
    [Fact]
    public void Constructor_WithNoConnectionInfo_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = Options.Create(new StorageQueueOptions
        {
            QueueName = "test-queue",
            CreateIfNotExists = false
        });

        // Neither ConnectionString nor AccountEndpoint set
        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new QueueClientFactory(options, NullLogger<QueueClientFactory>.Instance));

        Assert.Contains("ConnectionString or AccountEndpoint", exception.Message);
    }
}
