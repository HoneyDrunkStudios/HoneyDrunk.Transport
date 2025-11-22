using HoneyDrunk.Kernel.Abstractions.Context;

namespace HoneyDrunk.Transport.Context;

/// <summary>
/// Lightweight Grid context implementation for Transport.
/// This is used when the full Kernel package is not available.
/// Applications should use Kernel's GridContext implementation when possible.
/// </summary>
internal sealed class TransportGridContext(
    string correlationId,
    string? causationId,
    string nodeId,
    string studioId,
    string environment,
    IReadOnlyDictionary<string, string> baggage,
    DateTimeOffset createdAtUtc,
    CancellationToken cancellation) : IGridContext
{
    private readonly Dictionary<string, string> _baggage = new(baggage);

    /// <inheritdoc/>
    public string CorrelationId { get; } = correlationId;

    /// <inheritdoc/>
    public string? CausationId { get; } = causationId;

    /// <inheritdoc/>
    public string NodeId { get; } = nodeId;

    /// <inheritdoc/>
    public string StudioId { get; } = studioId;

    /// <inheritdoc/>
    public string Environment { get; } = environment;

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, string> Baggage => _baggage;

    /// <inheritdoc/>
    public CancellationToken Cancellation { get; } = cancellation;

    /// <inheritdoc/>
    public DateTimeOffset CreatedAtUtc { get; } = createdAtUtc;

    /// <inheritdoc/>
    public IDisposable BeginScope()
    {
        // No-op scope for lightweight implementation
        return new NoOpScope();
    }

    /// <inheritdoc/>
    public IGridContext CreateChildContext(string? nodeId = null)
    {
        // Create child context with current correlation as causation
        return new TransportGridContext(
            CorrelationId, // Correlation flows through
            CorrelationId, // Current becomes causation
            nodeId ?? NodeId,
            StudioId,
            Environment,
            _baggage,
            CreatedAtUtc,
            Cancellation);
    }

    /// <inheritdoc/>
    public IGridContext WithBaggage(string key, string value)
    {
        var newBaggage = new Dictionary<string, string>(_baggage)
        {
            [key] = value
        };

        return new TransportGridContext(
            CorrelationId,
            CausationId,
            NodeId,
            StudioId,
            Environment,
            newBaggage,
            CreatedAtUtc,
            Cancellation);
    }

    private sealed class NoOpScope : IDisposable
    {
        public void Dispose()
        {
            // No-op
        }
    }
}
