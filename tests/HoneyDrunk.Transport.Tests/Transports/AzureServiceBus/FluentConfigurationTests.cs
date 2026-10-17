using HoneyDrunk.Transport.AzureServiceBus.Configuration;
using HoneyDrunk.Transport.AzureServiceBus.DependencyInjection;
using HoneyDrunk.Transport.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using AsbRetryMode = HoneyDrunk.Transport.AzureServiceBus.Configuration.ServiceBusRetryMode;
using AsbRetryOptions = HoneyDrunk.Transport.AzureServiceBus.Configuration.ServiceBusRetryOptions;

namespace HoneyDrunk.Transport.Tests.Transports.AzureServiceBus;

/// <summary>
/// Tests for fluent configuration extension methods.
/// </summary>
public sealed class FluentConfigurationTests
{
    /// <summary>
    /// WithTopicSubscription configures subscription correctly.
    /// </summary>
    [Fact]
    public void WithTopicSubscription_ConfiguresSubscription()
    {
        var services = new ServiceCollection();
        services.AddHoneyDrunkTransportCore();

        var builder = services.AddHoneyDrunkServiceBusTransport(options =>
        {
            options.ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=test";
            options.Address = "test-topic";
        });

        builder.WithTopicSubscription("my-subscription");

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<AzureServiceBusOptions>>().Value;

        Assert.Equal(ServiceBusEntityType.Topic, options.EntityType);
        Assert.Equal("my-subscription", options.SubscriptionName);
    }

    /// <summary>
    /// WithSessions enables session support.
    /// </summary>
    [Fact]
    public void WithSessions_EnablesSessionSupport()
    {
        var services = new ServiceCollection();
        services.AddHoneyDrunkTransportCore();

        var builder = services.AddHoneyDrunkServiceBusTransport(options =>
        {
            options.ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=test";
            options.Address = "test-queue";
        });

        builder.WithSessions();

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<AzureServiceBusOptions>>().Value;

        Assert.True(options.SessionEnabled);
    }

    /// <summary>
    /// WithRetry configures retry options.
    /// </summary>
    [Fact]
    public void WithRetry_ConfiguresRetryOptions()
    {
        var services = new ServiceCollection();
        services.AddHoneyDrunkTransportCore();

        var builder = services.AddHoneyDrunkServiceBusTransport(options =>
        {
            options.ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=test";
            options.Address = "test-queue";
        });

        builder.WithRetry(retry =>
        {
            retry.MaxRetries = 5;
            retry.Delay = TimeSpan.FromSeconds(2);
            retry.MaxDelay = TimeSpan.FromMinutes(2);
            retry.Mode = AsbRetryMode.Exponential;
        });

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<AzureServiceBusOptions>>().Value;

        Assert.Equal(5, options.ServiceBusRetry.MaxRetries);
        Assert.Equal(TimeSpan.FromSeconds(2), options.ServiceBusRetry.Delay);
        Assert.Equal(TimeSpan.FromMinutes(2), options.ServiceBusRetry.MaxDelay);
        Assert.Equal(AsbRetryMode.Exponential, options.ServiceBusRetry.Mode);
    }

    /// <summary>
    /// WithBlobFallback enables and configures blob fallback.
    /// </summary>
    [Fact]
    public void WithBlobFallback_EnablesAndConfiguresBlobFallback()
    {
        var services = new ServiceCollection();
        services.AddHoneyDrunkTransportCore();

        var builder = services.AddHoneyDrunkServiceBusTransport(options =>
        {
            options.ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=test";
            options.Address = "test-queue";
        });

        builder.WithBlobFallback(blob =>
        {
            blob.ConnectionString = "UseDevelopmentStorage=true";
            blob.ContainerName = "my-fallback";
            blob.BlobPrefix = "custom-prefix";
        });

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<AzureServiceBusOptions>>().Value;

        Assert.True(options.BlobFallback.Enabled);
        Assert.Equal("UseDevelopmentStorage=true", options.BlobFallback.ConnectionString);
        Assert.Equal("my-fallback", options.BlobFallback.ContainerName);
        Assert.Equal("custom-prefix", options.BlobFallback.BlobPrefix);
    }

    /// <summary>
    /// AddHoneyDrunkServiceBusTransport with connection string overload configures correctly.
    /// </summary>
    [Fact]
    public void AddHoneyDrunkServiceBusTransport_ConnectionStringOverload_ConfiguresCorrectly()
    {
        var services = new ServiceCollection();
        services.AddHoneyDrunkTransportCore();

        services.AddHoneyDrunkServiceBusTransport(
            connectionString: "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=test",
            queueOrTopicName: "my-queue");

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<AzureServiceBusOptions>>().Value;

        Assert.Equal("Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=test", options.ConnectionString);
        Assert.Equal("my-queue", options.Address);
        Assert.Equal("my-queue", options.EndpointName);
    }

    /// <summary>
    /// AddHoneyDrunkServiceBusTransportWithManagedIdentity configures correctly.
    /// </summary>
    [Fact]
    public void AddHoneyDrunkServiceBusTransportWithManagedIdentity_ConfiguresCorrectly()
    {
        var services = new ServiceCollection();
        services.AddHoneyDrunkTransportCore();

        services.AddHoneyDrunkServiceBusTransportWithManagedIdentity(
            fullyQualifiedNamespace: "test.servicebus.windows.net",
            queueOrTopicName: "my-queue");

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<AzureServiceBusOptions>>().Value;

        Assert.Equal("test.servicebus.windows.net", options.FullyQualifiedNamespace);
        Assert.Equal("my-queue", options.Address);
        Assert.Equal("my-queue", options.EndpointName);
    }

    /// <summary>
    /// Fluent configuration methods can be chained.
    /// </summary>
    [Fact]
    public void FluentConfiguration_CanBeChained()
    {
        var services = new ServiceCollection();
        services.AddHoneyDrunkTransportCore();

        services.AddHoneyDrunkServiceBusTransport(options =>
            {
                options.ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=test";
                options.Address = "test-topic";
            })
            .WithTopicSubscription("my-sub")
            .WithSessions()
            .WithRetry(retry => retry.MaxRetries = 10)
            .WithBlobFallback(blob => blob.ConnectionString = "UseDevelopmentStorage=true");

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<AzureServiceBusOptions>>().Value;

        Assert.Equal(ServiceBusEntityType.Topic, options.EntityType);
        Assert.Equal("my-sub", options.SubscriptionName);
        Assert.True(options.SessionEnabled);
        Assert.Equal(10, options.ServiceBusRetry.MaxRetries);
        Assert.True(options.BlobFallback.Enabled);
    }

    /// <summary>
    /// ServiceBusRetryOptions has correct default values.
    /// </summary>
    [Fact]
    public void ServiceBusRetryOptions_HasCorrectDefaults()
    {
        var options = new AsbRetryOptions();

        Assert.Equal(AsbRetryMode.Exponential, options.Mode);
        Assert.Equal(3, options.MaxRetries);
        Assert.Equal(TimeSpan.FromSeconds(0.8), options.Delay);
        Assert.Equal(TimeSpan.FromMinutes(1), options.MaxDelay);
        Assert.Equal(TimeSpan.FromSeconds(60), options.TryTimeout);
    }

    /// <summary>
    /// AzureServiceBusOptions has correct default values.
    /// </summary>
    [Fact]
    public void AzureServiceBusOptions_HasCorrectDefaults()
    {
        var options = new AzureServiceBusOptions();

        Assert.Equal(ServiceBusEntityType.Queue, options.EntityType);
        Assert.Equal(TimeSpan.FromMinutes(1), options.MaxWaitTime);
        Assert.True(options.EnableDeadLetterQueue);
        Assert.Equal(10, options.MaxDeliveryCount);
        Assert.NotNull(options.ServiceBusRetry);
        Assert.NotNull(options.BlobFallback);
        Assert.False(options.BlobFallback.Enabled);
    }
}
