namespace HoneyDrunk.Transport.Metrics;

/// <summary>
/// Metrics collector for transport operations.
/// Integrates with Kernel IMetricsCollector for centralized metrics aggregation.
/// </summary>
public interface ITransportMetrics
{
    /// <summary>
    /// Records a message published.
    /// </summary>
    /// <param name="messageType">The type of message published.</param>
    /// <param name="destination">The destination endpoint.</param>
    void RecordMessagePublished(string messageType, string destination);

    /// <summary>
    /// Records a message consumed.
    /// </summary>
    /// <param name="messageType">The type of message consumed.</param>
    /// <param name="source">The source endpoint.</param>
    void RecordMessageConsumed(string messageType, string source);

    /// <summary>
    /// Records message processing duration.
    /// </summary>
    /// <param name="messageType">The type of message processed.</param>
    /// <param name="duration">The processing duration.</param>
    /// <param name="result">The processing result (success/retry/dead-letter).</param>
    void RecordProcessingDuration(string messageType, TimeSpan duration, string result);

    /// <summary>
    /// Records a message retry.
    /// </summary>
    /// <param name="messageType">The type of message retried.</param>
    /// <param name="attemptNumber">The retry attempt number.</param>
    void RecordMessageRetry(string messageType, int attemptNumber);

    /// <summary>
    /// Records a message moved to dead-letter queue.
    /// </summary>
    /// <param name="messageType">The type of message dead-lettered.</param>
    /// <param name="reason">The reason for dead-lettering.</param>
    void RecordMessageDeadLettered(string messageType, string reason);

    /// <summary>
    /// Records message payload size.
    /// </summary>
    /// <param name="messageType">The type of message.</param>
    /// <param name="sizeBytes">The payload size in bytes.</param>
    /// <param name="direction">The direction (publish/consume).</param>
    void RecordPayloadSize(string messageType, long sizeBytes, string direction);
}
