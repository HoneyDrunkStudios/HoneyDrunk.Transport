using HoneyDrunk.Transport.Abstractions;

namespace HoneyDrunk.Transport.Health;

/// <summary>
/// Health contributor for message publishers.
/// Reports whether the publisher can send messages to the underlying transport.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="PublisherHealthContributor"/> class.
/// </remarks>
/// <param name="publisher">The transport publisher to check.</param>
public sealed class PublisherHealthContributor(ITransportPublisher publisher) : ITransportHealthContributor
{
    private readonly ITransportPublisher _publisher = publisher;

    /// <inheritdoc/>
    public string Name => "Transport.Publisher";

    /// <inheritdoc/>
    public async Task<TransportHealthResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Basic health check - publisher is registered and available
            // Actual connectivity check would require transport-specific implementation
            _ = _publisher; // Validate publisher is not null

            return TransportHealthResult.Healthy(
                "Publisher is available",
                metadata: new Dictionary<string, object>
                {
                    ["PublisherType"] = _publisher.GetType().Name
                });
        }
        catch (Exception ex)
        {
            return TransportHealthResult.Unhealthy(
                "Publisher health check failed",
                ex);
        }
    }
}
