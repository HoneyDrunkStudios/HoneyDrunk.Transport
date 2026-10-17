namespace HoneyDrunk.Transport.Abstractions;

/// <summary>
/// Publishes messages to transport endpoints.
/// </summary>
public interface ITransportPublisher
{
    /// <summary>
    /// Publishes an envelope to a destination endpoint.
    /// </summary>
    /// <param name="envelope">The message envelope to publish.</param>
    /// <param name="destination">The destination endpoint address.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task PublishAsync(
        ITransportEnvelope envelope,
        IEndpointAddress destination,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes multiple envelopes as a batch.
    /// </summary>
    /// <param name="envelopes">The collection of envelopes to publish.</param>
    /// <param name="destination">The destination endpoint address.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task PublishBatchAsync(
        IEnumerable<ITransportEnvelope> envelopes,
        IEndpointAddress destination,
        CancellationToken cancellationToken = default);
}
