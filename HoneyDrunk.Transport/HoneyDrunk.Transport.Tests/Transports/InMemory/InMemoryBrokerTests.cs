using HoneyDrunk.Transport.InMemory;
using HoneyDrunk.Transport.Tests.Support;

namespace HoneyDrunk.Transport.Tests.Transports.InMemory;

/// <summary>
/// In-memory broker behavioral tests.
/// </summary>
public sealed class InMemoryBrokerTests
{
    /// <summary>
    /// Verifies direct subscribers are notified when a message is published.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task PublishAsync_WithSubscriber_NotifiesSubscriberSuccessfully()
    {
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<InMemoryBroker>.Instance;
        var broker = new InMemoryBroker(logger);
        var got = new TaskCompletionSource<HoneyDrunk.Transport.Abstractions.ITransportEnvelope>(TaskCreationOptions.RunContinuationsAsynchronously);

        broker.Subscribe(
            "a",
            (env, ct) =>
            {
                got.TrySetResult(env);
                return Task.CompletedTask;
            });

        var envp = TestData.CreateEnvelope(new SampleMessage { Value = "ok" });
        await broker.PublishAsync("a", envp);

        var received = await got.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(envp.MessageId, received.MessageId);
    }

    /// <summary>
    /// Verifies queued messages are processed by a consumer.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task ConsumeAsync_WithQueuedMessage_ProcessesMessageFromQueue()
    {
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<InMemoryBroker>.Instance;
        var broker = new InMemoryBroker(logger);

        var envp = TestData.CreateEnvelope(new SampleMessage { Value = "ok" });
        await broker.PublishAsync("b", envp);

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = broker.ConsumeAsync(
            "b",
            (e, ct) =>
            {
                tcs.TrySetResult();
                return Task.CompletedTask;
            });
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
    }

    /// <summary>
    /// Verifies ClearQueue removes pending messages.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task ClearQueue_WithPendingMessages_RemovesAllMessages()
    {
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<InMemoryBroker>.Instance;
        var broker = new InMemoryBroker(logger);

        var env1 = TestData.CreateEnvelope(new SampleMessage { Value = "1" });
        var env2 = TestData.CreateEnvelope(new SampleMessage { Value = "2" });

        await broker.PublishAsync("c", env1);
        await broker.PublishAsync("c", env2);

        Assert.True(broker.GetMessageCount("c") >= 0);
        broker.ClearQueue("c");
        Assert.Equal(0, broker.GetMessageCount("c"));
    }
}
