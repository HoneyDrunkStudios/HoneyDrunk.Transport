namespace HoneyDrunk.Transport.Configuration;

/// <summary>
/// Action to take for a failed message.
/// </summary>
public enum ErrorHandlingAction
{
    /// <summary>
    /// Retry processing the message.
    /// </summary>
    Retry,

    /// <summary>
    /// Move the message to the dead letter queue.
    /// </summary>
    DeadLetter,

    /// <summary>
    /// Abandon the message back to the queue.
    /// </summary>
    Abandon
}
