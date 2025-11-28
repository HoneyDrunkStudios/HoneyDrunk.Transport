using Azure.Messaging.ServiceBus;
using HoneyDrunk.Kernel.Abstractions.Context;
using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.Primitives;

namespace HoneyDrunk.Transport.AzureServiceBus.Mapping;

/// <summary>
/// Maps between HoneyDrunk transport envelopes and Azure Service Bus messages.
/// Uses Kernel GridHeaderNames for consistent header naming across transports.
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

        // Map Grid context using Kernel's canonical header names
        if (!string.IsNullOrEmpty(envelope.CausationId))
        {
            message.ApplicationProperties[GridHeaderNames.CausationId] = envelope.CausationId;
        }

        if (!string.IsNullOrEmpty(envelope.NodeId))
        {
            message.ApplicationProperties[GridHeaderNames.NodeId] = envelope.NodeId;
        }

        if (!string.IsNullOrEmpty(envelope.StudioId))
        {
            message.ApplicationProperties[GridHeaderNames.StudioId] = envelope.StudioId;
        }

        if (!string.IsNullOrEmpty(envelope.TenantId))
        {
            message.ApplicationProperties[GridHeaderNames.TenantId] = envelope.TenantId;
        }

        if (!string.IsNullOrEmpty(envelope.ProjectId))
        {
            message.ApplicationProperties[GridHeaderNames.ProjectId] = envelope.ProjectId;
        }

        if (!string.IsNullOrEmpty(envelope.Environment))
        {
            message.ApplicationProperties[GridHeaderNames.Environment] = envelope.Environment;
        }

        // Copy all headers (includes baggage with X-Baggage- prefix)
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
            GridHeaderNames.CausationId,
            GridHeaderNames.NodeId,
            GridHeaderNames.StudioId,
            GridHeaderNames.TenantId,
            GridHeaderNames.ProjectId,
            GridHeaderNames.Environment
        };

        // Extract headers from application properties, excluding reserved Grid context properties
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

        // Extract Grid context using Kernel's canonical header names
        string? causationId = null;
        if (message.ApplicationProperties.TryGetValue(GridHeaderNames.CausationId, out var causationValue))
        {
            causationId = causationValue?.ToString();
        }

        string? nodeId = null;
        if (message.ApplicationProperties.TryGetValue(GridHeaderNames.NodeId, out var nodeValue))
        {
            nodeId = nodeValue?.ToString();
        }

        string? studioId = null;
        if (message.ApplicationProperties.TryGetValue(GridHeaderNames.StudioId, out var studioValue))
        {
            studioId = studioValue?.ToString();
        }

        string? tenantId = null;
        if (message.ApplicationProperties.TryGetValue(GridHeaderNames.TenantId, out var tenantValue))
        {
            tenantId = tenantValue?.ToString();
        }

        string? projectId = null;
        if (message.ApplicationProperties.TryGetValue(GridHeaderNames.ProjectId, out var projectValue))
        {
            projectId = projectValue?.ToString();
        }

        string? environment = null;
        if (message.ApplicationProperties.TryGetValue(GridHeaderNames.Environment, out var envValue))
        {
            environment = envValue?.ToString();
        }

        return new TransportEnvelope
        {
            MessageId = message.MessageId,
            CorrelationId = message.CorrelationId,
            CausationId = causationId,
            NodeId = nodeId,
            StudioId = studioId,
            TenantId = tenantId,
            ProjectId = projectId,
            Environment = environment,
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
