using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.StorageQueue.Configuration;
using HoneyDrunk.Transport.StorageQueue.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HoneyDrunk.Transport.Tests.Transports.StorageQueue;

/// <summary>
/// Tests for Azure Storage Queue service registration extensions.
/// </summary>
public sealed class StorageQueueServiceCollectionExtensionsTests
{
    /// <summary>
    /// Gets identifiers for invalid fluent storage queue settings.
    /// Test rows are <see cref="string"/>s — not <see cref="Action"/>s — so Test
    /// Explorer can enumerate the rows individually (Sonar S6562 — TheoryData
    /// type arguments must be serializable). The test method builds the
    /// invalid configuration from the identifier.
    /// </summary>
    public static TheoryData<string> InvalidFluentSettings => new(
        "MaxDequeueCount-zero",
        "MaxDequeueCount-over-limit",
        "VisibilityTimeout-zero",
        "VisibilityTimeout-over-limit",
        "MessageTimeToLive-zero",
        "MessageTimeToLive-over-limit",
        "PrefetchCount-zero",
        "PrefetchCount-over-limit",
        "Concurrency-zero",
        "Concurrency-over-limit",
        "BatchPublishConcurrency-zero",
        "BatchPublishConcurrency-over-limit",
        "BatchProcessingConcurrency-zero",
        "BatchProcessingConcurrency-over-limit");

    /// <summary>
    /// Delegate registration wires transport services and configured options.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task AddHoneyDrunkTransportStorageQueue_WithConfigureAction_RegistersTransportServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddHoneyDrunkTransportStorageQueue(options =>
        {
            options.ConnectionString = "UseDevelopmentStorage=true";
            options.QueueName = "orders";
        });

        await using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<StorageQueueOptions>>().Value;

        Assert.Equal("UseDevelopmentStorage=true", options.ConnectionString);
        Assert.Equal("orders", options.QueueName);
        Assert.NotNull(provider.GetRequiredService<ITransportPublisher>());
        Assert.NotNull(provider.GetRequiredService<ITransportConsumer>());
        Assert.Equal("StorageQueue", provider.GetRequiredService<ITransportTopology>().Name);
    }

    /// <summary>
    /// Connection-string overload maps endpoint name and address from the queue name.
    /// </summary>
    [Fact]
    public void AddHoneyDrunkTransportStorageQueue_WithConnectionString_SetsEndpointFields()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddHoneyDrunkTransportStorageQueue(
            "UseDevelopmentStorage=true",
            "orders",
            options => options.MaxConcurrency = 3);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<StorageQueueOptions>>().Value;

        Assert.Equal("UseDevelopmentStorage=true", options.ConnectionString);
        Assert.Equal("orders", options.QueueName);
        Assert.Equal("orders", options.Address);
        Assert.Equal("orders", options.EndpointName);
        Assert.Equal(3, options.MaxConcurrency);
    }

    /// <summary>
    /// Fluent storage queue options compose into the configured options snapshot.
    /// </summary>
    [Fact]
    public void FluentStorageQueueOptions_WhenComposed_UpdateConfiguredOptions()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddHoneyDrunkTransportStorageQueue("UseDevelopmentStorage=true", "orders")
            .WithPoisonQueue("orders-dead")
            .WithMaxDequeueCount(9)
            .WithVisibilityTimeout(TimeSpan.FromMinutes(2))
            .WithMessageTimeToLive(TimeSpan.FromHours(1))
            .WithPrefetchCount(12)
            .WithConcurrency(7)
            .WithBatchPublishConcurrency(8)
            .WithBatchProcessingConcurrency(4)
            .WithoutAutoCreateQueue();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<StorageQueueOptions>>().Value;

        Assert.Equal("orders-dead", options.PoisonQueueName);
        Assert.Equal(9, options.MaxDequeueCount);
        Assert.Equal(TimeSpan.FromMinutes(2), options.VisibilityTimeout);
        Assert.Equal(TimeSpan.FromHours(1), options.MessageTimeToLive);
        Assert.Equal(12, options.PrefetchMaxMessages);
        Assert.Equal(7, options.MaxConcurrency);
        Assert.Equal(8, options.MaxBatchPublishConcurrency);
        Assert.Equal(4, options.BatchProcessingConcurrency);
        Assert.False(options.CreateIfNotExists);
    }

    /// <summary>
    /// Null service collections are rejected by the primary overload.
    /// </summary>
    [Fact]
    public void AddHoneyDrunkTransportStorageQueue_WhenServicesNull_Throws()
    {
        IServiceCollection? services = null;

        Assert.Throws<ArgumentNullException>(() =>
            services!.AddHoneyDrunkTransportStorageQueue(_ => { }));
    }

    /// <summary>
    /// Null configuration delegates are rejected.
    /// </summary>
    [Fact]
    public void AddHoneyDrunkTransportStorageQueue_WhenConfigureNull_Throws()
    {
        var services = new ServiceCollection();
        Action<StorageQueueOptions>? configure = null;

        Assert.Throws<ArgumentNullException>(() =>
            services.AddHoneyDrunkTransportStorageQueue(configure!));
    }

    /// <summary>
    /// Invalid fluent numeric settings are rejected.
    /// </summary>
    /// <param name="settingId">The identifier for the invalid configuration call.</param>
    [Theory]
    [MemberData(nameof(InvalidFluentSettings))]
    public void FluentStorageQueueOptions_WhenInvalid_ThrowArgumentOutOfRange(string settingId)
    {
        Action apply = BuildInvalidFluentSetting(settingId);

        Assert.Throws<ArgumentOutOfRangeException>(apply);
    }

    private static Action BuildInvalidFluentSetting(string settingId)
    {
        IServiceCollection NewServices() => new ServiceCollection();

        return settingId switch
        {
            "MaxDequeueCount-zero" => () => NewServices().AddHoneyDrunkTransportStorageQueue("UseDevelopmentStorage=true", "orders").WithMaxDequeueCount(0),
            "MaxDequeueCount-over-limit" => () => NewServices().AddHoneyDrunkTransportStorageQueue("UseDevelopmentStorage=true", "orders").WithMaxDequeueCount(101),
            "VisibilityTimeout-zero" => () => NewServices().AddHoneyDrunkTransportStorageQueue("UseDevelopmentStorage=true", "orders").WithVisibilityTimeout(TimeSpan.Zero),
            "VisibilityTimeout-over-limit" => () => NewServices().AddHoneyDrunkTransportStorageQueue("UseDevelopmentStorage=true", "orders").WithVisibilityTimeout(TimeSpan.FromDays(8)),
            "MessageTimeToLive-zero" => () => NewServices().AddHoneyDrunkTransportStorageQueue("UseDevelopmentStorage=true", "orders").WithMessageTimeToLive(TimeSpan.Zero),
            "MessageTimeToLive-over-limit" => () => NewServices().AddHoneyDrunkTransportStorageQueue("UseDevelopmentStorage=true", "orders").WithMessageTimeToLive(TimeSpan.FromDays(8)),
            "PrefetchCount-zero" => () => NewServices().AddHoneyDrunkTransportStorageQueue("UseDevelopmentStorage=true", "orders").WithPrefetchCount(0),
            "PrefetchCount-over-limit" => () => NewServices().AddHoneyDrunkTransportStorageQueue("UseDevelopmentStorage=true", "orders").WithPrefetchCount(33),
            "Concurrency-zero" => () => NewServices().AddHoneyDrunkTransportStorageQueue("UseDevelopmentStorage=true", "orders").WithConcurrency(0),
            "Concurrency-over-limit" => () => NewServices().AddHoneyDrunkTransportStorageQueue("UseDevelopmentStorage=true", "orders").WithConcurrency(101),
            "BatchPublishConcurrency-zero" => () => NewServices().AddHoneyDrunkTransportStorageQueue("UseDevelopmentStorage=true", "orders").WithBatchPublishConcurrency(0),
            "BatchPublishConcurrency-over-limit" => () => NewServices().AddHoneyDrunkTransportStorageQueue("UseDevelopmentStorage=true", "orders").WithBatchPublishConcurrency(129),
            "BatchProcessingConcurrency-zero" => () => NewServices().AddHoneyDrunkTransportStorageQueue("UseDevelopmentStorage=true", "orders").WithBatchProcessingConcurrency(0),
            "BatchProcessingConcurrency-over-limit" => () => NewServices().AddHoneyDrunkTransportStorageQueue("UseDevelopmentStorage=true", "orders").WithBatchProcessingConcurrency(33),
            _ => throw new ArgumentOutOfRangeException(nameof(settingId), settingId, "Unknown invalid fluent setting identifier."),
        };
    }
}
