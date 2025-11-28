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
    string? tenantId,
    string? projectId,
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
    public string? TenantId { get; } = tenantId;

    /// <inheritdoc/>
    public string? ProjectId { get; } = projectId;

    /// <inheritdoc/>
    public string Environment { get; } = environment;

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, string> Baggage => _baggage;

    /// <inheritdoc/>
    public CancellationToken Cancellation { get; } = cancellation;

    /// <inheritdoc/>
    public DateTimeOffset CreatedAtUtc { get; } = createdAtUtc;

    /// <summary>
    /// Begins a scope for this Grid context.
    /// </summary>
    public IDisposable BeginScope()
    {
        // No-op scope for lightweight implementation
        return new NoOpScope();
    }

    /// <summary>
    /// Creates a child context derived from this context.
    /// </summary>
    public IGridContext CreateChildContext(string? nodeId = null)
    {
        // Create child context with current correlation as causation
        return new TransportGridContext(
            CorrelationId, // Correlation flows through
            CorrelationId, // Current becomes causation
            nodeId ?? NodeId,
            StudioId,
            TenantId,
            ProjectId,
            Environment,
            _baggage,
            CreatedAtUtc,
            Cancellation);
    }

    /// <summary>
    /// Creates a new context with additional baggage.
    /// </summary>
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
            TenantId,
            ProjectId,
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
