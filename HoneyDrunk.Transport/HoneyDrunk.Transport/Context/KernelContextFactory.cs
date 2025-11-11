using HoneyDrunk.Kernel.Abstractions.Context;
using HoneyDrunk.Kernel.Context;
using HoneyDrunk.Transport.Abstractions;

namespace HoneyDrunk.Transport.Context;

/// <summary>
/// Default implementation of kernel context factory.
/// </summary>
public sealed class KernelContextFactory : IKernelContextFactory
{
    /// <inheritdoc />
    public IKernelContext CreateFromEnvelope(ITransportEnvelope envelope, CancellationToken cancellationToken)
    {
        // Use CorrelationId from envelope, or generate from MessageId
        var correlationId = envelope.CorrelationId ?? envelope.MessageId;

        // Use CausationId from envelope, or use MessageId
        var causationId = envelope.CausationId ?? envelope.MessageId;

        // Create baggage from envelope headers
        var baggage = envelope.Headers != null
            ? new Dictionary<string, string>(envelope.Headers)
            : [];

        return new KernelContext(correlationId, causationId, baggage, cancellationToken);
    }
}
