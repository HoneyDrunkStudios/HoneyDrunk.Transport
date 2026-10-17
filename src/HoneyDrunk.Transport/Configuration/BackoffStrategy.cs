namespace HoneyDrunk.Transport.Configuration;

/// <summary>
/// Backoff strategy for retries.
/// </summary>
public enum BackoffStrategy
{
    /// <summary>
    /// Fixed delay between retries.
    /// </summary>
    Fixed,

    /// <summary>
    /// Linear increase in delay.
    /// </summary>
    Linear,

    /// <summary>
    /// Exponential increase in delay.
    /// </summary>
    Exponential
}
