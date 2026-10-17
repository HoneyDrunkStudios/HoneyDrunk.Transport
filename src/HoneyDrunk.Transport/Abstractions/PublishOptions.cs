namespace HoneyDrunk.Transport.Abstractions;

/// <summary>
/// Publishing options for customizing message delivery behavior.
/// Not all properties are supported by all transport adapters - check adapter documentation.
/// </summary>
public sealed class PublishOptions
{
    /// <summary>
    /// Gets or initializes the time-to-live for the message (expiration).
    /// After this duration, the message will be automatically removed from the queue.
    /// </summary>
    public TimeSpan? TimeToLive { get; init; }

    /// <summary>
    /// Gets or initializes the scheduled enqueue time for delayed message delivery.
    /// The message will not be available for processing until this time.
    /// </summary>
    public DateTimeOffset? ScheduledEnqueueTime { get; init; }

    /// <summary>
    /// Gets or initializes the partition key for ordered delivery within a partition.
    /// Messages with the same partition key are guaranteed to be processed in order.
    /// </summary>
    public string? PartitionKey { get; init; }

    /// <summary>
    /// Gets or initializes the session identifier for session-based ordered processing.
    /// All messages with the same session ID are processed sequentially.
    /// </summary>
    public string? SessionId { get; init; }

    /// <summary>
    /// Gets or initializes the message priority level.
    /// Higher priority messages are processed before lower priority messages (adapter-dependent).
    /// </summary>
    public MessagePriority Priority { get; init; } = MessagePriority.Normal;

    /// <summary>
    /// Gets or initializes additional adapter-specific options.
    /// Use this for transport-specific features not covered by standard properties.
    /// </summary>
    public IReadOnlyDictionary<string, object>? AdditionalOptions { get; init; }
}
