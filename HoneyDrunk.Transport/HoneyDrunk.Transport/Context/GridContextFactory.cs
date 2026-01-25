using HoneyDrunk.Kernel.Abstractions.Context;
using HoneyDrunk.Transport.Abstractions;

using KernelGridContext = HoneyDrunk.Kernel.Context.GridContext;

namespace HoneyDrunk.Transport.Context;

/// <summary>
/// Factory for initializing Grid context instances from transport envelope metadata.
/// </summary>
/// <remarks>
/// <para>
/// <b>Kernel vNext Pattern (v0.4.0+):</b> This factory INITIALIZES an existing DI-scoped
/// <see cref="KernelGridContext"/> rather than creating a new instance. This ensures exactly
/// one GridContext per DI scope, owned by Kernel.
/// </para>
/// </remarks>
public sealed class GridContextFactory : IGridContextFactory
{
    /// <inheritdoc/>
    public void InitializeFromEnvelope(
        IGridContext gridContext,
        ITransportEnvelope envelope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(gridContext);
        ArgumentNullException.ThrowIfNull(envelope);

        // gridContext must be Kernel's GridContext for Initialize() to work
        if (gridContext is not KernelGridContext kernelContext)
        {
            throw new InvalidOperationException(
                $"Expected GridContext from Kernel DI scope, but got {gridContext.GetType().FullName}. " +
                "Ensure IGridContext is registered as scoped and resolves to HoneyDrunk.Kernel.Context.GridContext.");
        }

        // Use messageId as fallback for correlation and causation if not provided
        var correlationId = envelope.CorrelationId ?? envelope.MessageId;
        var causationId = envelope.CausationId ?? envelope.MessageId;

        // Copy headers as baggage
        var baggage = envelope.Headers != null
            ? new Dictionary<string, string>(envelope.Headers)
            : [];

        // Initialize the DI-owned GridContext with envelope metadata
        kernelContext.Initialize(
            correlationId: correlationId,
            causationId: causationId,
            tenantId: envelope.TenantId,
            projectId: envelope.ProjectId,
            baggage: baggage,
            cancellation: cancellationToken);
    }
}
