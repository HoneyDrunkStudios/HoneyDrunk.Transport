using HoneyDrunk.Transport.Outbox;

namespace HoneyDrunk.Transport.Health;

/// <summary>
/// Health contributor for the transactional outbox.
/// Reports outbox backlog size and potential issues with message dispatch.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="OutboxHealthContributor"/> class.
/// </remarks>
/// <param name="outboxStore">The outbox store to check.</param>
public sealed class OutboxHealthContributor(IOutboxStore outboxStore) : ITransportHealthContributor
{
    private const int WarningThreshold = 1000;
    private const int CriticalThreshold = 10000;

    private readonly IOutboxStore _outboxStore = outboxStore;

    /// <inheritdoc/>
    public string Name => "Transport.Outbox";

    /// <inheritdoc/>
    public async Task<TransportHealthResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Load pending messages to check backlog
            var pending = await _outboxStore.LoadPendingAsync(CriticalThreshold + 1, cancellationToken);
            var count = pending.Count();

            var metadata = new Dictionary<string, object>
            {
                ["PendingCount"] = count,
                ["WarningThreshold"] = WarningThreshold,
                ["CriticalThreshold"] = CriticalThreshold
            };

            if (count >= CriticalThreshold)
            {
                return TransportHealthResult.Unhealthy(
                    $"Outbox backlog is critical: {count} pending messages (threshold: {CriticalThreshold})",
                    metadata: metadata);
            }

            if (count >= WarningThreshold)
            {
                // Still healthy but include warning in metadata
                metadata["Warning"] = $"Backlog approaching threshold: {count}/{WarningThreshold}";
            }

            return TransportHealthResult.Healthy(
                count > 0
                    ? $"Outbox operational with {count} pending messages"
                    : "Outbox operational with no pending messages",
                metadata: metadata);
        }
        catch (Exception ex)
        {
            return TransportHealthResult.Unhealthy(
                "Outbox health check failed - unable to query pending messages",
                ex);
        }
    }
}
