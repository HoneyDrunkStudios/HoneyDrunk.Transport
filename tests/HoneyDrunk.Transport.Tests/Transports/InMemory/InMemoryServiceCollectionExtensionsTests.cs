using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.InMemory;
using HoneyDrunk.Transport.InMemory.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HoneyDrunk.Transport.Tests.Transports.InMemory;

/// <summary>
/// Tests for in-memory transport service registration helpers.
/// </summary>
public sealed class InMemoryServiceCollectionExtensionsTests
{
    /// <summary>
    /// Endpoint overload configures endpoint metadata and registers in-memory transport services.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task AddHoneyDrunkInMemoryTransport_WithEndpoint_RegistersServicesAndOptions()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var builder = services.AddHoneyDrunkInMemoryTransport(
            "orders-endpoint",
            "orders",
            options => options.MaxConcurrency = 3);

        Assert.Same(builder, builder.WithConsumer());

        await using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<HoneyDrunk.Transport.Configuration.TransportOptions>>().Value;

        Assert.Equal("orders-endpoint", options.EndpointName);
        Assert.Equal("orders", options.Address);
        Assert.Equal(3, options.MaxConcurrency);
        Assert.IsType<InMemoryTransportPublisher>(provider.GetRequiredService<ITransportPublisher>());
        Assert.IsType<InMemoryTransportConsumer>(provider.GetRequiredService<ITransportConsumer>());
        Assert.NotNull(provider.GetRequiredService<ITransportTopology>());
    }
}
