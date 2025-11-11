using HoneyDrunk.Transport.Abstractions;

namespace HoneyDrunk.Transport.Telemetry;

/// <summary>
/// Middleware that adds telemetry to message processing.
/// </summary>
public sealed class TelemetryMiddleware : Pipeline.IMessageMiddleware
{
    /// <inheritdoc/>
    public async Task InvokeAsync(
        ITransportEnvelope envelope,
        MessageContext context,
        Func<Task> next,
        CancellationToken cancellationToken = default)
    {
        using var activity = TransportTelemetry.StartProcessActivity(envelope, "consumer");
        TransportTelemetry.RecordDeliveryCount(activity, context.DeliveryCount);

        try
        {
            await next();
            TransportTelemetry.RecordOutcome(activity, MessageProcessingResult.Success);
        }
        catch (Exception ex)
        {
            TransportTelemetry.RecordError(activity, ex);
            throw;
        }
    }
}
