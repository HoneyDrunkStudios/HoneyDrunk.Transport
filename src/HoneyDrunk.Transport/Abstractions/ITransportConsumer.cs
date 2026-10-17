namespace HoneyDrunk.Transport.Abstractions;

/// <summary>
/// Consumes messages from transport endpoints.
/// </summary>
public interface ITransportConsumer
{
    /// <summary>
    /// Starts consuming messages from the configured endpoint.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops consuming messages gracefully.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task StopAsync(CancellationToken cancellationToken = default);
}
