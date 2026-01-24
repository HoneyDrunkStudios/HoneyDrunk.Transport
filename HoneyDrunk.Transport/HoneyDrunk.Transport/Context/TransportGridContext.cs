using HoneyDrunk.Kernel.Abstractions.Context;

namespace HoneyDrunk.Transport.Context;

/// <summary>
/// Transport-specific Grid context implementation for inbound messages.
/// This implementation is used when processing received messages to provide
/// handlers with a fully-hydrated Grid context from envelope metadata.
/// </summary>
/// <remarks>
/// <para>
/// <b>DEPRECATED (v0.4.0):</b> This class violates Kernel vNext's one-GridContext-per-scope
/// invariant. Use the DI-scoped <c>GridContext</c> from Kernel instead, initialized via
/// <see cref="IGridContextFactory.InitializeFromEnvelope"/>.
/// </para>
/// <para>
/// This context was previously created by <see cref="GridContextFactory"/> from transport envelope
/// metadata. It provided all the context information needed for distributed tracing,
/// multi-tenancy, and operation correlation.
/// </para>
/// </remarks>
/// <param name="correlationId">The correlation identifier for distributed tracing.</param>
/// <param name="causationId">The causation identifier linking to the causing operation.</param>
/// <param name="nodeId">The node identifier where the operation executes.</param>
/// <param name="studioId">The studio identifier for studio-scoped operations.</param>
/// <param name="tenantId">The tenant identifier for multi-tenant operations.</param>
/// <param name="projectId">The project identifier for project-scoped operations.</param>
/// <param name="environment">The environment name (e.g., production, staging).</param>
/// <param name="baggage">Key-value pairs propagated with the context.</param>
/// <param name="createdAtUtc">The UTC timestamp when the context was created.</param>
/// <param name="cancellation">The cancellation token for the operation.</param>
[Obsolete("TransportGridContext violates Kernel vNext's one-GridContext-per-scope invariant. " +
          "Use the DI-scoped GridContext from Kernel instead, initialized via IGridContextFactory.InitializeFromEnvelope(). " +
          "This class will be removed in v0.5.0.")]
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
    /// Gets a value indicating whether this context is initialized.
    /// Always returns true for TransportGridContext as it is initialized at construction.
    /// </summary>
    public bool IsInitialized => true;

    /// <summary>
    /// Creates a child context for nested operations.
    /// The current correlation ID becomes the causation ID in the child context.
    /// </summary>
    /// <param name="nodeId">Optional node ID override for the child context.</param>
    /// <returns>A new <see cref="IGridContext"/> with causation linking to this context.</returns>
    public IGridContext CreateChildContext(string? nodeId = null)
    {
        // Create child context with current correlation as causation
        return new TransportGridContext(
            correlationId: CorrelationId, // Correlation flows through
            causationId: CorrelationId,   // Current becomes causation
            nodeId: nodeId ?? NodeId,
            studioId: StudioId,
            tenantId: TenantId,
            projectId: ProjectId,
            environment: Environment,
            baggage: _baggage,
            createdAtUtc: CreatedAtUtc,
            cancellation: Cancellation);
    }

    /// <inheritdoc/>
    public void AddBaggage(string key, string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        _baggage[key] = value;
    }
}
