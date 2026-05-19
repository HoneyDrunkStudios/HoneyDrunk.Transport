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
    /// Gets invalid fluent storage queue settings.
    /// </summary>
    public static TheoryData<Action> InvalidFluentSettings => new()
    {
        () => new ServiceCollection().AddHoneyDrunkTransportStorageQueue("UseDevelopmentStorage=true", "orders").WithMaxDequeueCount(0),
        () => new ServiceCollection().AddHoneyDrunkTransportStorageQueue("UseDevelopmentStorage=true", "orders").WithMaxDequeueCount(101),
        () => new ServiceCollection().AddHoneyDrunkTransportStorageQueue("UseDevelopmentStorage=true", "orders").WithVisibilityTimeout(TimeSpan.Zero),
        () => new ServiceCollection().AddHoneyDrunkTransportStorageQueue("UseDevelopmentStorage=true", "orders").WithVisibilityTimeout(TimeSpan.FromDays(8)),
        () => new ServiceCollection().AddHoneyDrunkTransportStorageQueue("UseDevelopmentStorage=true", "orders").WithMessageTimeToLive(TimeSpan.Zero),
        () => new ServiceCollection().AddHoneyDrunkTransportStorageQueue("UseDevelopmentStorage=true", "orders").WithMessageTimeToLive(TimeSpan.FromDays(8)),
        () => new ServiceCollection().AddHoneyDrunkTransportStorageQueue("UseDevelopmentStorage=true", "orders").WithPrefetchCount(0),
        () => new ServiceCollection().AddHoneyDrunkTransportStorageQueue("UseDevelopmentStorage=true", "orders").WithPrefetchCount(33),
        () => new ServiceCollection().AddHoneyDrunkTransportStorageQueue("UseDevelopmentStorage=true", "orders").WithConcurrency(0),
        () => new ServiceCollection().AddHoneyDrunkTransportStorageQueue("UseDevelopmentStorage=true", "orders").WithConcurrency(101),
        () => new ServiceCollection().AddHoneyDrunkTransportStorageQueue("UseDevelopmentStorage=true", "orders").WithBatchPublishConcurrency(0),
        () => new ServiceCollection().AddHoneyDrunkTransportStorageQueue("UseDevelopmentStorage=true", "orders").WithBatchPublishConcurrency(129),
        () => new ServiceCollection().AddHoneyDrunkTransportStorageQueue("UseDevelopmentStorage=true", "orders").WithBatchProcessingConcurrency(0),
        () => new ServiceCollection().AddHoneyDrunkTransportStorageQueue("UseDevelopmentStorage=true", "orders").WithBatchProcessingConcurrency(33)
    };

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
    /// <param name="apply">The invalid configuration call.</param>
    [Theory]
    [MemberData(nameof(InvalidFluentSettings))]
    public void FluentStorageQueueOptions_WhenInvalid_ThrowArgumentOutOfRange(Action apply)
    {
        Assert.Throws<ArgumentOutOfRangeException>(apply);
    }
}
