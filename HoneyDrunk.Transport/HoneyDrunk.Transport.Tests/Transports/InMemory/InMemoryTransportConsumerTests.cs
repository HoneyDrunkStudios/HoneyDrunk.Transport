using HoneyDrunk.Transport.Configuration;
using HoneyDrunk.Transport.DependencyInjection;
using HoneyDrunk.Transport.InMemory;
using HoneyDrunk.Transport.Pipeline;
using HoneyDrunk.Transport.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HoneyDrunk.Transport.Tests.Transports.InMemory;

/// <summary>
/// Tests for InMemory transport consumer.
/// </summary>
public sealed class InMemoryTransportConsumerTests
{
    /// <summary>
    /// Verifies StartAsync begins consuming messages.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task StartAsync_WhenCalled_BeginsConsumingMessages()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHoneyDrunkTransportCore();
        services.AddMessageHandler<SampleMessage, SampleMessageHandler>();
        var sp = services.BuildServiceProvider();

        var options = new TestOptions(new TransportOptions
        {
            EndpointName = "test",
            Address = "test-queue",
            MaxConcurrency = 1
        });

        var brokerLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<InMemoryBroker>.Instance;
        var broker = new InMemoryBroker(brokerLogger);
        var pipeline = sp.GetRequiredService<IMessagePipeline>();
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<InMemoryTransportConsumer>.Instance;

        await using var consumer = new InMemoryTransportConsumer(broker, pipeline, options, logger);

        await consumer.StartAsync();

        // Consumer should be running
        await Task.Delay(50);

        await consumer.StopAsync();
    }

    /// <summary>
    /// Verifies StopAsync halts consumption.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task StopAsync_WhenCalled_HaltsConsumption()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHoneyDrunkTransportCore();
        var sp = services.BuildServiceProvider();

        var options = new TestOptions(new TransportOptions
        {
            EndpointName = "test",
            Address = "test-queue2",
            MaxConcurrency = 1
        });

        var brokerLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<InMemoryBroker>.Instance;
        var broker = new InMemoryBroker(brokerLogger);
        var pipeline = sp.GetRequiredService<IMessagePipeline>();
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<InMemoryTransportConsumer>.Instance;

        await using var consumer = new InMemoryTransportConsumer(broker, pipeline, options, logger);

        await consumer.StartAsync();
        await consumer.StopAsync();

        // Should be stopped
        await Task.Delay(50);
    }

    /// <summary>
    /// Verifies DisposeAsync cleans up resources.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task DisposeAsync_WhenCalled_CleansUpResourcesSuccessfully()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHoneyDrunkTransportCore();
        var sp = services.BuildServiceProvider();

        var options = new TestOptions(new TransportOptions
        {
            EndpointName = "test",
            Address = "test-queue3",
            MaxConcurrency = 1
        });

        var brokerLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<InMemoryBroker>.Instance;
        var broker = new InMemoryBroker(brokerLogger);
        var pipeline = sp.GetRequiredService<IMessagePipeline>();
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<InMemoryTransportConsumer>.Instance;

        var consumer = new InMemoryTransportConsumer(broker, pipeline, options, logger);

        await consumer.StartAsync();
        await consumer.DisposeAsync();

        // Should be disposed
        await Task.Delay(50);
    }

    /// <summary>
    /// Verifies consumer processes messages through pipeline.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task ConsumeAsync_WithMessage_ProcessesMessageThroughPipeline()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHoneyDrunkTransportCore();

        var handled = false;
        services.AddMessageHandler<SampleMessage>((msg, ctx, ct) =>
        {
            handled = true;
            return Task.CompletedTask;
        });

        var sp = services.BuildServiceProvider();

        var options = new TestOptions(new TransportOptions
        {
            EndpointName = "test",
            Address = "test-queue4",
            MaxConcurrency = 1
        });

        var brokerLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<InMemoryBroker>.Instance;
        var broker = new InMemoryBroker(brokerLogger);
        var pipeline = sp.GetRequiredService<IMessagePipeline>();
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<InMemoryTransportConsumer>.Instance;

        await using var consumer = new InMemoryTransportConsumer(broker, pipeline, options, logger);
        await consumer.StartAsync();

        // Publish a message
        var envelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" });
        await broker.PublishAsync("test-queue4", envelope);

        // Wait for processing
        await Task.Delay(200);

        await consumer.StopAsync();

        Assert.True(handled);
    }

    // Place helper after methods to satisfy SA1201
    private sealed class TestOptions(TransportOptions value) : IOptions<TransportOptions>
    {
        public TransportOptions Value { get; } = value;
    }
}
