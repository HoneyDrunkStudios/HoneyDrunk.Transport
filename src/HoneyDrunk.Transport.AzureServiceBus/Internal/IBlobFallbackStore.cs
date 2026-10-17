using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.AzureServiceBus.Configuration;

namespace HoneyDrunk.Transport.AzureServiceBus.Internal;

/// <summary>
/// Persists failed Service Bus publishes to durable storage for later replay.
/// </summary>
internal interface IBlobFallbackStore
{
    /// <summary>
    /// Saves the envelope and destination to storage with failure metadata.
    /// </summary>
    /// <param name="envelope">Envelope to persist.</param>
    /// <param name="destination">Destination address metadata.</param>
    /// <param name="failure">The publish failure exception.</param>
    /// <param name="options">Blob fallback configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>URI of the created blob.</returns>
    Task<Uri> SaveAsync(
        ITransportEnvelope envelope,
        IEndpointAddress destination,
        Exception failure,
        BlobFallbackOptions options,
        CancellationToken cancellationToken);
}
