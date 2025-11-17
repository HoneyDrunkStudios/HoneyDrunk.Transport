using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.Telemetry;
using Microsoft.Extensions.Logging;

namespace HoneyDrunk.Transport.InMemory;

/// <summary>
/// In-memory implementation of transport publisher.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="InMemoryTransportPublisher"/> class.
/// </remarks>
/// <param name="broker">The in-memory message broker.</param>
/// <param name="logger">The logger instance.</param>
public sealed class InMemoryTransportPublisher(
    InMemoryBroker broker,
    ILogger<InMemoryTransportPublisher> logger) : ITransportPublisher
{
    private readonly InMemoryBroker _broker = broker;
    private readonly ILogger<InMemoryTransportPublisher> _logger = logger;

    /// <inheritdoc />
    public async Task PublishAsync(
        ITransportEnvelope envelope,
        IEndpointAddress destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(destination);

        using var activity = TransportTelemetry.StartPublishActivity(envelope, destination);

        try
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Publishing message {MessageId} to {Destination}",
                    envelope.MessageId,
                    destination.Address);
            }

            await _broker.PublishAsync(destination.Address, envelope, cancellationToken);

            TransportTelemetry.RecordOutcome(activity, MessageProcessingResult.Success);

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Successfully published message {MessageId}",
                    envelope.MessageId);
            }
        }
        catch (Exception ex)
        {
            TransportTelemetry.RecordError(activity, ex);

            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(
                    ex,
                    "Failed to publish message {MessageId}",
                    envelope.MessageId);
            }

            throw;
        }
    }

    /// <inheritdoc />
    public async Task PublishBatchAsync(
        IEnumerable<ITransportEnvelope> envelopes,
        IEndpointAddress destination,
        CancellationToken cancellationToken = default)
    {
        foreach (var envelope in envelopes)
        {
            await PublishAsync(envelope, destination, cancellationToken);
        }
    }
}
