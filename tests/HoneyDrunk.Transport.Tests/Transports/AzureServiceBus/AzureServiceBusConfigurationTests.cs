using HoneyDrunk.Transport.AzureServiceBus.Configuration;

namespace HoneyDrunk.Transport.Tests.Transports.AzureServiceBus;

/// <summary>
/// Tests for Azure Service Bus configuration.
/// </summary>
public sealed class AzureServiceBusConfigurationTests
{
    /// <summary>
    /// Verifies AzureServiceBusOptions defaults.
    /// </summary>
    [Fact]
    public void AzureServiceBusOptions_WhenCreated_HasExpectedDefaults()
    {
        var opts = new AzureServiceBusOptions();
        Assert.Null(opts.ConnectionString);
        Assert.Equal(string.Empty, opts.FullyQualifiedNamespace);
        Assert.Equal(ServiceBusEntityType.Queue, opts.EntityType);
        Assert.Null(opts.SubscriptionName);
    }

    /// <summary>
    /// Verifies ServiceBusEntityType enum values.
    /// </summary>
    [Fact]
    public void ServiceBusEntityType_WhenValidating_HasExpectedEnumValues()
    {
        Assert.Equal(0, (int)ServiceBusEntityType.Queue);
        Assert.Equal(1, (int)ServiceBusEntityType.Topic);
    }

    /// <summary>
    /// Verifies ServiceBusRetryMode enum values.
    /// </summary>
    [Fact]
    public void ServiceBusRetryMode_WhenValidating_HasExpectedEnumValues()
    {
        Assert.Equal(0, (int)ServiceBusRetryMode.Fixed);
        Assert.Equal(1, (int)ServiceBusRetryMode.Exponential);
    }

    /// <summary>
    /// Verifies ServiceBusRetryOptions defaults.
    /// </summary>
    [Fact]
    public void ServiceBusRetryOptions_WhenCreated_HasExpectedDefaults()
    {
        var opts = new ServiceBusRetryOptions();
        Assert.Equal(ServiceBusRetryMode.Exponential, opts.Mode);
        Assert.Equal(3, opts.MaxRetries);
        Assert.Equal(TimeSpan.FromSeconds(0.8), opts.Delay);
        Assert.Equal(TimeSpan.FromMinutes(1), opts.MaxDelay);
        Assert.Equal(TimeSpan.FromSeconds(60), opts.TryTimeout);
    }
}
