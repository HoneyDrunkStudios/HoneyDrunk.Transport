namespace HoneyDrunk.Transport.AzureServiceBus.Configuration;

/// <summary>
/// Options for Blob Storage fallback when publishing to Service Bus fails.
/// </summary>
public sealed class BlobFallbackOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether Blob fallback is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the Blob Storage connection string. If not provided, <see cref="AccountUrl"/> with managed identity will be used.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the Blob service account URL (e.g., https://account.blob.core.windows.net). Used with DefaultAzureCredential when ConnectionString is not set.
    /// </summary>
    public string? AccountUrl { get; set; }

    /// <summary>
    /// Gets or sets the container name used to store fallback messages.
    /// </summary>
    public string ContainerName { get; set; } = "transport-fallback";

    /// <summary>
    /// Gets or sets an optional blob name prefix (folders) for organizing blobs (e.g., "servicebus/").
    /// </summary>
    public string? BlobPrefix { get; set; }

    /// <summary>
    /// Gets or sets the optional time-to-live after which the blob may be deleted by a lifecycle policy.
    /// This library does not manage lifecycle; this is informational only and can be used by external policies.
    /// </summary>
    public TimeSpan? TimeToLive { get; set; }
}
