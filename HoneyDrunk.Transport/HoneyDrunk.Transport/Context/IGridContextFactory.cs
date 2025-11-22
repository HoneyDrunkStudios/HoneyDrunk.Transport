using HoneyDrunk.Kernel.Abstractions.Context;
using HoneyDrunk.Transport.Abstractions;

namespace HoneyDrunk.Transport.Context;

/// <summary>
/// Factory for creating Grid context instances from transport envelopes.
/// Bridges Transport envelope metadata to Kernel IGridContext for distributed context propagation.
/// </summary>
public interface IGridContextFactory
{
    /// <summary>
    /// Creates a Grid context from a transport envelope.
    /// </summary>
    /// <param name="envelope">The transport envelope containing Grid metadata.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>A new Grid context instance.</returns>
    IGridContext CreateFromEnvelope(ITransportEnvelope envelope, CancellationToken cancellationToken);
}
