namespace HoneyDrunk.Transport.Health;

/// <summary>
/// Health check result for transport components.
/// Integrates with Kernel health/readiness probes for Kubernetes deployments.
/// </summary>
public sealed class TransportHealthResult
{
    /// <summary>
    /// Gets a value indicating whether the transport component is healthy.
    /// </summary>
    public required bool IsHealthy { get; init; }

    /// <summary>
    /// Gets the human-readable description of the health status.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets optional metadata about the health check (e.g., connection latency, queue depth).
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }

    /// <summary>
    /// Gets the exception that caused the health check to fail, if any.
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    /// Creates a healthy result.
    /// </summary>
    /// <param name="description">Description of the healthy state.</param>
    /// <param name="metadata">Optional metadata.</param>
    /// <returns>A healthy transport health result.</returns>
    public static TransportHealthResult Healthy(string description, IReadOnlyDictionary<string, object>? metadata = null)
        => new() { IsHealthy = true, Description = description, Metadata = metadata };

    /// <summary>
    /// Creates an unhealthy result.
    /// </summary>
    /// <param name="description">Description of the unhealthy state.</param>
    /// <param name="exception">Optional exception that caused the failure.</param>
    /// <param name="metadata">Optional metadata.</param>
    /// <returns>An unhealthy transport health result.</returns>
    public static TransportHealthResult Unhealthy(string description, Exception? exception = null, IReadOnlyDictionary<string, object>? metadata = null)
        => new() { IsHealthy = false, Description = description, Exception = exception, Metadata = metadata };
}
