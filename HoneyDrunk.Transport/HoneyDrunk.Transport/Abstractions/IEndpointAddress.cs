namespace HoneyDrunk.Transport.Abstractions;

/// <summary>
/// Represents a logical transport endpoint address with typed metadata.
/// </summary>
public interface IEndpointAddress
{
    /// <summary>
    /// Gets the logical name of the endpoint.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the transport-specific address information (queue name, topic name, etc.).
    /// </summary>
    string Address { get; }

    /// <summary>
    /// Gets the session identifier for session-based ordered processing.
    /// </summary>
    string? SessionId { get; }

    /// <summary>
    /// Gets the partition key for partitioned endpoints (ordering within partition).
    /// </summary>
    string? PartitionKey { get; }

    /// <summary>
    /// Gets the scheduled enqueue time for delayed message delivery.
    /// </summary>
    DateTimeOffset? ScheduledEnqueueTime { get; }

    /// <summary>
    /// Gets the time-to-live for the message (expiration).
    /// </summary>
    TimeSpan? TimeToLive { get; }

    /// <summary>
    /// Gets the optional adapter-specific properties for extensions.
    /// </summary>
    IReadOnlyDictionary<string, string> AdditionalProperties { get; }
}
