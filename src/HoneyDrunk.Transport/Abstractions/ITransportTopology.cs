namespace HoneyDrunk.Transport.Abstractions;

/// <summary>
/// Declares the capabilities of a transport adapter.
/// Used to validate configuration and provide runtime feature detection.
/// </summary>
public interface ITransportTopology
{
    /// <summary>
    /// Gets the name of the transport adapter.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets a value indicating whether the transport supports topic-based publish/subscribe.
    /// </summary>
    bool SupportsTopics { get; }

    /// <summary>
    /// Gets a value indicating whether the transport supports subscriptions to topics.
    /// </summary>
    bool SupportsSubscriptions { get; }

    /// <summary>
    /// Gets a value indicating whether the transport supports session-based ordered processing.
    /// </summary>
    bool SupportsSessions { get; }

    /// <summary>
    /// Gets a value indicating whether the transport guarantees message ordering.
    /// </summary>
    bool SupportsOrdering { get; }

    /// <summary>
    /// Gets a value indicating whether the transport supports scheduled/delayed message delivery.
    /// </summary>
    bool SupportsScheduledMessages { get; }

    /// <summary>
    /// Gets a value indicating whether the transport supports batch operations.
    /// </summary>
    bool SupportsBatching { get; }

    /// <summary>
    /// Gets a value indicating whether the transport supports transactional message processing.
    /// </summary>
    bool SupportsTransactions { get; }

    /// <summary>
    /// Gets a value indicating whether the transport supports dead-letter queues.
    /// </summary>
    bool SupportsDeadLetterQueue { get; }

    /// <summary>
    /// Gets a value indicating whether the transport supports message priority.
    /// </summary>
    bool SupportsPriority { get; }

    /// <summary>
    /// Gets the maximum message payload size in bytes (null = no limit).
    /// </summary>
    long? MaxMessageSize { get; }
}
