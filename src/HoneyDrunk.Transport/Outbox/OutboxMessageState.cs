namespace HoneyDrunk.Transport.Outbox;

/// <summary>
/// State of an outbox message.
/// </summary>
public enum OutboxMessageState
{
    /// <summary>
    /// Message is pending dispatch.
    /// </summary>
    Pending,

    /// <summary>
    /// Message is currently being dispatched.
    /// </summary>
    Processing,

    /// <summary>
    /// Message was successfully dispatched.
    /// </summary>
    Dispatched,

    /// <summary>
    /// Message dispatch failed and is awaiting retry.
    /// </summary>
    Failed,

    /// <summary>
    /// Message has exceeded retry limits and is poisoned.
    /// </summary>
    Poisoned
}
