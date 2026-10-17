using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.Configuration;
using HoneyDrunk.Transport.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HoneyDrunk.Transport.InMemory.DependencyInjection;

/// <summary>
/// Extension methods for registering in-memory transport services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the HoneyDrunk in-memory transport implementation.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration action for transport options.</param>
    /// <returns>A transport builder for additional configuration.</returns>
    public static ITransportBuilder AddHoneyDrunkInMemoryTransport(
        this IServiceCollection services,
        Action<TransportOptions>? configure = null)
    {
        // Ensure core transport services are registered
        var builder = services.AddHoneyDrunkTransportCore();

        // Register the in-memory broker as a singleton
        services.TryAddSingleton<InMemoryBroker>();

        // Register publisher and consumer
        services.TryAddSingleton<ITransportPublisher, InMemoryTransportPublisher>();
        services.TryAddSingleton<ITransportConsumer, InMemoryTransportConsumer>();

        // Register topology
        services.TryAddSingleton<ITransportTopology, InMemoryTopology>();

        // Configure transport options
        if (configure != null)
        {
            services.Configure(configure);
        }

        return builder;
    }

    /// <summary>
    /// Adds the HoneyDrunk in-memory transport with specific endpoint configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="endpointName">The endpoint name.</param>
    /// <param name="address">The endpoint address.</param>
    /// <param name="configure">Optional configuration action for additional settings.</param>
    /// <returns>A transport builder for additional configuration.</returns>
    public static ITransportBuilder AddHoneyDrunkInMemoryTransport(
        this IServiceCollection services,
        string endpointName,
        string address,
        Action<TransportOptions>? configure = null)
    {
        return services.AddHoneyDrunkInMemoryTransport(options =>
        {
            options.EndpointName = endpointName;
            options.Address = address;
            configure?.Invoke(options);
        });
    }

    /// <summary>
    /// Extension to start the in-memory consumer from the builder.
    /// </summary>
    /// <param name="builder">The transport builder.</param>
    /// <returns>The transport builder for additional configuration.</returns>
    public static ITransportBuilder WithConsumer(this ITransportBuilder builder)
    {
        // Consumer is already registered; this is for fluent API completeness
        return builder;
    }
}
