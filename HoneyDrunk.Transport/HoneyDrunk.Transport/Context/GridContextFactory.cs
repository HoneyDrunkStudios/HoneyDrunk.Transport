using HoneyDrunk.Kernel.Abstractions.Context;
using HoneyDrunk.Transport.Abstractions;

namespace HoneyDrunk.Transport.Context;

/// <summary>
/// Default Grid context factory implementation.
/// Creates Grid context from transport envelope metadata for distributed context propagation.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="GridContextFactory"/> class.
/// </remarks>
/// <param name="timeProvider">The time provider for timestamps.</param>
public sealed class GridContextFactory(TimeProvider timeProvider) : IGridContextFactory
{
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc />
    public IGridContext CreateFromEnvelope(ITransportEnvelope envelope, CancellationToken cancellationToken)
    {
        // Create Grid context from envelope fields
        var correlationId = envelope.CorrelationId ?? envelope.MessageId;
        var causationId = envelope.CausationId ?? envelope.MessageId;

        var baggage = envelope.Headers != null
            ? new Dictionary<string, string>(envelope.Headers)
            : [];

        return new TransportGridContext(
            correlationId,
            causationId,
            envelope.NodeId ?? string.Empty,
            envelope.StudioId ?? string.Empty,
            envelope.Environment ?? string.Empty,
            baggage,
            _timeProvider.GetUtcNow(),
            cancellationToken);
    }
}
