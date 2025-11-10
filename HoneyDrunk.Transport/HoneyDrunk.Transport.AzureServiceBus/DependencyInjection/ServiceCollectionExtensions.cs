using Azure.Messaging.ServiceBus;
using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.AzureServiceBus.Configuration;
using HoneyDrunk.Transport.DependencyInjection;
using Microsoft.Extensions.Azure;
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
        services.AddSingleton<ServiceBusClient>(sp =>
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
                // Use managed identity (DefaultAzureCredential)
                return new ServiceBusClient(
                    options.FullyQualifiedNamespace,
                    new Azure.Identity.DefaultAzureCredential(),
                    clientOptions);
            }
            else
            {
                throw new InvalidOperationException(
                    "Either ConnectionString or FullyQualifiedNamespace must be configured");
            }
        });

        // Register publisher and consumer
        services.TryAddSingleton<ITransportPublisher, ServiceBusTransportPublisher>();
        services.TryAddSingleton<ITransportConsumer, ServiceBusTransportConsumer>();

        return builder;
    }

    /// <summary>
    /// Adds Azure Service Bus transport with connection string.
    /// </summary>
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
}
