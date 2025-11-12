using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.DependencyInjection;
using HoneyDrunk.Transport.StorageQueue.Configuration;
using HoneyDrunk.Transport.StorageQueue.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HoneyDrunk.Transport.StorageQueue.DependencyInjection;

/// <summary>
/// Extension methods for registering Azure Storage Queue transport services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the HoneyDrunk Azure Storage Queue transport implementation.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configuration action for Storage Queue options.</param>
    /// <returns>A transport builder for fluent configuration.</returns>
    public static ITransportBuilder AddHoneyDrunkTransportStorageQueue(
        this IServiceCollection services,
        Action<StorageQueueOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        // Ensure core transport services are registered
        var builder = services.AddHoneyDrunkTransportCore();

        // Configure options
        services.Configure(configure);

        // Validate options on startup
        services.AddOptions<StorageQueueOptions>()
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Register queue client factory
        services.TryAddSingleton<QueueClientFactory>();

        // Register publisher and consumer
        services.TryAddSingleton<ITransportPublisher, StorageQueueSender>();
        services.TryAddSingleton<ITransportConsumer, StorageQueueProcessor>();

        return builder;
    }

    /// <summary>
    /// Adds Azure Storage Queue transport with connection string and queue name.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">The Azure Storage connection string.</param>
    /// <param name="queueName">The queue name.</param>
    /// <param name="configure">Optional additional configuration.</param>
    /// <returns>A transport builder for fluent configuration.</returns>
    public static ITransportBuilder AddHoneyDrunkTransportStorageQueue(
        this IServiceCollection services,
        string connectionString,
        string queueName,
        Action<StorageQueueOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);

        return services.AddHoneyDrunkTransportStorageQueue(options =>
        {
            options.ConnectionString = connectionString;
            options.QueueName = queueName;
            options.Address = queueName;
            options.EndpointName = queueName;
            configure?.Invoke(options);
        });
    }

    /// <summary>
    /// Configures the Storage Queue transport to use a specific poison queue name.
    /// </summary>
    /// <param name="builder">The transport builder.</param>
    /// <param name="poisonQueueName">The poison queue name.</param>
    /// <returns>The transport builder for fluent configuration.</returns>
    public static ITransportBuilder WithPoisonQueue(
        this ITransportBuilder builder,
        string poisonQueueName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(poisonQueueName);

        builder.Services.Configure<StorageQueueOptions>(options =>
        {
            options.PoisonQueueName = poisonQueueName;
        });

        return builder;
    }

    /// <summary>
    /// Configures the maximum dequeue count before messages are moved to poison queue.
    /// </summary>
    /// <param name="builder">The transport builder.</param>
    /// <param name="maxDequeueCount">The maximum dequeue count.</param>
    /// <returns>The transport builder for fluent configuration.</returns>
    public static ITransportBuilder WithMaxDequeueCount(
        this ITransportBuilder builder,
        int maxDequeueCount)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (maxDequeueCount < 1 || maxDequeueCount > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDequeueCount), "MaxDequeueCount must be between 1 and 100");
        }

        builder.Services.Configure<StorageQueueOptions>(options =>
        {
            options.MaxDequeueCount = maxDequeueCount;
        });

        return builder;
    }

    /// <summary>
    /// Configures the visibility timeout for received messages.
    /// </summary>
    /// <param name="builder">The transport builder.</param>
    /// <param name="visibilityTimeout">The visibility timeout duration.</param>
    /// <returns>The transport builder for fluent configuration.</returns>
    public static ITransportBuilder WithVisibilityTimeout(
        this ITransportBuilder builder,
        TimeSpan visibilityTimeout)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (visibilityTimeout <= TimeSpan.Zero || visibilityTimeout > TimeSpan.FromDays(7))
        {
            throw new ArgumentOutOfRangeException(nameof(visibilityTimeout), "VisibilityTimeout must be between 1 second and 7 days");
        }

        builder.Services.Configure<StorageQueueOptions>(options =>
        {
            options.VisibilityTimeout = visibilityTimeout;
        });

        return builder;
    }

    /// <summary>
    /// Configures the message time-to-live.
    /// </summary>
    /// <param name="builder">The transport builder.</param>
    /// <param name="timeToLive">The message time-to-live duration.</param>
    /// <returns>The transport builder for fluent configuration.</returns>
    public static ITransportBuilder WithMessageTimeToLive(
        this ITransportBuilder builder,
        TimeSpan timeToLive)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (timeToLive <= TimeSpan.Zero || timeToLive > TimeSpan.FromDays(7))
        {
            throw new ArgumentOutOfRangeException(nameof(timeToLive), "MessageTimeToLive must be between 1 second and 7 days");
        }

        builder.Services.Configure<StorageQueueOptions>(options =>
        {
            options.MessageTimeToLive = timeToLive;
        });

        return builder;
    }

    /// <summary>
    /// Configures the prefetch message count for batch retrieval.
    /// </summary>
    /// <param name="builder">The transport builder.</param>
    /// <param name="prefetchCount">The number of messages to prefetch (1-32).</param>
    /// <returns>The transport builder for fluent configuration.</returns>
    public static ITransportBuilder WithPrefetchCount(
        this ITransportBuilder builder,
        int prefetchCount)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (prefetchCount < 1 || prefetchCount > 32)
        {
            throw new ArgumentOutOfRangeException(nameof(prefetchCount), "PrefetchCount must be between 1 and 32");
        }

        builder.Services.Configure<StorageQueueOptions>(options =>
        {
            options.PrefetchMaxMessages = prefetchCount;
        });

        return builder;
    }

    /// <summary>
    /// Configures the maximum number of concurrent message processors.
    /// </summary>
    /// <param name="builder">The transport builder.</param>
    /// <param name="maxConcurrency">The maximum concurrency level.</param>
    /// <returns>The transport builder for fluent configuration.</returns>
    public static ITransportBuilder WithConcurrency(
        this ITransportBuilder builder,
        int maxConcurrency)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (maxConcurrency < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxConcurrency), "MaxConcurrency must be at least 1");
        }

        builder.Services.Configure<StorageQueueOptions>(options =>
        {
            options.MaxConcurrency = maxConcurrency;
        });

        return builder;
    }

    /// <summary>
    /// Disables base64 encoding for message payloads.
    /// </summary>
    /// <param name="builder">The transport builder.</param>
    /// <returns>The transport builder for fluent configuration.</returns>
    /// <remarks>
    /// Use with caution - disabling base64 encoding may cause issues with binary payloads.
    /// </remarks>
    public static ITransportBuilder WithoutBase64Encoding(this ITransportBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.Configure<StorageQueueOptions>(options =>
        {
            options.Base64EncodePayload = false;
        });

        return builder;
    }

    /// <summary>
    /// Disables automatic queue creation.
    /// </summary>
    /// <param name="builder">The transport builder.</param>
    /// <returns>The transport builder for fluent configuration.</returns>
    public static ITransportBuilder WithoutAutoCreateQueue(this ITransportBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.Configure<StorageQueueOptions>(options =>
        {
            options.CreateIfNotExists = false;
        });

        return builder;
    }
}
