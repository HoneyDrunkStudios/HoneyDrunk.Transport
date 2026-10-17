namespace HoneyDrunk.Transport.Outbox;

/// <summary>
/// Configuration options for the outbox dispatcher.
/// </summary>
public sealed class OutboxDispatcherOptions
{
    /// <summary>
    /// Gets or sets the poll interval for checking pending messages.
    /// </summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the batch size for loading pending messages.
    /// </summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets the maximum number of retry attempts before marking a message as poisoned.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 5;

    /// <summary>
    /// Gets or sets the base delay for exponential backoff retry strategy.
    /// </summary>
    public TimeSpan BaseRetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets the maximum retry delay (cap for exponential backoff).
    /// </summary>
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the delay before starting the dispatcher (allows other services to initialize).
    /// </summary>
    public TimeSpan StartupDelay { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// Gets or sets the delay after an error before retrying the dispatch loop.
    /// </summary>
    public TimeSpan ErrorDelay { get; set; } = TimeSpan.FromSeconds(30);
}
