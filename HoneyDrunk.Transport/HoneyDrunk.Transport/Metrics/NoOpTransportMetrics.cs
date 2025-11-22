namespace HoneyDrunk.Transport.Metrics;

/// <summary>
/// No-op implementation of transport metrics when metrics collection is disabled.
/// </summary>
public sealed class NoOpTransportMetrics : ITransportMetrics
{
    /// <summary>
    /// Gets the singleton instance of the no-op metrics collector.
    /// </summary>
    public static readonly NoOpTransportMetrics Instance = new();

    private NoOpTransportMetrics()
    {
    }

    /// <inheritdoc/>
    public void RecordMessagePublished(string messageType, string destination)
    {
        // No-op
    }

    /// <inheritdoc/>
    public void RecordMessageConsumed(string messageType, string source)
    {
        // No-op
    }

    /// <inheritdoc/>
    public void RecordProcessingDuration(string messageType, TimeSpan duration, string result)
    {
        // No-op
    }

    /// <inheritdoc/>
    public void RecordMessageRetry(string messageType, int attemptNumber)
    {
        // No-op
    }

    /// <inheritdoc/>
    public void RecordMessageDeadLettered(string messageType, string reason)
    {
        // No-op
    }

    /// <inheritdoc/>
    public void RecordPayloadSize(string messageType, long sizeBytes, string direction)
    {
        // No-op
    }
}
