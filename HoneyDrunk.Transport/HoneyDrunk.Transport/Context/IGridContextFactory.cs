using HoneyDrunk.Kernel.Abstractions.Context;
using HoneyDrunk.Transport.Abstractions;

namespace HoneyDrunk.Transport.Context;

/// <summary>
/// Factory interface for initializing Grid context instances from transport envelopes.
/// </summary>
/// <remarks>
/// <para>
/// This interface bridges transport envelope metadata into Kernel's <see cref="IGridContext"/>.
/// Application code rarely calls this directly - it is infrastructure plumbing between
/// the middleware pipeline and message handlers.
/// </para>
/// <para>
/// <b>Kernel vNext Pattern (v0.4.0+):</b> The factory INITIALIZES an existing DI-scoped
/// <see cref="IGridContext"/> rather than creating a new instance. This ensures exactly
/// one GridContext per DI scope, owned by Kernel.
/// </para>
/// </remarks>
public interface IGridContextFactory
{
    /// <summary>
    /// Initializes a Grid context from transport envelope metadata.
    /// </summary>
    /// <param name="gridContext">The DI-scoped Grid context to initialize.</param>
    /// <param name="envelope">The transport envelope containing context metadata.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <remarks>
    /// <para>
    /// Called by <c>GridContextPropagationMiddleware</c> during message processing
    /// to initialize the DI-owned GridContext with envelope metadata.
    /// </para>
    /// <para>
    /// <b>Important:</b> The <paramref name="gridContext"/> must be resolved from the
    /// current DI scope. This method calls <c>GridContext.Initialize()</c> to set
    /// correlation, causation, tenant, project, baggage, and cancellation token.
    /// </para>
    /// </remarks>
    void InitializeFromEnvelope(
        IGridContext gridContext,
        ITransportEnvelope envelope,
        CancellationToken cancellationToken);
}
