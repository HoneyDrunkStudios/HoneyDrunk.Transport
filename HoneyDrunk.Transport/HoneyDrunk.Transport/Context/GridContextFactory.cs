using HoneyDrunk.Kernel.Abstractions.Context;
using HoneyDrunk.Transport.Abstractions;

namespace HoneyDrunk.Transport.Context;

/// <summary>
/// Factory for creating Grid context instances from transport envelopes.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="GridContextFactory"/> class.
/// </remarks>
/// <param name="timeProvider">The time provider for timestamps.</param>
public sealed class GridContextFactory(TimeProvider timeProvider) : IGridContextFactory
{
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <summary>
    /// Creates a Grid context from a transport envelope.
    /// </summary>
    /// <param name="envelope">The transport envelope.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A Grid context instance.</returns>
    public IGridContext CreateFromEnvelope(ITransportEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        // Use messageId as fallback for correlation and causation if not provided
        var correlationId = envelope.CorrelationId ?? envelope.MessageId;
        var causationId = envelope.CausationId ?? envelope.MessageId;

        // Copy headers as baggage (immutably)
        var baggage = envelope.Headers != null
            ? new Dictionary<string, string>(envelope.Headers)
            : [];

        return new TransportGridContext(
            correlationId,
            causationId,
            envelope.NodeId ?? string.Empty,
            envelope.StudioId ?? string.Empty,
            envelope.TenantId,
            envelope.ProjectId,
            envelope.Environment ?? string.Empty,
            baggage,
            _timeProvider.GetUtcNow(),
            cancellationToken);
    }
}
