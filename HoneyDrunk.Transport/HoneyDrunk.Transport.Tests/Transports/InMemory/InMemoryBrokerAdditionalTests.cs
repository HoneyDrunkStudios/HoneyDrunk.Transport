using HoneyDrunk.Transport.InMemory;
using HoneyDrunk.Transport.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace HoneyDrunk.Transport.Tests.Transports.InMemory;

/// <summary>
/// Additional tests for InMemoryBroker to improve coverage.
/// </summary>
public sealed class InMemoryBrokerAdditionalTests
{
    /// <summary>
    /// Verifies Subscribe adds handler and PublishAsync invokes it.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Subscribe_ThenPublish_InvokesSubscribedHandler()
    {
        // Arrange
        var broker = new InMemoryBroker(NullLogger<InMemoryBroker>.Instance);
        var receivedEnvelopes = new List<string>();

        broker.Subscribe("test-address", (envelope, ct) =>
        {
            receivedEnvelopes.Add(envelope.MessageId);
            return Task.CompletedTask;
        });

        var message = new SampleMessage { Value = "test" };
        var envelope = TestData.CreateEnvelope(message);

        // Act
        await broker.PublishAsync("test-address", envelope);
        await Task.Delay(100); // Allow async handler to execute

        // Assert
        Assert.Single(receivedEnvelopes);
        Assert.Equal(envelope.MessageId, receivedEnvelopes[0]);
    }

    /// <summary>
    /// Verifies GetMessageCount returns correct count of pending messages.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetMessageCount_WithQueuedMessages_ReturnsCorrectCount()
    {
        // Arrange
        var broker = new InMemoryBroker(NullLogger<InMemoryBroker>.Instance);
        var message1 = new SampleMessage { Value = "msg1" };
        var message2 = new SampleMessage { Value = "msg2" };
        var message3 = new SampleMessage { Value = "msg3" };

        // Act - publish without consumer
        await broker.PublishAsync("test-queue", TestData.CreateEnvelope(message1));
        await broker.PublishAsync("test-queue", TestData.CreateEnvelope(message2));
        await broker.PublishAsync("test-queue", TestData.CreateEnvelope(message3));

        var count = broker.GetMessageCount("test-queue");

        // Assert
        Assert.Equal(3, count);
    }

    /// <summary>
    /// Verifies GetMessageCount returns zero for non-existent address.
    /// </summary>
    [Fact]
    public void GetMessageCount_WithNonExistentAddress_ReturnsZero()
    {
        // Arrange
        var broker = new InMemoryBroker(NullLogger<InMemoryBroker>.Instance);

        // Act
        var count = broker.GetMessageCount("non-existent-queue");

        // Assert
        Assert.Equal(0, count);
    }

    /// <summary>
    /// Verifies ClearQueue drains all pending messages.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task ClearQueue_WithQueuedMessages_DrainsAllMessages()
    {
        // Arrange
        var broker = new InMemoryBroker(NullLogger<InMemoryBroker>.Instance);

        await broker.PublishAsync("test-queue", TestData.CreateEnvelope(new SampleMessage { Value = "msg1" }));
        await broker.PublishAsync("test-queue", TestData.CreateEnvelope(new SampleMessage { Value = "msg2" }));
        await broker.PublishAsync("test-queue", TestData.CreateEnvelope(new SampleMessage { Value = "msg3" }));

        Assert.Equal(3, broker.GetMessageCount("test-queue"));

        // Act
        broker.ClearQueue("test-queue");

        // Assert
        Assert.Equal(0, broker.GetMessageCount("test-queue"));
    }

    /// <summary>
    /// Verifies ClearQueue on non-existent address does not throw.
    /// </summary>
    [Fact]
    public void ClearQueue_WithNonExistentAddress_DoesNotThrow()
    {
        // Arrange
        var broker = new InMemoryBroker(NullLogger<InMemoryBroker>.Instance);

        // Act & Assert - should not throw
        broker.ClearQueue("non-existent-queue");
    }

    /// <summary>
    /// Verifies multiple subscribers all receive the published message.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Subscribe_WithMultipleSubscribers_AllReceiveMessage()
    {
        // Arrange
        var broker = new InMemoryBroker(NullLogger<InMemoryBroker>.Instance);
        var received1 = false;
        var received2 = false;
        var received3 = false;

        broker.Subscribe("test-address", (envelope, ct) =>
        {
            received1 = true;
            return Task.CompletedTask;
        });

        broker.Subscribe("test-address", (envelope, ct) =>
        {
            received2 = true;
            return Task.CompletedTask;
        });

        broker.Subscribe("test-address", (envelope, ct) =>
        {
            received3 = true;
            return Task.CompletedTask;
        });

        var message = new SampleMessage { Value = "test" };
        var envelope = TestData.CreateEnvelope(message);

        // Act
        await broker.PublishAsync("test-address", envelope);
        await Task.Delay(100); // Allow async handlers to execute

        // Assert
        Assert.True(received1);
        Assert.True(received2);
        Assert.True(received3);
    }

    /// <summary>
    /// Verifies ConsumeAsync processes messages from the queue.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task ConsumeAsync_WithQueuedMessages_ProcessesMessages()
    {
        // Arrange
        var broker = new InMemoryBroker(NullLogger<InMemoryBroker>.Instance);
        var receivedMessages = new List<string>();

        // Queue messages first
        await broker.PublishAsync("test-queue", TestData.CreateEnvelope(new SampleMessage { Value = "msg1" }));
        await broker.PublishAsync("test-queue", TestData.CreateEnvelope(new SampleMessage { Value = "msg2" }));

        // Act - start consuming
        using var cts = new CancellationTokenSource();
        var consumeTask = Task.Run(
            async () =>
            {
                await broker.ConsumeAsync(
                    "test-queue",
                    (envelope, ct) =>
                    {
                        receivedMessages.Add(envelope.MessageId);
                        if (receivedMessages.Count >= 2)
                        {
                            cts.Cancel(); // Stop after processing 2 messages
                        }

                        return Task.CompletedTask;
                    },
                    cts.Token);
            });

        // Wait for messages to be processed or timeout
        await Task.WhenAny(consumeTask, Task.Delay(2000));
        cts.Cancel();

        // Assert
        Assert.Equal(2, receivedMessages.Count);
    }

    /// <summary>
    /// Verifies subscriber error handling does not break the broker.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Subscribe_WhenHandlerThrows_DoesNotBreakBroker()
    {
        // Arrange
        var broker = new InMemoryBroker(NullLogger<InMemoryBroker>.Instance);
        var received2 = false;

        // First subscriber throws
        broker.Subscribe("test-address", (envelope, ct) =>
        {
            throw new InvalidOperationException("Subscriber error");
        });

        // Second subscriber should still receive message
        broker.Subscribe("test-address", (envelope, ct) =>
        {
            received2 = true;
            return Task.CompletedTask;
        });

        var message = new SampleMessage { Value = "test" };
        var envelope = TestData.CreateEnvelope(message);

        // Act
        await broker.PublishAsync("test-address", envelope);
        await Task.Delay(100); // Allow async handlers to execute

        // Assert - second subscriber should have received message despite first one throwing
        Assert.True(received2);
    }
}
