using HoneyDrunk.Kernel.Abstractions.Context;
using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.Primitives;

namespace HoneyDrunk.Transport.Publishers;

/// <summary>
/// Default implementation of <see cref="IMessagePublisher"/> that bridges
/// high-level typed message publishing to low-level envelope-based transport.
/// </summary>
/// <remarks>
/// This publisher converts typed messages into transport envelopes by:
/// 1. Serializing the message payload
/// 2. Creating an envelope with Grid context metadata
/// 3. Delegating to the low-level <see cref="ITransportPublisher"/>.
/// </remarks>
public sealed class MessagePublisher(
    ITransportPublisher transportPublisher,
    IMessageSerializer serializer,
    EnvelopeFactory envelopeFactory) : IMessagePublisher
{
    private readonly ITransportPublisher _transportPublisher = transportPublisher;
    private readonly IMessageSerializer _serializer = serializer;
    private readonly EnvelopeFactory _envelopeFactory = envelopeFactory;

    /// <inheritdoc/>
    public async Task PublishAsync<T>(
        string destination,
        T message,
        IGridContext gridContext,
        CancellationToken cancellationToken = default)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(gridContext);

        // Serialize message
        var payload = _serializer.Serialize(message);

        // Create envelope with Grid context
        var envelope = _envelopeFactory.CreateEnvelopeWithGridContext<T>(
            payload,
            gridContext);

        // Publish via low-level transport
        var endpointAddress = new EndpointAddress
        {
            Name = destination,
            Address = destination
        };

        await _transportPublisher.PublishAsync(
            envelope,
            endpointAddress,
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task PublishBatchAsync<T>(
        string destination,
        IEnumerable<T> messages,
        IGridContext gridContext,
        CancellationToken cancellationToken = default)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(gridContext);

        var messageList = messages.ToList();
        if (messageList.Count == 0)
        {
            return;
        }

        // Create envelopes for all messages
        var envelopes = messageList.Select(message =>
        {
            var payload = _serializer.Serialize(message);
            return _envelopeFactory.CreateEnvelopeWithGridContext<T>(payload, gridContext);
        }).ToList();

        // Publish batch via low-level transport
        var endpointAddress = new EndpointAddress
        {
            Name = destination,
            Address = destination
        };

        await _transportPublisher.PublishBatchAsync(
            envelopes,
            endpointAddress,
            cancellationToken);
    }
}
