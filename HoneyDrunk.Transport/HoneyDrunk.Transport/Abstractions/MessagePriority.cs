namespace HoneyDrunk.Transport.Abstractions;

/// <summary>
/// Message priority levels for transport adapters that support prioritization.
/// </summary>
public enum MessagePriority
{
    /// <summary>
    /// Low priority - processed after normal and high priority messages.
    /// </summary>
    Low = 0,

    /// <summary>
    /// Normal priority - default priority level.
    /// </summary>
    Normal = 1,

    /// <summary>
    /// High priority - processed before normal and low priority messages.
    /// </summary>
    High = 2
}
