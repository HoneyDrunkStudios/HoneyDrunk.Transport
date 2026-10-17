using HoneyDrunk.Kernel.Abstractions.Context;
using HoneyDrunk.Transport.Abstractions;

namespace HoneyDrunk.Transport.Context;

/// <summary>
/// Factory interface for creating Grid context instances from transport envelopes.
/// </summary>
/// <remarks>
/// <para>
/// This interface bridges transport envelope metadata into Kernel's <see cref="IGridContext"/>.
/// Application code rarely calls this directly - it is infrastructure plumbing between
/// the middleware pipeline and message handlers.
/// </para>
/// <para>
/// The factory creates an abstractions-only initialized context so Transport does not depend
/// on Kernel runtime implementation types.
/// </para>
/// </remarks>
public interface IGridContextFactory
{
    /// <summary>
    /// Creates an initialized Grid context from transport envelope metadata.
    /// </summary>
    /// <param name="envelope">The transport envelope containing context metadata.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>An initialized Grid context carrying envelope propagation metadata.</returns>
    /// <remarks>
    /// Called by <c>GridContextPropagationMiddleware</c> during message processing
    /// to populate <c>MessageContext.GridContext</c> without requiring a runtime Kernel reference.
    /// </remarks>
    IGridContext CreateFromEnvelope(
        ITransportEnvelope envelope,
        CancellationToken cancellationToken);
}
