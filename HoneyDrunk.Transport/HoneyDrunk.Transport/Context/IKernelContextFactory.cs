using HoneyDrunk.Kernel.Abstractions.Context;
using HoneyDrunk.Transport.Abstractions;

namespace HoneyDrunk.Transport.Context;

/// <summary>
/// Factory for creating kernel context instances from transport envelopes.
/// </summary>
public interface IKernelContextFactory
{
    /// <summary>
    /// Creates a kernel context from a transport envelope.
    /// </summary>
    /// <param name="envelope">The transport envelope.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>A new kernel context instance.</returns>
    IKernelContext CreateFromEnvelope(ITransportEnvelope envelope, CancellationToken cancellationToken);
}
