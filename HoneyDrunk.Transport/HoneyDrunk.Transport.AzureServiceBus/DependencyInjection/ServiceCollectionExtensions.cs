using Azure.Messaging.ServiceBus;
using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.AzureServiceBus.Configuration;
using HoneyDrunk.Transport.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using AzureSBRetryOptions = Azure.Messaging.ServiceBus.ServiceBusRetryOptions;

namespace HoneyDrunk.Transport.AzureServiceBus.DependencyInjection;

/// <summary>
/// Extension methods for registering Azure Service Bus transport services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the HoneyDrunk Azure Service Bus transport implementation.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configuration action for Azure Service Bus options.</param>
    /// <returns>A transport builder for additional configuration.</returns>
    public static ITransportBuilder AddHoneyDrunkServiceBusTransport(
        this IServiceCollection services,
        Action<AzureServiceBusOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        // Ensure core transport services are registered
        var builder = services.AddHoneyDrunkTransportCore();

        // Configure options
        services.Configure(configure);

        // Register Service Bus client
        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AzureServiceBusOptions>>().Value;

            var clientOptions = new ServiceBusClientOptions
            {
                RetryOptions = new AzureSBRetryOptions
                {
                    Mode = options.ServiceBusRetry.Mode == Configuration.ServiceBusRetryMode.Exponential
                        ? Azure.Messaging.ServiceBus.ServiceBusRetryMode.Exponential
                        : Azure.Messaging.ServiceBus.ServiceBusRetryMode.Fixed,
                    MaxRetries = options.ServiceBusRetry.MaxRetries,
                    Delay = options.ServiceBusRetry.Delay,
                    MaxDelay = options.ServiceBusRetry.MaxDelay,
                    TryTimeout = options.ServiceBusRetry.TryTimeout
                }
            };

            if (!string.IsNullOrEmpty(options.ConnectionString))
            {
                return new ServiceBusClient(options.ConnectionString, clientOptions);
            }
            else if (!string.IsNullOrEmpty(options.FullyQualifiedNamespace))
            {
                // Use managed identity (DefaultAzureCredential is reused as singleton)
                var credential = sp.GetRequiredService<Azure.Core.TokenCredential>();
                return new ServiceBusClient(
                    options.FullyQualifiedNamespace,
                    credential,
                    clientOptions);
            }
            else
            {
                throw new InvalidOperationException(
                    "Either ConnectionString or FullyQualifiedNamespace must be configured");
            }
        });

        // Register DefaultAzureCredential as singleton for reuse across services
        services.TryAddSingleton<Azure.Core.TokenCredential>(sp => new Azure.Identity.DefaultAzureCredential());

        // Register publisher and consumer
        services.TryAddSingleton<ITransportPublisher, ServiceBusTransportPublisher>();
        services.TryAddSingleton<ITransportConsumer, ServiceBusTransportConsumer>();

        // Register topology
        services.TryAddSingleton<ITransportTopology, ServiceBusTopology>();

        // Register default blob fallback store (internal) for DI resolution
        services.TryAddSingleton<Internal.IBlobFallbackStore, Internal.DefaultBlobFallbackStore>();

        return builder;
    }

    /// <summary>
    /// Adds Azure Service Bus transport with connection string.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">The Azure Service Bus connection string.</param>
    /// <param name="queueOrTopicName">The queue or topic name.</param>
    /// <param name="configure">Optional configuration action for additional settings.</param>
    /// <returns>A transport builder for additional configuration.</returns>
    public static ITransportBuilder AddHoneyDrunkServiceBusTransport(
        this IServiceCollection services,
        string connectionString,
        string queueOrTopicName,
        Action<AzureServiceBusOptions>? configure = null)
    {
        return services.AddHoneyDrunkServiceBusTransport(options =>
        {
            options.ConnectionString = connectionString;
            options.Address = queueOrTopicName;
            options.EndpointName = queueOrTopicName;
            configure?.Invoke(options);
        });
    }

    /// <summary>
    /// Adds Azure Service Bus transport with managed identity.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="fullyQualifiedNamespace">The fully qualified Service Bus namespace.</param>
    /// <param name="queueOrTopicName">The queue or topic name.</param>
    /// <param name="configure">Optional configuration action for additional settings.</param>
    /// <returns>A transport builder for additional configuration.</returns>
    public static ITransportBuilder AddHoneyDrunkServiceBusTransportWithManagedIdentity(
        this IServiceCollection services,
        string fullyQualifiedNamespace,
        string queueOrTopicName,
        Action<AzureServiceBusOptions>? configure = null)
    {
        return services.AddHoneyDrunkServiceBusTransport(options =>
        {
            options.FullyQualifiedNamespace = fullyQualifiedNamespace;
            options.Address = queueOrTopicName;
            options.EndpointName = queueOrTopicName;
            configure?.Invoke(options);
        });
    }

    /// <summary>
    /// Configures the transport for a topic with subscription.
    /// </summary>
    /// <param name="builder">The transport builder.</param>
    /// <param name="subscriptionName">The subscription name.</param>
    /// <returns>The configured transport builder.</returns>
    public static ITransportBuilder WithTopicSubscription(
        this ITransportBuilder builder,
        string subscriptionName)
    {
        builder.Services.Configure<AzureServiceBusOptions>(options =>
        {
            options.EntityType = ServiceBusEntityType.Topic;
            options.SubscriptionName = subscriptionName;
        });

        return builder;
    }

    /// <summary>
    /// Enables session support for ordered message processing.
    /// </summary>
    /// <param name="builder">The transport builder.</param>
    /// <returns>The configured transport builder.</returns>
    public static ITransportBuilder WithSessions(this ITransportBuilder builder)
    {
        builder.Services.Configure<AzureServiceBusOptions>(options =>
        {
            options.SessionEnabled = true;
        });

        return builder;
    }

    /// <summary>
    /// Configures retry behavior for the transport.
    /// </summary>
    /// <param name="builder">The transport builder.</param>
    /// <param name="configure">Configuration action for retry options.</param>
    /// <returns>The configured transport builder.</returns>
    public static ITransportBuilder WithRetry(
        this ITransportBuilder builder,
        Action<Configuration.ServiceBusRetryOptions> configure)
    {
        builder.Services.Configure<AzureServiceBusOptions>(options =>
        {
            configure(options.ServiceBusRetry);
        });

        return builder;
    }

    /// <summary>
    /// Enables Blob Storage fallback for publish failures and applies configuration.
    /// </summary>
    /// <param name="builder">The transport builder to configure.</param>
    /// <param name="configure">Action that configures Blob fallback options.</param>
    /// <returns>The configured transport builder.</returns>
    public static ITransportBuilder WithBlobFallback(
        this ITransportBuilder builder,
        Action<BlobFallbackOptions> configure)
    {
        builder.Services.Configure<AzureServiceBusOptions>(options =>
        {
            configure(options.BlobFallback);
            options.BlobFallback.Enabled = true;
        });

        return builder;
    }
}
