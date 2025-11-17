using Azure.Messaging.ServiceBus;
using HoneyDrunk.Transport.AzureServiceBus;
using HoneyDrunk.Transport.AzureServiceBus.Configuration;
using HoneyDrunk.Transport.Pipeline;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace HoneyDrunk.Transport.Tests.Transports.AzureServiceBus;

/// <summary>
/// Tests for ServiceBusTransportConsumer start/stop and configuration flows.
/// </summary>
public sealed class ServiceBusTransportConsumerTests
{
    /// <summary>
    /// Start on queue with no sessions should create standard processor and start/stop correctly.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task StartAsync_Queue_NoSession_UsesStandardProcessor()
    {
        var asb = new AzureServiceBusOptions
        {
            Address = "orders",
            EntityType = ServiceBusEntityType.Queue,
            SessionEnabled = false,
            AutoComplete = true,
            MaxConcurrency = 2,
            PrefetchCount = 10,
        };
        var options = new TestOptions(asb);

        var client = Substitute.For<ServiceBusClient>();
        var pipeline = Substitute.For<IMessagePipeline>();
        var logger = Substitute.For<ILogger<ServiceBusTransportConsumer>>();

        var processor = Substitute.For<ServiceBusProcessor>();
        client.CreateProcessor("orders", Arg.Any<ServiceBusProcessorOptions>()).Returns(processor);
        processor.StartProcessingAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        processor.StopProcessingAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        await using var consumer = new ServiceBusTransportConsumer(client, pipeline, options, logger);

        await consumer.StartAsync();

        client.Received(1).CreateProcessor("orders", Arg.Any<ServiceBusProcessorOptions>());
        await processor.Received(1).StartProcessingAsync(Arg.Any<CancellationToken>());

        await consumer.StopAsync();

        await processor.Received(1).StopProcessingAsync(Arg.Any<CancellationToken>());

        // DisposeAsync is not virtual on SDK types; cannot verify directly
    }

    /// <summary>
    /// Start on topic without subscription should throw.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task StartAsync_TopicWithoutSubscription_Throws()
    {
        var asb = new AzureServiceBusOptions
        {
            Address = "orders-topic",
            EntityType = ServiceBusEntityType.Topic,
            SessionEnabled = false,
        };
        var options = new TestOptions(asb);

        var client = Substitute.For<ServiceBusClient>();
        var pipeline = Substitute.For<IMessagePipeline>();
        var logger = Substitute.For<ILogger<ServiceBusTransportConsumer>>();

        await using var consumer = new ServiceBusTransportConsumer(client, pipeline, options, logger);

        await Assert.ThrowsAsync<InvalidOperationException>(() => consumer.StartAsync());
    }

    /// <summary>
    /// Start with sessions on queue should create session processor.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task StartAsync_Queue_WithSessions_UsesSessionProcessor()
    {
        var asb = new AzureServiceBusOptions
        {
            Address = "orders",
            EntityType = ServiceBusEntityType.Queue,
            SessionEnabled = true,
            MaxConcurrency = 3,
            PrefetchCount = 5,
        };
        var options = new TestOptions(asb);

        var client = Substitute.For<ServiceBusClient>();
        var pipeline = Substitute.For<IMessagePipeline>();
        var logger = Substitute.For<ILogger<ServiceBusTransportConsumer>>();

        var sessionProcessor = Substitute.For<ServiceBusSessionProcessor>();
        client.CreateSessionProcessor("orders", Arg.Any<ServiceBusSessionProcessorOptions>()).Returns(sessionProcessor);
        sessionProcessor.StartProcessingAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        sessionProcessor.StopProcessingAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        await using var consumer = new ServiceBusTransportConsumer(client, pipeline, options, logger);

        await consumer.StartAsync();

        client.Received(1).CreateSessionProcessor("orders", Arg.Any<ServiceBusSessionProcessorOptions>());
        await sessionProcessor.Received(1).StartProcessingAsync(Arg.Any<CancellationToken>());

        await consumer.StopAsync();

        await sessionProcessor.Received(1).StopProcessingAsync(Arg.Any<CancellationToken>());

        // DisposeAsync is not virtual on SDK types; cannot verify directly
    }

    /// <summary>
    /// StopAsync without starting should be no-op.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task StopAsync_WithoutStart_NoOp()
    {
        var options = new TestOptions(new AzureServiceBusOptions());
        var client = Substitute.For<ServiceBusClient>();
        var pipeline = Substitute.For<IMessagePipeline>();
        var logger = Substitute.For<ILogger<ServiceBusTransportConsumer>>();

        await using var consumer = new ServiceBusTransportConsumer(client, pipeline, options, logger);
        await consumer.StopAsync();

        // No exceptions and nothing to assert; coverage of branch
        Assert.True(true);
    }

    /// <summary>
    /// Starting twice should throw.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task StartAsync_Twice_Throws()
    {
        var asb = new AzureServiceBusOptions
        {
            Address = "orders",
            EntityType = ServiceBusEntityType.Queue,
        };
        var options = new TestOptions(asb);

        var client = Substitute.For<ServiceBusClient>();
        var pipeline = Substitute.For<IMessagePipeline>();
        var logger = Substitute.For<ILogger<ServiceBusTransportConsumer>>();

        var processor = Substitute.For<ServiceBusProcessor>();
        client.CreateProcessor("orders", Arg.Any<ServiceBusProcessorOptions>()).Returns(processor);
        processor.StartProcessingAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        await using var consumer = new ServiceBusTransportConsumer(client, pipeline, options, logger);
        await consumer.StartAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => consumer.StartAsync());
    }

    /// <summary>
    /// Start on topic with subscription uses subscription-based processor overload.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task StartAsync_Topic_WithSubscription_UsesOverload()
    {
        var asb = new AzureServiceBusOptions
        {
            Address = "orders-topic",
            EntityType = ServiceBusEntityType.Topic,
            SubscriptionName = "sub-a",
        };
        var options = new TestOptions(asb);

        var client = Substitute.For<ServiceBusClient>();
        var pipeline = Substitute.For<IMessagePipeline>();
        var logger = Substitute.For<ILogger<ServiceBusTransportConsumer>>();

        var processor = Substitute.For<ServiceBusProcessor>();
        client.CreateProcessor("orders-topic", "sub-a", Arg.Any<ServiceBusProcessorOptions>()).Returns(processor);
        processor.StartProcessingAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        await using var consumer = new ServiceBusTransportConsumer(client, pipeline, options, logger);
        await consumer.StartAsync();

        client.Received(1).CreateProcessor("orders-topic", "sub-a", Arg.Any<ServiceBusProcessorOptions>());
        await processor.Received(1).StartProcessingAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Start on topic with sessions uses subscription-based session processor overload.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task StartAsync_Topic_WithSessions_UsesSessionOverload()
    {
        var asb = new AzureServiceBusOptions
        {
            Address = "orders-topic",
            EntityType = ServiceBusEntityType.Topic,
            SubscriptionName = "sub-a",
            SessionEnabled = true,
        };
        var options = new TestOptions(asb);

        var client = Substitute.For<ServiceBusClient>();
        var pipeline = Substitute.For<IMessagePipeline>();
        var logger = Substitute.For<ILogger<ServiceBusTransportConsumer>>();

        var sessionProcessor = Substitute.For<ServiceBusSessionProcessor>();
        client.CreateSessionProcessor("orders-topic", "sub-a", Arg.Any<ServiceBusSessionProcessorOptions>()).Returns(sessionProcessor);
        sessionProcessor.StartProcessingAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        await using var consumer = new ServiceBusTransportConsumer(client, pipeline, options, logger);
        await consumer.StartAsync();

        client.Received(1).CreateSessionProcessor("orders-topic", "sub-a", Arg.Any<ServiceBusSessionProcessorOptions>());
        await sessionProcessor.Received(1).StartProcessingAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// DisposeAsync can be called multiple times safely (idempotent).
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task DisposeAsync_CalledMultipleTimes_IsIdempotent()
    {
        var asb = new AzureServiceBusOptions
        {
            Address = "orders",
            EntityType = ServiceBusEntityType.Queue,
        };
        var options = new TestOptions(asb);

        var client = Substitute.For<ServiceBusClient>();
        var pipeline = Substitute.For<IMessagePipeline>();
        var logger = Substitute.For<ILogger<ServiceBusTransportConsumer>>();

        var consumer = new ServiceBusTransportConsumer(client, pipeline, options, logger);

        // Dispose multiple times
        await consumer.DisposeAsync();
        await consumer.DisposeAsync();
        await consumer.DisposeAsync();

        // Should not throw
        Assert.True(true);
    }

    private sealed class TestOptions(AzureServiceBusOptions value) : IOptions<AzureServiceBusOptions>
    {
        public AzureServiceBusOptions Value { get; } = value;
    }
}
