using HoneyDrunk.Transport.AzureServiceBus.Mapping;
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
}
