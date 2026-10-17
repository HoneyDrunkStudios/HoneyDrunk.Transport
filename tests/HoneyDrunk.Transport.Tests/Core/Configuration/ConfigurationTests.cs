using HoneyDrunk.Transport.Configuration;

namespace HoneyDrunk.Transport.Tests.Core.Configuration;

/// <summary>
/// Tests for configuration option classes.
/// </summary>
public sealed class ConfigurationTests
{
    /// <summary>
    /// Verifies RetryOptions defaults.
    /// </summary>
    [Fact]
    public void RetryOptions_WhenCreated_HasExpectedDefaults()
    {
        var opts = new RetryOptions();
        Assert.Equal(3, opts.MaxAttempts);
        Assert.Equal(BackoffStrategy.Exponential, opts.Strategy);
        Assert.Equal(TimeSpan.FromSeconds(1), opts.InitialDelay);
        Assert.Equal(TimeSpan.FromMinutes(5), opts.MaxDelay);
    }

    /// <summary>
    /// Verifies BackoffStrategy enum values exist.
    /// </summary>
    [Fact]
    public void BackoffStrategy_WhenValidating_HasExpectedEnumValues()
    {
        Assert.Equal(0, (int)BackoffStrategy.Fixed);
        Assert.Equal(1, (int)BackoffStrategy.Linear);
        Assert.Equal(2, (int)BackoffStrategy.Exponential);
    }

    /// <summary>
    /// Verifies ErrorHandlingAction enum values exist.
    /// </summary>
    [Fact]
    public void ErrorHandlingAction_WhenValidating_HasExpectedEnumValues()
    {
        Assert.Equal(0, (int)ErrorHandlingAction.Retry);
        Assert.Equal(1, (int)ErrorHandlingAction.DeadLetter);
    }

    /// <summary>
    /// Verifies TransportCoreOptions defaults.
    /// </summary>
    [Fact]
    public void TransportCoreOptions_WhenCreated_HasExpectedDefaults()
    {
        var opts = new TransportCoreOptions();
        Assert.True(opts.EnableTelemetry);
        Assert.True(opts.EnableLogging);
    }

    /// <summary>
    /// Verifies TransportOptions defaults.
    /// </summary>
    [Fact]
    public void TransportOptions_WhenCreated_HasExpectedDefaults()
    {
        var opts = new TransportOptions();
        Assert.Equal(1, opts.MaxConcurrency);
        Assert.Equal(string.Empty, opts.EndpointName);
        Assert.Equal(string.Empty, opts.Address);
    }
}
