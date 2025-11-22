namespace HoneyDrunk.Transport.Health;

/// <summary>
/// Health contributor for transport components.
/// Each transport implementation (Service Bus, Storage Queue, etc.) should provide a health contributor.
/// These integrate with Kernel health aggregation for Kubernetes readiness/liveness probes.
/// </summary>
public interface ITransportHealthContributor
{
    /// <summary>
    /// Gets the name of this health contributor for identification in health reports.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Checks the health of the transport component.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the health check.</param>
    /// <returns>The health check result.</returns>
    Task<TransportHealthResult> CheckHealthAsync(CancellationToken cancellationToken = default);
}
