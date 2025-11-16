using HoneyDrunk.Transport.AzureServiceBus.Mapping;
using HoneyDrunk.Transport.Telemetry;
using HoneyDrunk.Transport.Tests.Support;

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
        var activity = TransportTelemetry.ActivitySource.StartActivity("test");
        var tags = new Dictionary<string, object?>
        {
            ["custom.key1"] = "value1",
            ["custom.key2"] = 123
        };

        // Act
        TransportTelemetry.EnrichActivity(activity, tags);

        // Assert
        if (activity != null)
        {
            Assert.Contains(activity.Tags, t => t.Key == "custom.key1" && t.Value == "value1");
            Assert.Contains(activity.Tags, t => t.Key == "custom.key2" && t.Value == "123");
            activity.Dispose();
        }
    }
}
