using System.ComponentModel.DataAnnotations;
using HoneyDrunk.Transport.Configuration;

namespace HoneyDrunk.Transport.StorageQueue.Configuration;

/// <summary>
/// Configuration options for Azure Storage Queue transport.
/// </summary>
public sealed class StorageQueueOptions : TransportOptions
{
    /// <summary>
    /// Gets or sets the connection string for Azure Storage Queue.
    /// </summary>
    /// <remarks>
    /// Required if <see cref="AccountEndpoint"/> is not specified.
    /// </remarks>
    [Required(ErrorMessage = "ConnectionString or AccountEndpoint must be configured")]
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the storage account endpoint URI (for managed identity).
    /// </summary>
    /// <remarks>
    /// TODO: Add TokenCredential support for managed identity authentication.
    /// </remarks>
    public Uri? AccountEndpoint { get; set; }

    /// <summary>
    /// Gets or sets the queue name.
    /// </summary>
    [Required(ErrorMessage = "QueueName is required")]
    public string QueueName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether to create the queue if it doesn't exist.
    /// </summary>
    public bool CreateIfNotExists { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to base64-encode message payloads.
    /// </summary>
    /// <remarks>
    /// Azure Storage Queues support base64 encoding for binary data.
    /// Default is true to handle arbitrary byte payloads safely.
    /// </remarks>
    public bool Base64EncodePayload { get; set; } = true;

    /// <summary>
    /// Gets or sets the message time-to-live (TTL).
    /// </summary>
    /// <remarks>
    /// Null means use the service default (7 days).
    /// </remarks>
    public TimeSpan? MessageTimeToLive { get; set; }

    /// <summary>
    /// Gets or sets the visibility timeout for received messages.
    /// </summary>
    /// <remarks>
    /// Duration a message remains invisible after being retrieved.
    /// Default is 30 seconds.
    /// </remarks>
    public TimeSpan VisibilityTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the maximum number of messages to retrieve per receive call.
    /// </summary>
    /// <remarks>
    /// Valid range: 1-32 (Azure Storage Queue limit).
    /// Default is 16 for balanced throughput.
    /// </remarks>
    [Range(1, 32, ErrorMessage = "PrefetchMaxMessages must be between 1 and 32")]
    public int PrefetchMaxMessages { get; set; } = 16;

    /// <summary>
    /// Gets or sets the maximum dequeue count before a message is considered poison.
    /// </summary>
    /// <remarks>
    /// After this many delivery attempts, the message moves to the poison queue.
    /// Default is 5.
    /// </remarks>
    [Range(1, 100, ErrorMessage = "MaxDequeueCount must be between 1 and 100")]
    public int MaxDequeueCount { get; set; } = 5;

    /// <summary>
    /// Gets or sets the poison queue name.
    /// </summary>
    /// <remarks>
    /// If null, defaults to "{QueueName}-poison".
    /// </remarks>
    public string? PoisonQueueName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether telemetry is enabled.
    /// </summary>
    public bool EnableTelemetry { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether logging is enabled.
    /// </summary>
    public bool EnableLogging { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether correlation tracking is enabled.
    /// </summary>
    public bool EnableCorrelation { get; set; } = true;

    /// <summary>
    /// Gets or sets the polling interval when the queue is empty.
    /// </summary>
    /// <remarks>
    /// Default is 1 second. Uses exponential backoff with jitter.
    /// </remarks>
    public TimeSpan EmptyQueuePollingInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets the maximum polling interval during idle periods.
    /// </summary>
    /// <remarks>
    /// Used for exponential backoff when queue is consistently empty.
    /// Default is 5 seconds.
    /// </remarks>
    public TimeSpan MaxPollingInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets the effective poison queue name.
    /// </summary>
    internal string GetPoisonQueueName() => PoisonQueueName ?? $"{QueueName}-poison";
}
