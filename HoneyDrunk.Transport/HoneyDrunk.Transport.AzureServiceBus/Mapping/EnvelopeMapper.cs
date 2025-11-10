using Azure.Messaging.ServiceBus;
using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.Primitives;

namespace HoneyDrunk.Transport.AzureServiceBus.Mapping;

/// <summary>
/// Maps between HoneyDrunk transport envelopes and Azure Service Bus messages.
/// </summary>
public static class EnvelopeMapper
{
    private const string MessageTypeProperty = "MessageType";
    private const string TimestampProperty = "Timestamp";

    /// <summary>
    /// Converts a transport envelope to a Service Bus message.
    /// </summary>
    public static ServiceBusMessage ToServiceBusMessage(ITransportEnvelope envelope)
    {
        var message = new ServiceBusMessage(envelope.Payload)
        {
            MessageId = envelope.MessageId,
            CorrelationId = envelope.CorrelationId,
            Subject = envelope.MessageType,
            ContentType = "application/octet-stream"
        };

        // Add custom properties
        message.ApplicationProperties[MessageTypeProperty] = envelope.MessageType;
        message.ApplicationProperties[TimestampProperty] = envelope.Timestamp.ToString("o");

        if (!string.IsNullOrEmpty(envelope.CausationId))
        {
            message.ApplicationProperties["CausationId"] = envelope.CausationId;
        }

        // Copy all headers
        foreach (var header in envelope.Headers)
        {
            message.ApplicationProperties[header.Key] = header.Value;
        }

        return message;
    }

    /// <summary>
    /// Converts a Service Bus received message to a transport envelope.
    /// </summary>
    public static ITransportEnvelope FromServiceBusMessage(ServiceBusReceivedMessage message)
    {
        var headers = new Dictionary<string, string>();

        // Extract headers from application properties
        foreach (var property in message.ApplicationProperties)
        {
            if (property.Key != MessageTypeProperty && 
                property.Key != TimestampProperty && 
                property.Key != "CausationId")
            {
                headers[property.Key] = property.Value?.ToString() ?? string.Empty;
            }
        }

        // Parse timestamp
        var timestamp = DateTimeOffset.UtcNow;
        if (message.ApplicationProperties.TryGetValue(TimestampProperty, out var timestampValue) 
            && timestampValue is string timestampStr)
        {
            DateTimeOffset.TryParse(timestampStr, out timestamp);
        }

        // Get message type
        var messageType = message.Subject;
        if (message.ApplicationProperties.TryGetValue(MessageTypeProperty, out var messageTypeValue))
        {
            messageType = messageTypeValue?.ToString() ?? messageType;
        }

        // Get causation ID
        string? causationId = null;
        if (message.ApplicationProperties.TryGetValue("CausationId", out var causationValue))
        {
            causationId = causationValue?.ToString();
        }

        return new TransportEnvelope
        {
            MessageId = message.MessageId,
            CorrelationId = message.CorrelationId,
            CausationId = causationId,
            Timestamp = timestamp,
            MessageType = messageType ?? "Unknown",
            Headers = headers,
            Payload = message.Body.ToMemory()
        };
    }

    /// <summary>
    /// Extracts delivery count from Service Bus message.
    /// </summary>
    public static int GetDeliveryCount(ServiceBusReceivedMessage message)
    {
        return (int)message.DeliveryCount;
    }

    /// <summary>
    /// Creates a transaction context from Service Bus message.
    /// </summary>
    public static ITransportTransaction CreateTransaction(ServiceBusReceivedMessage message)
    {
        return new ServiceBusTransportTransaction(message);
    }
}

/// <summary>
/// Service Bus specific transaction context.
/// </summary>
internal sealed class ServiceBusTransportTransaction : ITransportTransaction
{
    private readonly ServiceBusReceivedMessage _message;

    public ServiceBusTransportTransaction(ServiceBusReceivedMessage message)
    {
        _message = message;
        TransactionId = message.MessageId;
    }

    public string TransactionId { get; }

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
