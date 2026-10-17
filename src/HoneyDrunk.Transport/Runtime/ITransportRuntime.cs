using HoneyDrunk.Transport.Health;

namespace HoneyDrunk.Transport.Runtime;

/// <summary>
/// Unified runtime orchestrator for transport lifecycle management.
/// Coordinates consumer startup, shutdown, and health aggregation across all transport adapters.
/// </summary>
public interface ITransportRuntime
{
    /// <summary>
    /// Gets a value indicating whether the runtime is currently running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Starts all registered transport consumers.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous start operation.</returns>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops all transport consumers gracefully, allowing in-flight messages to complete.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous stop operation.</returns>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all health contributors from registered transports for aggregation.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Collection of transport health contributors.</returns>
    Task<IEnumerable<ITransportHealthContributor>> GetHealthContributorsAsync(
        CancellationToken cancellationToken = default);
}
