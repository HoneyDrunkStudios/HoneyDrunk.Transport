using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.Configuration;
using HoneyDrunk.Transport.InMemory;
using HoneyDrunk.Transport.Pipeline;
using HoneyDrunk.Transport.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace HoneyDrunk.Transport.Tests.Transports.InMemory;

/// <summary>
/// Additional lifecycle coverage for in-memory transport publisher and consumer.
/// </summary>
public sealed class InMemoryTransportLifecycleAdditionalTests
{
    /// <summary>
    /// Publisher validates required envelope and destination arguments.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task PublishAsync_WhenArgumentsMissing_ThrowsArgumentNullException()
    {
        var publisher = CreatePublisher();
        var envelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" });
        var destination = TestData.Address("orders", "orders");

        await Assert.ThrowsAsync<ArgumentNullException>(() => publisher.PublishAsync(null!, destination));
        await Assert.ThrowsAsync<ArgumentNullException>(() => publisher.PublishAsync(envelope, null!));
    }

    /// <summary>
    /// Batch publishing publishes each envelope to the broker.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task PublishBatchAsync_WhenMultipleEnvelopes_PublishesEachEnvelope()
    {
        var broker = new InMemoryBroker(NullLogger<InMemoryBroker>.Instance);
        var publisher = CreatePublisher(broker);
        var destination = TestData.Address("orders", "orders");
        var first = TestData.CreateEnvelope(new SampleMessage { Value = "one" });
        var second = TestData.CreateEnvelope(new SampleMessage { Value = "two" });

        await publisher.PublishBatchAsync([first, second], destination);

        Assert.Equal(2, broker.GetMessageCount("orders"));
    }

    /// <summary>
    /// Consumer start is guarded against duplicate starts.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task StartAsync_WhenAlreadyStarted_ThrowsInvalidOperationException()
    {
        await using var fixture = CreateConsumer();

        await fixture.Consumer.StartAsync();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Consumer.StartAsync());

        Assert.Equal("Consumer is already started", ex.Message);
    }

    /// <summary>
    /// Stop is safe before start and after start.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task StopAsync_WhenCalledBeforeAndAfterStart_IsIdempotent()
    {
        await using var fixture = CreateConsumer();

        var ex = await Record.ExceptionAsync(async () =>
        {
            await fixture.Consumer.StopAsync();
            await fixture.Consumer.StartAsync();
            await fixture.Consumer.StopAsync();
            await fixture.Consumer.StopAsync();
        });
        Assert.Null(ex);
    }

    /// <summary>
    /// Retry processing result republishes the message to the same address.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task Consumer_WhenPipelineRequestsRetry_RepublishesMessage()
    {
        var broker = new InMemoryBroker(NullLogger<InMemoryBroker>.Instance);
        var pipeline = Substitute.For<IMessagePipeline>();
        var processed = 0;
        pipeline.ProcessAsync(
                Arg.Any<ITransportEnvelope>(),
                Arg.Any<MessageContext>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                var current = Interlocked.Increment(ref processed);
                return current == 1
                    ? MessageProcessingResult.Retry
                    : MessageProcessingResult.Success;
            });
        await using var fixture = CreateConsumer(broker, pipeline);
        var publisher = CreatePublisher(broker);
        var destination = TestData.Address("orders", "orders");

        await fixture.Consumer.StartAsync();
        await publisher.PublishAsync(TestData.CreateEnvelope(new SampleMessage { Value = "retry" }), destination);
        await WaitUntilAsync(() => Volatile.Read(ref processed) >= 2);

        await pipeline.Received(2).ProcessAsync(
            Arg.Any<ITransportEnvelope>(),
            Arg.Any<MessageContext>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Disposed consumers reject subsequent starts.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task StartAsync_WhenDisposed_ThrowsObjectDisposedException()
    {
        await using var fixture = CreateConsumer();
        await fixture.Consumer.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => fixture.Consumer.StartAsync());
    }

    /// <summary>
    /// Creates an in-memory publisher.
    /// </summary>
    /// <param name="broker">Optional broker instance.</param>
    /// <returns>An in-memory publisher.</returns>
    private static InMemoryTransportPublisher CreatePublisher(InMemoryBroker? broker = null)
    {
        return new InMemoryTransportPublisher(
            broker ?? new InMemoryBroker(NullLogger<InMemoryBroker>.Instance),
            NullLogger<InMemoryTransportPublisher>.Instance);
    }

    /// <summary>
    /// Creates an in-memory consumer fixture.
    /// </summary>
    /// <param name="broker">Optional broker instance.</param>
    /// <param name="pipeline">Optional message pipeline.</param>
    /// <returns>An in-memory consumer fixture.</returns>
    private static ConsumerFixture CreateConsumer(
        InMemoryBroker? broker = null,
        IMessagePipeline? pipeline = null)
    {
        var provider = new ServiceCollection().BuildServiceProvider();
        var consumer = new InMemoryTransportConsumer(
            broker ?? new InMemoryBroker(NullLogger<InMemoryBroker>.Instance),
            pipeline ?? Substitute.For<IMessagePipeline>(),
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new TransportOptions { Address = "orders", EndpointName = "orders" }),
            NullLogger<InMemoryTransportConsumer>.Instance);

        return new ConsumerFixture(consumer, provider);
    }

    /// <summary>
    /// Waits until the supplied condition becomes true.
    /// </summary>
    /// <param name="condition">The condition to observe.</param>
    /// <returns>A task representing the asynchronous wait.</returns>
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException("Condition was not met before the timeout elapsed.");
            }

            await Task.Delay(25);
        }
    }

    private sealed class ConsumerFixture : IAsyncDisposable
    {
        public ConsumerFixture(InMemoryTransportConsumer consumer, ServiceProvider provider)
        {
            Consumer = consumer;
            Provider = provider;
        }

        public InMemoryTransportConsumer Consumer { get; }

        private ServiceProvider Provider { get; }

        public async ValueTask DisposeAsync()
        {
            await Consumer.DisposeAsync();
            await Provider.DisposeAsync();
        }
    }
}
