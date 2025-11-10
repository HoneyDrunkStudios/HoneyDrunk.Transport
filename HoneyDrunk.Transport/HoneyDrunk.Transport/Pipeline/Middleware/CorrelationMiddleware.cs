using HoneyDrunk.Transport.Abstractions;

namespace HoneyDrunk.Transport.Pipeline.Middleware;

/// <summary>
/// Middleware that enriches the message context with correlation information.
/// </summary>
public sealed class CorrelationMiddleware : IMessageMiddleware
{
    /// <inheritdoc/>
    public Task InvokeAsync(
        ITransportEnvelope envelope,
        MessageContext context,
        Func<Task> next,
        CancellationToken cancellationToken = default)
    {
        // Enrich context with correlation data
        context.Properties["CorrelationId"] = envelope.CorrelationId ?? envelope.MessageId;
        context.Properties["CausationId"] = envelope.CausationId ?? string.Empty;

        return next();
    }
}
