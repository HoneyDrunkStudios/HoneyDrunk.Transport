using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.AzureServiceBus.DependencyInjection;
using HoneyDrunk.Transport.InMemory.DependencyInjection;
using HoneyDrunk.Transport.StorageQueue.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace HoneyDrunk.Transport.Tests.Transports;

/// <summary>
/// Tests for transport topology capability descriptors.
/// </summary>
public sealed class TopologyTests
{
    /// <summary>
    /// In-memory topology advertises its supported test-friendly capabilities.
    /// </summary>
    [Fact]
    public void InMemoryTopology_WhenResolved_ReportsExpectedCapabilities()
    {
        using var provider = BuildProvider(services => services.AddHoneyDrunkInMemoryTransport());

        var topology = provider.GetRequiredService<ITransportTopology>();

        Assert.Equal("InMemory", topology.Name);
        Assert.True(topology.SupportsTopics);
        Assert.True(topology.SupportsSubscriptions);
        Assert.False(topology.SupportsSessions);
        Assert.True(topology.SupportsOrdering);
        Assert.False(topology.SupportsScheduledMessages);
        Assert.True(topology.SupportsBatching);
        Assert.False(topology.SupportsTransactions);
        Assert.False(topology.SupportsDeadLetterQueue);
        Assert.False(topology.SupportsPriority);
        Assert.Null(topology.MaxMessageSize);
    }

    /// <summary>
    /// Service Bus topology advertises enterprise broker capabilities.
    /// </summary>
    [Fact]
    public void ServiceBusTopology_WhenResolved_ReportsExpectedCapabilities()
    {
        using var provider = BuildProvider(services => services.AddHoneyDrunkServiceBusTransport(
            "Endpoint=sb://example.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=test",
            "orders"));

        var topology = provider.GetRequiredService<ITransportTopology>();

        Assert.Equal("AzureServiceBus", topology.Name);
        Assert.True(topology.SupportsTopics);
        Assert.True(topology.SupportsSubscriptions);
        Assert.True(topology.SupportsSessions);
        Assert.True(topology.SupportsOrdering);
        Assert.True(topology.SupportsScheduledMessages);
        Assert.True(topology.SupportsBatching);
        Assert.True(topology.SupportsTransactions);
        Assert.True(topology.SupportsDeadLetterQueue);
        Assert.False(topology.SupportsPriority);
        Assert.Equal(256 * 1024, topology.MaxMessageSize);
    }

    /// <summary>
    /// Storage Queue topology advertises simple queue capabilities and limits.
    /// </summary>
    [Fact]
    public void StorageQueueTopology_WhenResolved_ReportsExpectedCapabilities()
    {
        using var provider = BuildProvider(services => services.AddHoneyDrunkTransportStorageQueue(
            "UseDevelopmentStorage=true",
            "orders"));

        var topology = provider.GetRequiredService<ITransportTopology>();

        Assert.Equal("StorageQueue", topology.Name);
        Assert.False(topology.SupportsTopics);
        Assert.False(topology.SupportsSubscriptions);
        Assert.False(topology.SupportsSessions);
        Assert.False(topology.SupportsOrdering);
        Assert.False(topology.SupportsScheduledMessages);
        Assert.True(topology.SupportsBatching);
        Assert.False(topology.SupportsTransactions);
        Assert.False(topology.SupportsDeadLetterQueue);
        Assert.False(topology.SupportsPriority);
        Assert.Equal(64 * 1024, topology.MaxMessageSize);
    }

    private static ServiceProvider BuildProvider(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        configure(services);
        return services.BuildServiceProvider();
    }
}
