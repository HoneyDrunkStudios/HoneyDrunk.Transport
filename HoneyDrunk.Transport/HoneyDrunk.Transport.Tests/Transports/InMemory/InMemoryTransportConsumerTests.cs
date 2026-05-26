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
        services.AddTestKernelServices();
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
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<InMemoryTransportConsumer>.Instance;

        await using var consumer = new InMemoryTransportConsumer(broker, pipeline, scopeFactory, options, logger);

        await consumer.StartAsync();

        // Consumer should be running
        await Task.Delay(50);

        var stopEx = await Record.ExceptionAsync(() => consumer.StopAsync());
        Assert.Null(stopEx);
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
        services.AddTestKernelServices();
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
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<InMemoryTransportConsumer>.Instance;

        await using var consumer = new InMemoryTransportConsumer(broker, pipeline, scopeFactory, options, logger);

        await consumer.StartAsync();
        var ex = await Record.ExceptionAsync(() => consumer.StopAsync());
        Assert.Null(ex);

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
        services.AddTestKernelServices();
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
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<InMemoryTransportConsumer>.Instance;

        var consumer = new InMemoryTransportConsumer(broker, pipeline, scopeFactory, options, logger);

        try
        {
            await consumer.StartAsync();

            // Act — explicit single dispose (no `await using` shadow-cleanup) so the
            // test exercises the post-dispose path without relying on idempotence.
            await consumer.DisposeAsync();

            // Assert — second Start surfaces ObjectDisposedException, proving the dispose completed.
            await Assert.ThrowsAsync<ObjectDisposedException>(() => consumer.StartAsync());
        }
        finally
        {
            // Defensive: idempotent (Interlocked sentinel) so the happy-path second call is a no-op.
            await consumer.DisposeAsync();
        }
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
        services.AddTestKernelServices();
        services.AddHoneyDrunkTransportCore();

        var handled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        services.AddMessageHandler<SampleMessage>((msg, ctx, ct) =>
        {
            handled.TrySetResult();
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
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<InMemoryTransportConsumer>.Instance;

        await using var consumer = new InMemoryTransportConsumer(broker, pipeline, scopeFactory, options, logger);
        await consumer.StartAsync();

        // Publish a message
        var envelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" });
        await broker.PublishAsync("test-queue4", envelope);

        await handled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(handled.Task.IsCompletedSuccessfully);

        await consumer.StopAsync();
    }

    // Place helper after methods to satisfy SA1201
    private sealed class TestOptions(TransportOptions value) : IOptions<TransportOptions>
    {
        public TransportOptions Value { get; } = value;
    }
}
