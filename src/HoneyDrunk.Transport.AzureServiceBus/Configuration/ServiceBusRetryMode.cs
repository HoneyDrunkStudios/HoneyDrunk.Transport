namespace HoneyDrunk.Transport.AzureServiceBus.Configuration;

/// <summary>
/// Retry mode for Service Bus.
/// </summary>
public enum ServiceBusRetryMode
{
    /// <summary>
    /// Fixed delay between retries.
    /// </summary>
    Fixed,

    /// <summary>
    /// Exponential backoff between retries.
    /// </summary>
    Exponential
}
