using HoneyDrunk.Transport.InMemory;
using HoneyDrunk.Transport.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HoneyDrunk.Transport.Tests.Transports.InMemory;

/// <summary>
/// Tests end-to-end publishing and consuming using the in-memory transport.
/// </summary>
public sealed class InMemoryTransportTests
{
    /// <summary>
    /// Verifies publishing and consuming a single message completes without errors.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task PublishAsync_WithConsumer_ProcessesMessageSuccessfully()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Debug));
        HoneyDrunk.Transport.DependencyInjection.ServiceCollectionExtensions.AddHoneyDrunkTransportCore(services);
        HoneyDrunk.Transport.DependencyInjection.ServiceCollectionExtensions.AddMessageHandler<SampleMessage, SampleMessageHandler>(services);
        services.AddSingleton<InMemoryBroker>();
        services.AddSingleton<InMemoryTransportPublisher>();
        services.AddSingleton<InMemoryTransportConsumer>();
        services.Configure<HoneyDrunk.Transport.Configuration.TransportOptions>(o =>
        {
            o.EndpointName = "test";
            o.Address = "addr";
            o.MaxConcurrency = 1;
        });
        var sp = services.BuildServiceProvider();

        var publisher = sp.GetRequiredService<InMemoryTransportPublisher>();
        var consumer = sp.GetRequiredService<InMemoryTransportConsumer>();
        await consumer.StartAsync();

        var envelope = TestData.CreateEnvelope(new SampleMessage { Value = "ok" });
        await publisher.PublishAsync(envelope, TestData.Address("test", "addr"));

        // Give consumer time to process
        await Task.Delay(100);

        await consumer.StopAsync();
    }
}
