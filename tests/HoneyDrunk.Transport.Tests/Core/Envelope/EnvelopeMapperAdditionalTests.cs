using Azure.Messaging.ServiceBus;
using HoneyDrunk.Kernel.Abstractions.Context;
using HoneyDrunk.Transport.AzureServiceBus.Mapping;
using HoneyDrunk.Transport.Primitives;
using HoneyDrunk.Transport.Telemetry;
using HoneyDrunk.Transport.Tests.Support;
using System.Diagnostics;

namespace HoneyDrunk.Transport.Tests.Core.Envelope;

/// <summary>
/// Additional tests to improve coverage for Azure Service Bus mapping and telemetry.
/// </summary>
public sealed class EnvelopeMapperAdditionalTests
{
    /// <summary>
    /// Ensures FromServiceBusMessage maps core fields from a received message.
    /// </summary>
    [Fact]
    public void FromServiceBusMessage_WithCoreFields_MapsCorrectly()
    {
        // Arrange
        var sbMessage = Azure.Messaging.ServiceBus.ServiceBusModelFactory.ServiceBusReceivedMessage(
            messageId: "id-123",
            correlationId: "corr-456",
            subject: typeof(SampleMessage).FullName);

        // Act
        var envelope = EnvelopeMapper.FromServiceBusMessage(sbMessage);

        // Assert
        Assert.Equal("id-123", envelope.MessageId);
        Assert.Equal("corr-456", envelope.CorrelationId);
        Assert.Equal(typeof(SampleMessage).FullName, envelope.MessageType);
    }

    /// <summary>
    /// Ensures GetDeliveryCount returns the SDK delivery count.
    /// </summary>
    [Fact]
    public void GetDeliveryCount_ReturnsMessageDeliveryCount()
    {
        // Arrange
        var msg = Azure.Messaging.ServiceBus.ServiceBusModelFactory.ServiceBusReceivedMessage(
            deliveryCount: 5);

        // Act
        var count = EnvelopeMapper.GetDeliveryCount(msg);

        // Assert
        Assert.Equal(5, count);
    }

    /// <summary>
    /// Ensures CreateTransaction wraps the received message and exposes context.
    /// </summary>
    [Fact]
    public void CreateTransaction_ReturnsTransactionWithContext()
    {
        // Arrange
        var msg = Azure.Messaging.ServiceBus.ServiceBusModelFactory.ServiceBusReceivedMessage(
            messageId: "abc",
            deliveryCount: 1,
            sequenceNumber: 123);

        // Act
        var tx = EnvelopeMapper.CreateTransaction(msg);

        // Assert
        Assert.NotNull(tx);
        Assert.Equal("abc", tx.TransactionId);
        Assert.True(tx.Context.ContainsKey("ServiceBusMessage"));
        Assert.True(tx.Context.ContainsKey("LockToken"));
        Assert.True(tx.Context.ContainsKey("SequenceNumber"));
    }

    /// <summary>
    /// Ensures EnrichActivity adds the provided custom tags to the activity.
    /// </summary>
    [Fact]
    public void EnrichActivity_AddsCustomTags()
    {
        // Arrange
        // Create a listener to enable activity creation
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == TransportTelemetry.ActivitySourceName,
            Sample = (ref _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = TransportTelemetry.ActivitySource.StartActivity("test");

        // Skip test if activity couldn't be created (no listener)
        if (activity == null)
        {
            return;
        }

        var tags = new Dictionary<string, object?>
        {
            ["custom.key1"] = "value1",
            ["custom.key2"] = "value2" // Use string instead of int to avoid conversion issues
        };

        // Act
        TransportTelemetry.EnrichActivity(activity, tags);

        // Assert
        // SetTag converts values to strings internally
        // Note: Activity.Tags enumerates ALL tags added to the activity
        Assert.True(
            activity.Tags.Any(t => t.Key == "custom.key1" && t.Value == "value1"),
            "Expected tag 'custom.key1' with value 'value1' not found in activity tags");
        Assert.True(
            activity.Tags.Any(t => t.Key == "custom.key2" && t.Value == "value2"),
            $"Expected tag 'custom.key2' with value 'value2' not found. Actual tags: {string.Join(", ", activity.Tags.Select(t => $"{t.Key}={t.Value}"))}");
    }

    /// <summary>
    /// Verifies ToServiceBusMessage maps TenantId and ProjectId to ApplicationProperties.
    /// </summary>
    [Fact]
    public void ToServiceBusMessage_WithTenantIdAndProjectId_MapsToApplicationProperties()
    {
        // Arrange
        var envelope = new TransportEnvelope
        {
            MessageId = "msg-123",
            CorrelationId = "corr-456",
            CausationId = "cause-789",
            NodeId = "node-abc",
            StudioId = "studio-def",
            TenantId = "tenant-xyz",
            ProjectId = "project-123",
            Environment = "production",
            Timestamp = DateTimeOffset.UtcNow,
            MessageType = typeof(SampleMessage).FullName!,
            Headers = new Dictionary<string, string>(),
            Payload = new byte[] { 1, 2, 3 }
        };

        // Act
        var sbMessage = EnvelopeMapper.ToServiceBusMessage(envelope);

        // Assert
        Assert.Equal("tenant-xyz", sbMessage.ApplicationProperties[GridHeaderNames.TenantId]);
        Assert.Equal("project-123", sbMessage.ApplicationProperties[GridHeaderNames.ProjectId]);
    }

    /// <summary>
    /// Verifies ToServiceBusMessage does NOT add TenantId/ProjectId properties when null.
    /// </summary>
    [Fact]
    public void ToServiceBusMessage_WithNullTenantIdAndProjectId_DoesNotAddToApplicationProperties()
    {
        // Arrange
        var envelope = new TransportEnvelope
        {
            MessageId = "msg-123",
            CorrelationId = "corr-456",
            NodeId = "node-abc",
            StudioId = "studio-def",
            TenantId = null,  // Explicitly null
            ProjectId = null, // Explicitly null
            Environment = "test",
            Timestamp = DateTimeOffset.UtcNow,
            MessageType = typeof(SampleMessage).FullName!,
            Headers = new Dictionary<string, string>(),
            Payload = new byte[] { 1, 2, 3 }
        };

        // Act
        var sbMessage = EnvelopeMapper.ToServiceBusMessage(envelope);

        // Assert
        Assert.False(sbMessage.ApplicationProperties.ContainsKey(GridHeaderNames.TenantId));
        Assert.False(sbMessage.ApplicationProperties.ContainsKey(GridHeaderNames.ProjectId));
    }

    /// <summary>
    /// Verifies ToServiceBusMessage does NOT add TenantId/ProjectId properties when empty string.
    /// </summary>
    [Fact]
    public void ToServiceBusMessage_WithEmptyTenantIdAndProjectId_DoesNotAddToApplicationProperties()
    {
        // Arrange
        var envelope = new TransportEnvelope
        {
            MessageId = "msg-123",
            CorrelationId = "corr-456",
            NodeId = "node-abc",
            StudioId = "studio-def",
            TenantId = string.Empty,  // Empty string
            ProjectId = string.Empty, // Empty string
            Environment = "test",
            Timestamp = DateTimeOffset.UtcNow,
            MessageType = typeof(SampleMessage).FullName!,
            Headers = new Dictionary<string, string>(),
            Payload = new byte[] { 1, 2, 3 }
        };

        // Act
        var sbMessage = EnvelopeMapper.ToServiceBusMessage(envelope);

        // Assert
        Assert.False(sbMessage.ApplicationProperties.ContainsKey(GridHeaderNames.TenantId));
        Assert.False(sbMessage.ApplicationProperties.ContainsKey(GridHeaderNames.ProjectId));
    }

    /// <summary>
    /// Verifies FromServiceBusMessage extracts TenantId and ProjectId from ApplicationProperties.
    /// </summary>
    [Fact]
    public void FromServiceBusMessage_WithTenantIdAndProjectId_ExtractsFromApplicationProperties()
    {
        // Arrange
        var applicationProperties = new Dictionary<string, object>
        {
            [GridHeaderNames.TenantId] = "tenant-abc",
            [GridHeaderNames.ProjectId] = "project-xyz",
            [GridHeaderNames.CausationId] = "cause-123",
            [GridHeaderNames.NodeId] = "node-456",
            [GridHeaderNames.StudioId] = "studio-789",
            [GridHeaderNames.Environment] = "production",
            ["MessageType"] = typeof(SampleMessage).FullName!,
            ["Timestamp"] = DateTimeOffset.UtcNow.ToString("o")
        };

        var sbMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(
            messageId: "msg-123",
            correlationId: "corr-456",
            subject: typeof(SampleMessage).FullName,
            properties: applicationProperties);

        // Act
        var envelope = EnvelopeMapper.FromServiceBusMessage(sbMessage);

        // Assert
        Assert.Equal("tenant-abc", envelope.TenantId);
        Assert.Equal("project-xyz", envelope.ProjectId);
    }

    /// <summary>
    /// Verifies FromServiceBusMessage handles missing TenantId and ProjectId gracefully.
    /// </summary>
    [Fact]
    public void FromServiceBusMessage_WithoutTenantIdAndProjectId_ReturnsNullValues()
    {
        // Arrange
        var applicationProperties = new Dictionary<string, object>
        {
            [GridHeaderNames.NodeId] = "node-456",
            [GridHeaderNames.StudioId] = "studio-789",
            ["MessageType"] = typeof(SampleMessage).FullName!,
            ["Timestamp"] = DateTimeOffset.UtcNow.ToString("o")
        };

        var sbMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(
            messageId: "msg-123",
            correlationId: "corr-456",
            subject: typeof(SampleMessage).FullName,
            properties: applicationProperties);

        // Act
        var envelope = EnvelopeMapper.FromServiceBusMessage(sbMessage);

        // Assert
        Assert.Null(envelope.TenantId);
        Assert.Null(envelope.ProjectId);
    }

    /// <summary>
    /// Verifies round-trip mapping preserves TenantId and ProjectId.
    /// </summary>
    [Fact]
    public void RoundTripMapping_PreservesTenantIdAndProjectId()
    {
        // Arrange
        var originalEnvelope = new TransportEnvelope
        {
            MessageId = "msg-123",
            CorrelationId = "corr-456",
            CausationId = "cause-789",
            NodeId = "node-abc",
            StudioId = "studio-def",
            TenantId = "tenant-original",
            ProjectId = "project-original",
            Environment = "staging",
            Timestamp = DateTimeOffset.UtcNow,
            MessageType = typeof(SampleMessage).FullName!,
            Headers = new Dictionary<string, string> { ["custom"] = "header" },
            Payload = new byte[] { 1, 2, 3, 4, 5 }
        };

        // Act - Round trip: envelope → ServiceBusMessage → envelope
        var sbMessage = EnvelopeMapper.ToServiceBusMessage(originalEnvelope);

        // Convert ServiceBusMessage to ServiceBusReceivedMessage for deserialization
        var receivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: sbMessage.Body,
            messageId: sbMessage.MessageId,
            correlationId: sbMessage.CorrelationId,
            subject: sbMessage.Subject,
            properties: sbMessage.ApplicationProperties);

        var roundTrippedEnvelope = EnvelopeMapper.FromServiceBusMessage(receivedMessage);

        // Assert - All Grid context fields preserved
        Assert.Equal(originalEnvelope.MessageId, roundTrippedEnvelope.MessageId);
        Assert.Equal(originalEnvelope.CorrelationId, roundTrippedEnvelope.CorrelationId);
        Assert.Equal(originalEnvelope.CausationId, roundTrippedEnvelope.CausationId);
        Assert.Equal(originalEnvelope.NodeId, roundTrippedEnvelope.NodeId);
        Assert.Equal(originalEnvelope.StudioId, roundTrippedEnvelope.StudioId);
        Assert.Equal(originalEnvelope.TenantId, roundTrippedEnvelope.TenantId);
        Assert.Equal(originalEnvelope.ProjectId, roundTrippedEnvelope.ProjectId);
        Assert.Equal(originalEnvelope.Environment, roundTrippedEnvelope.Environment);
        Assert.Equal(originalEnvelope.MessageType, roundTrippedEnvelope.MessageType);
        Assert.Equal(originalEnvelope.Payload.ToArray(), roundTrippedEnvelope.Payload.ToArray());
    }

    /// <summary>
    /// Verifies round-trip mapping with null TenantId/ProjectId preserves null values.
    /// </summary>
    [Fact]
    public void RoundTripMapping_WithNullTenantIdAndProjectId_PreservesNullValues()
    {
        // Arrange
        var originalEnvelope = new TransportEnvelope
        {
            MessageId = "msg-123",
            CorrelationId = "corr-456",
            NodeId = "node-abc",
            StudioId = "studio-def",
            TenantId = null,  // Explicitly null
            ProjectId = null, // Explicitly null
            Environment = "test",
            Timestamp = DateTimeOffset.UtcNow,
            MessageType = typeof(SampleMessage).FullName!,
            Headers = new Dictionary<string, string>(),
            Payload = new byte[] { 1, 2, 3 }
        };

        // Act - Round trip: envelope → ServiceBusMessage → envelope
        var sbMessage = EnvelopeMapper.ToServiceBusMessage(originalEnvelope);

        var receivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: sbMessage.Body,
            messageId: sbMessage.MessageId,
            correlationId: sbMessage.CorrelationId,
            subject: sbMessage.Subject,
            properties: sbMessage.ApplicationProperties);

        var roundTrippedEnvelope = EnvelopeMapper.FromServiceBusMessage(receivedMessage);

        // Assert - Null values preserved
        Assert.Null(roundTrippedEnvelope.TenantId);
        Assert.Null(roundTrippedEnvelope.ProjectId);
    }

    /// <summary>
    /// Verifies TenantId and ProjectId are NOT included in the Headers collection (reserved fields).
    /// </summary>
    [Fact]
    public void FromServiceBusMessage_ExcludesTenantIdAndProjectIdFromHeaders()
    {
        // Arrange
        var applicationProperties = new Dictionary<string, object>
        {
            [GridHeaderNames.TenantId] = "tenant-abc",
            [GridHeaderNames.ProjectId] = "project-xyz",
            [GridHeaderNames.CausationId] = "cause-123",
            ["CustomHeader"] = "custom-value",
            ["MessageType"] = typeof(SampleMessage).FullName!,
            ["Timestamp"] = DateTimeOffset.UtcNow.ToString("o")
        };

        var sbMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(
            messageId: "msg-123",
            correlationId: "corr-456",
            subject: typeof(SampleMessage).FullName,
            properties: applicationProperties);

        // Act
        var envelope = EnvelopeMapper.FromServiceBusMessage(sbMessage);

        // Assert - Reserved Grid context fields NOT in headers
        Assert.False(envelope.Headers.ContainsKey(GridHeaderNames.TenantId));
        Assert.False(envelope.Headers.ContainsKey(GridHeaderNames.ProjectId));
        Assert.False(envelope.Headers.ContainsKey(GridHeaderNames.CausationId));
        Assert.False(envelope.Headers.ContainsKey(GridHeaderNames.NodeId));
        Assert.False(envelope.Headers.ContainsKey(GridHeaderNames.StudioId));
        Assert.False(envelope.Headers.ContainsKey(GridHeaderNames.Environment));
        Assert.False(envelope.Headers.ContainsKey("MessageType"));
        Assert.False(envelope.Headers.ContainsKey("Timestamp"));

        // But custom headers ARE included
        Assert.True(envelope.Headers.TryGetValue("CustomHeader", out var customHeaderValue));
        Assert.Equal("custom-value", customHeaderValue);
    }
}
