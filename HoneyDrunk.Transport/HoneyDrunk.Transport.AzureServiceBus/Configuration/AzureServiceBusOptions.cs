using HoneyDrunk.Transport.Configuration;

namespace HoneyDrunk.Transport.AzureServiceBus.Configuration;

/// <summary>
/// Configuration options for Azure Service Bus transport.
/// </summary>
public sealed class AzureServiceBusOptions : TransportOptions
{
    /// <summary>
    /// Fully qualified namespace (e.g., "mynamespace.servicebus.windows.net").
    /// </summary>
    public string FullyQualifiedNamespace { get; set; } = string.Empty;

    /// <summary>
    /// Connection string (alternative to FullyQualifiedNamespace with managed identity).
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Entity type: Queue or Topic.
    /// </summary>
    public ServiceBusEntityType EntityType { get; set; } = ServiceBusEntityType.Queue;

    /// <summary>
    /// Subscription name (required for Topic entity type).
    /// </summary>
    public string? SubscriptionName { get; set; }

    /// <summary>
    /// Maximum wait time for receiving messages.
    /// </summary>
    public TimeSpan MaxWaitTime { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Service Bus retry options.
    /// </summary>
    public ServiceBusRetryOptions ServiceBusRetry { get; set; } = new();

    /// <summary>
    /// Whether to use sub-queue (dead letter queue).
    /// </summary>
    public bool EnableDeadLetterQueue { get; set; } = true;

    /// <summary>
    /// Maximum delivery count before moving to dead letter queue.
    /// </summary>
    public int MaxDeliveryCount { get; set; } = 10;
}

/// <summary>
/// Entity type for Azure Service Bus.
/// </summary>
public enum ServiceBusEntityType
{
    /// <summary>
    /// Queue entity type.
    /// </summary>
    Queue,
    
    /// <summary>
    /// Topic entity type.
    /// </summary>
    Topic
}

/// <summary>
/// Service Bus specific retry configuration.
/// </summary>
public sealed class ServiceBusRetryOptions
{
    /// <summary>
    /// Retry mode for Service Bus operations.
    /// </summary>
    public ServiceBusRetryMode Mode { get; set; } = ServiceBusRetryMode.Exponential;

    /// <summary>
    /// Maximum number of retry attempts.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Delay between retry attempts.
    /// </summary>
    public TimeSpan Delay { get; set; } = TimeSpan.FromSeconds(0.8);

    /// <summary>
    /// Maximum delay between retries.
    /// </summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Timeout for Service Bus operations.
    /// </summary>
    public TimeSpan TryTimeout { get; set; } = TimeSpan.FromMinutes(1);
}

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
