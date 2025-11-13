using HoneyDrunk.Transport.Configuration;

namespace HoneyDrunk.Transport.AzureServiceBus.Configuration;

/// <summary>
/// Configuration options for Azure Service Bus transport.
/// </summary>
public sealed class AzureServiceBusOptions : TransportOptions
{
    /// <summary>
    /// Gets or sets the fully qualified namespace (e.g., "mynamespace.servicebus.windows.net").
    /// </summary>
    public string FullyQualifiedNamespace { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the connection string (alternative to FullyQualifiedNamespace with managed identity).
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the entity type: Queue or Topic.
    /// </summary>
    public ServiceBusEntityType EntityType { get; set; } = ServiceBusEntityType.Queue;

    /// <summary>
    /// Gets or sets the subscription name (required for Topic entity type).
    /// </summary>
    public string? SubscriptionName { get; set; }

    /// <summary>
    /// Gets or sets the maximum wait time for receiving messages.
    /// </summary>
    public TimeSpan MaxWaitTime { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Gets or sets the Service Bus retry options.
    /// </summary>
    public ServiceBusRetryOptions ServiceBusRetry { get; set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether to use sub-queue (dead letter queue).
    /// </summary>
    public bool EnableDeadLetterQueue { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum delivery count before moving to dead letter queue.
    /// </summary>
    public int MaxDeliveryCount { get; set; } = 10;
}
