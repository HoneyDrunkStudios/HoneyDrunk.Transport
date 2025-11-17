using HoneyDrunk.Transport.Exceptions;
using HoneyDrunk.Transport.StorageQueue.Configuration;
using HoneyDrunk.Transport.StorageQueue.Internal;
using HoneyDrunk.Transport.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HoneyDrunk.Transport.Tests.Transports.StorageQueue;

/// <summary>
/// Tests for StorageQueueSender message size validation.
/// </summary>
public sealed class StorageQueueSenderTests
{
    /// <summary>
    /// Verifies PublishAsync throws MessageTooLargeException for oversized payload.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task PublishAsync_WithOversizedPayload_ThrowsMessageTooLargeException()
    {
        // Arrange - create a payload that exceeds ~64KB when base64-encoded
        // Azure Storage Queue max is ~64KB after base64 encoding
        // Create 70KB payload to ensure it exceeds the limit
        var largePayload = new byte[70 * 1024];
        Array.Fill(largePayload, (byte)'X');

        var envelope = new HoneyDrunk.Transport.Primitives.TransportEnvelope
        {
            MessageId = Guid.NewGuid().ToString("N"),
            CorrelationId = Guid.NewGuid().ToString("N"),
            Timestamp = DateTimeOffset.UtcNow,
            MessageType = typeof(SampleMessage).FullName!,
            Headers = new Dictionary<string, string>(),
            Payload = largePayload
        };

        var options = Options.Create(new StorageQueueOptions
        {
            ConnectionString = "UseDevelopmentStorage=true",
            QueueName = "test-queue",
            CreateIfNotExists = false
        });

        var factory = new QueueClientFactory(options, NullLogger<QueueClientFactory>.Instance);
        var sender = new StorageQueueSender(factory, options, NullLogger<StorageQueueSender>.Instance);

        var destination = TestData.Address("test-queue", "test-queue");

        try
        {
            // Act & Assert
            var exception = await Assert.ThrowsAsync<MessageTooLargeException>(
                () => sender.PublishAsync(envelope, destination));

            Assert.Equal(envelope.MessageId, exception.MessageId);
            Assert.True(exception.ActualSize > exception.MaxSize, "Actual size should exceed max size");
            Assert.Equal(65536, exception.MaxSize); // 64KB in bytes
            Assert.Contains("65,536", exception.Message); // Check for formatted max size
            Assert.Contains("bytes", exception.Message); // Should mention bytes
        }
        finally
        {
            await factory.DisposeAsync();
            await sender.DisposeAsync();
        }
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
        var sender = new StorageQueueSender(factory, options, NullLogger<StorageQueueSender>.Instance);

        // Act & Assert - should not throw
        await sender.DisposeAsync();
        await factory.DisposeAsync();
    }

    /// <summary>
    /// Verifies PublishAsync throws ObjectDisposedException after disposal.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task PublishAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var options = Options.Create(new StorageQueueOptions
        {
            ConnectionString = "UseDevelopmentStorage=true",
            QueueName = "test-queue",
            CreateIfNotExists = false
        });

        var factory = new QueueClientFactory(options, NullLogger<QueueClientFactory>.Instance);
        var sender = new StorageQueueSender(factory, options, NullLogger<StorageQueueSender>.Instance);

        var envelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" });
        var destination = TestData.Address("test-queue", "test-queue");

        await sender.DisposeAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => sender.PublishAsync(envelope, destination));

        await factory.DisposeAsync();
    }
}
