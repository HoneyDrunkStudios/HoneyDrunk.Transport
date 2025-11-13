using HoneyDrunk.Transport.Abstractions;

namespace HoneyDrunk.Transport.AzureServiceBus.Mapping;

/// <summary>
/// Service Bus specific transaction context.
/// </summary>
internal sealed class ServiceBusTransportTransaction(Azure.Messaging.ServiceBus.ServiceBusReceivedMessage message) : ITransportTransaction
{
    private readonly Azure.Messaging.ServiceBus.ServiceBusReceivedMessage _message = message;

    public string TransactionId { get; } = message.MessageId;

    public IReadOnlyDictionary<string, object> Context => new Dictionary<string, object>
    {
        ["ServiceBusMessage"] = _message,
        ["LockToken"] = _message.LockToken,
        ["SequenceNumber"] = _message.SequenceNumber,
        ["EnqueuedTime"] = _message.EnqueuedTime
    };

    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        // Completion is handled by the processor
        return Task.CompletedTask;
    }

    public Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        // Abandonment is handled by the processor
        return Task.CompletedTask;
    }
}
