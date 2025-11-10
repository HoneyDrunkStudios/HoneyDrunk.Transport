using HoneyDrunk.Transport.Abstractions;

namespace HoneyDrunk.Transport.Configuration;

/// <summary>
/// Configuration options for a transport endpoint.
/// </summary>
public class TransportOptions
{
    /// <summary>
    /// Gets or sets the logical name of the endpoint.
    /// </summary>
    public string EndpointName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the physical address (queue name, topic name, etc.).
    /// </summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the maximum number of concurrent message processors.
    /// </summary>
    public int MaxConcurrency { get; set; } = 1;

    /// <summary>
    /// Gets or sets the prefetch count for message retrieval optimization.
    /// </summary>
    public int PrefetchCount { get; set; }

    /// <summary>
    /// Gets or sets the retry configuration.
    /// </summary>
    public RetryOptions Retry { get; set; } = new();

    /// <summary>
    /// Gets or sets the message lock/visibility timeout.
    /// </summary>
    public TimeSpan MessageLockDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets a value indicating whether to automatically complete messages on success.
    /// </summary>
    public bool AutoComplete { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to use sessions for ordered processing.
    /// </summary>
    public bool SessionEnabled { get; set; }

    /// <summary>
    /// Gets the additional transport-specific properties.
    /// </summary>
    public Dictionary<string, string> Properties { get; } = [];
}
