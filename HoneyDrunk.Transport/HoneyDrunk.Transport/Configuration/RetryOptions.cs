namespace HoneyDrunk.Transport.Configuration;

/// <summary>
/// Retry and backoff configuration.
/// </summary>
public sealed class RetryOptions
{
    /// <summary>
    /// Gets or sets the maximum number of retry attempts.
    /// </summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets the initial delay before first retry.
    /// </summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets the maximum delay between retries.
    /// </summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the backoff strategy.
    /// </summary>
    public BackoffStrategy Strategy { get; set; } = BackoffStrategy.Exponential;

    /// <summary>
    /// Gets or sets the multiplier for exponential backoff.
    /// </summary>
    public double BackoffMultiplier { get; set; } = 2.0;

    /// <summary>
    /// Gets or sets a value indicating whether to add jitter to retry delays.
    /// </summary>
    public bool UseJitter { get; set; } = true;

    /// <summary>
    /// Calculates the delay for a given retry attempt.
    /// </summary>
    /// <param name="attempt">The attempt number (1-based).</param>
    /// <returns>The calculated delay duration.</returns>
    public TimeSpan CalculateDelay(int attempt)
    {
        if (attempt <= 0)
        {
            return TimeSpan.Zero;
        }

        TimeSpan delay = Strategy switch
        {
            BackoffStrategy.Fixed => InitialDelay,
            BackoffStrategy.Linear => TimeSpan.FromMilliseconds(InitialDelay.TotalMilliseconds * attempt),
            BackoffStrategy.Exponential => TimeSpan.FromMilliseconds(
                InitialDelay.TotalMilliseconds * Math.Pow(BackoffMultiplier, attempt - 1)),
            _ => InitialDelay
        };

        // Cap at max delay
        if (delay > MaxDelay)
        {
            delay = MaxDelay;
        }

        // Add jitter if enabled
        if (UseJitter)
        {
            var jitter = Random.Shared.NextDouble() * 0.3; // ±30% jitter
            delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * (1.0 + jitter - 0.15));
        }

        return delay;
    }
}
