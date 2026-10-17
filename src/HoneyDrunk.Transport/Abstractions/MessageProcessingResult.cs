namespace HoneyDrunk.Transport.Abstractions;

/// <summary>
/// Result of processing a message.
/// </summary>
public enum MessageProcessingResult
{
    /// <summary>
    /// Message was successfully processed and should be acknowledged/completed.
    /// </summary>
    Success,

    /// <summary>
    /// Message processing failed but should be retried.
    /// </summary>
    Retry,

    /// <summary>
    /// Message processing failed and should be moved to dead letter queue.
    /// </summary>
    DeadLetter,

    /// <summary>
    /// Message should be abandoned back to the queue.
    /// </summary>
    Abandon
}
