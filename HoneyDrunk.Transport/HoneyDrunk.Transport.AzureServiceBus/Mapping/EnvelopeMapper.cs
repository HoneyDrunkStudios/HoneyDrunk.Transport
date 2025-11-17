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
    /// <param name="envelope">The transport envelope to convert.</param>
    /// <returns>A Service Bus message.</returns>
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
    /// <param name="message">The Service Bus received message to convert.</param>
    /// <returns>A transport envelope.</returns>
    public static ITransportEnvelope FromServiceBusMessage(ServiceBusReceivedMessage message)
    {
        // Define reserved property keys that shouldn't be copied to headers
        var reservedProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            MessageTypeProperty,
            TimestampProperty,
            "CausationId"
        };

        // Extract headers from application properties, excluding reserved properties
        var headers = message.ApplicationProperties
            .Where(property => !reservedProperties.Contains(property.Key))
            .ToDictionary(
                property => property.Key,
                property => property.Value?.ToString() ?? string.Empty);

        // Parse timestamp - use separate variable to avoid TryParse overwriting fallback
        var timestamp = DateTimeOffset.UtcNow;
        if (message.ApplicationProperties.TryGetValue(TimestampProperty, out var timestampValue)
            && timestampValue is string timestampStr
            && DateTimeOffset.TryParse(timestampStr, out var parsedTimestamp))
        {
            // Successfully parsed - use parsed value
            timestamp = parsedTimestamp;
        }

        // If parsing fails or property doesn't exist, timestamp remains DateTimeOffset.UtcNow (fallback)

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
    /// <param name="message">The Service Bus received message.</param>
    /// <returns>The delivery count.</returns>
    public static int GetDeliveryCount(ServiceBusReceivedMessage message)
    {
        return (int)message.DeliveryCount;
    }

    /// <summary>
    /// Creates a transaction context from Service Bus message.
    /// </summary>
    /// <param name="message">The Service Bus received message.</param>
    /// <returns>A transport transaction context.</returns>
    public static ITransportTransaction CreateTransaction(ServiceBusReceivedMessage message)
    {
        return new ServiceBusTransportTransaction(message);
    }
}
