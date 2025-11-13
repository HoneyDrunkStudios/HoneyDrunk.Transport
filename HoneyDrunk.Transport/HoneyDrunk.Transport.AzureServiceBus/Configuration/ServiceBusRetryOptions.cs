namespace HoneyDrunk.Transport.AzureServiceBus.Configuration;

/// <summary>
/// Service Bus specific retry configuration.
/// </summary>
public sealed class ServiceBusRetryOptions
{
    /// <summary>
    /// Gets or sets the retry mode for Service Bus operations.
    /// </summary>
    public ServiceBusRetryMode Mode { get; set; } = ServiceBusRetryMode.Exponential;

    /// <summary>
    /// Gets or sets the maximum number of retry attempts.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Gets or sets the delay between retry attempts.
    /// </summary>
    public TimeSpan Delay { get; set; } = TimeSpan.FromSeconds(0.8);

    /// <summary>
    /// Gets or sets the maximum delay between retries.
    /// </summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets the timeout for Service Bus operations.
    /// </summary>
    public TimeSpan TryTimeout { get; set; } = TimeSpan.FromMinutes(1);
}
