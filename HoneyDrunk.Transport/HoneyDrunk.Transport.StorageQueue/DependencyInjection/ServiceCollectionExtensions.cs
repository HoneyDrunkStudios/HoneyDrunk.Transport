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
/// <remarks>
/// <para>
/// <strong>Single Transport Per Application:</strong> Only ONE transport implementation
/// (Storage Queue, Service Bus, InMemory) should be registered per application. The library
/// uses TryAddSingleton for ITransportPublisher and ITransportConsumer, meaning the FIRST
/// registration wins and subsequent registrations are silently ignored.
/// </para>
/// <para>
/// <strong>Correct Usage:</strong>
/// </para>
/// <code>
/// // ? CORRECT: Single transport
/// services.AddHoneyDrunkTransportCore()
///     .AddHoneyDrunkTransportStorageQueue(...);
///
/// // ? WRONG: Multiple transports - second is ignored!
/// services.AddHoneyDrunkTransportCore()
///     .AddHoneyDrunkServiceBusTransport(...)  // ? This registers
///     .AddHoneyDrunkTransportStorageQueue(...); // ? Silently ignored!
///
/// // ? CORRECT: Test override
/// #if DEBUG
/// services.AddHoneyDrunkTransportCore()
///     .AddHoneyDrunkInMemoryTransport();  // ? Overrides for testing
/// #else
/// services.AddHoneyDrunkTransportCore()
///     .AddHoneyDrunkTransportStorageQueue(...);
/// #endif
/// </code>
/// <para>
/// To use multiple message brokers simultaneously, deploy separate application instances,
/// each with its own transport registration.
/// </para>
/// </remarks>
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

        // Register poison queue mover
        services.TryAddSingleton<PoisonQueueMover>();

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

        if (maxConcurrency < 1 || maxConcurrency > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(maxConcurrency), "MaxConcurrency must be between 1 and 100");
        }

        builder.Services.Configure<StorageQueueOptions>(options =>
        {
            options.MaxConcurrency = maxConcurrency;
        });

        return builder;
    }

    /// <summary>
    /// Configures the maximum concurrent operations for batch publishing.
    /// </summary>
    /// <param name="builder">The transport builder.</param>
    /// <param name="maxBatchConcurrency">The maximum batch publish concurrency.</param>
    /// <returns>The transport builder for fluent configuration.</returns>
    /// <remarks>
    /// Default is Min(ProcessorCount * 2, 32). Increase cautiously to avoid Azure Storage rate limits.
    /// </remarks>
    public static ITransportBuilder WithBatchPublishConcurrency(
        this ITransportBuilder builder,
        int maxBatchConcurrency)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (maxBatchConcurrency < 1 || maxBatchConcurrency > 128)
        {
            throw new ArgumentOutOfRangeException(nameof(maxBatchConcurrency), "MaxBatchPublishConcurrency must be between 1 and 128");
        }

        builder.Services.Configure<StorageQueueOptions>(options =>
        {
            options.MaxBatchPublishConcurrency = maxBatchConcurrency;
        });

        return builder;
    }

    /// <summary>
    /// Configures concurrent message processing within each consumer's batch.
    /// </summary>
    /// <param name="builder">The transport builder.</param>
    /// <param name="batchConcurrency">The number of messages to process concurrently per batch (1-32).</param>
    /// <returns>The transport builder for fluent configuration.</returns>
    /// <remarks>
    /// <para>
    /// Controls parallelism within each fetch loop. Default is 1 (sequential).
    /// Total concurrent processing = MaxConcurrency × BatchProcessingConcurrency.
    /// </para>
    /// <para>
    /// <strong>Example:</strong> WithConcurrency(5).WithBatchProcessingConcurrency(4) = 20 total concurrent operations.
    /// </para>
    /// <para>
    /// Must be ? PrefetchMaxMessages. Start with low values and increase based on monitoring.
    /// </para>
    /// </remarks>
    public static ITransportBuilder WithBatchProcessingConcurrency(
        this ITransportBuilder builder,
        int batchConcurrency)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (batchConcurrency < 1 || batchConcurrency > 32)
        {
            throw new ArgumentOutOfRangeException(nameof(batchConcurrency), "BatchProcessingConcurrency must be between 1 and 32");
        }

        builder.Services.Configure<StorageQueueOptions>(options =>
        {
            options.BatchProcessingConcurrency = batchConcurrency;
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
