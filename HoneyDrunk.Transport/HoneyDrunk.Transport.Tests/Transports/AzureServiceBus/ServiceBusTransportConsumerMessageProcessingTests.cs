using Azure.Messaging.ServiceBus;
using HoneyDrunk.Transport.AzureServiceBus;
using HoneyDrunk.Transport.AzureServiceBus.Configuration;
using HoneyDrunk.Transport.AzureServiceBus.Mapping;
using HoneyDrunk.Transport.Pipeline;
using HoneyDrunk.Transport.Primitives;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace HoneyDrunk.Transport.Tests.Transports.AzureServiceBus;

/// <summary>
/// Tests for ServiceBusTransportConsumer message processing through EnvelopeMapper integration.
/// Since Azure SDK types are sealed, we test the mappable components.
/// </summary>
public sealed class ServiceBusTransportConsumerMessageProcessingTests
{
    /// <summary>
    /// EnvelopeMapper correctly extracts delivery count from ServiceBusReceivedMessage.
    /// </summary>
    [Fact]
    public void EnvelopeMapper_GetDeliveryCount_ReturnsCorrectCount()
    {
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString("{}"),
            messageId: "test-1",
            deliveryCount: 5);

        var deliveryCount = EnvelopeMapper.GetDeliveryCount(message);

        Assert.Equal(5, deliveryCount);
    }

    /// <summary>
    /// EnvelopeMapper creates transaction from ServiceBusReceivedMessage.
    /// </summary>
    [Fact]
    public void EnvelopeMapper_CreateTransaction_ReturnsValidTransaction()
    {
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString("{}"),
            messageId: "test-2");

        var transaction = EnvelopeMapper.CreateTransaction(message);

        Assert.NotNull(transaction);
        Assert.Equal("test-2", transaction.TransactionId);
    }

    /// <summary>
    /// EnvelopeMapper converts ServiceBusMessage to TransportEnvelope with all properties.
    /// </summary>
    [Fact]
    public void EnvelopeMapper_FromServiceBusMessage_MapsAllProperties()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var properties = new Dictionary<string, object>
        {
            ["MessageType"] = "Test.Message",
            ["Timestamp"] = timestamp.ToString("o"),
            ["CausationId"] = "causation-123",
            ["CustomHeader"] = "custom-value"
        };

        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString("{\"test\":\"data\"}"),
            messageId: "msg-123",
            correlationId: "corr-456",
            subject: "Test.Subject",
            properties: properties);

        var envelope = EnvelopeMapper.FromServiceBusMessage(message);

        Assert.Equal("msg-123", envelope.MessageId);
        Assert.Equal("corr-456", envelope.CorrelationId);
        Assert.Equal("causation-123", envelope.CausationId);
        Assert.Equal("Test.Message", envelope.MessageType);
        Assert.Contains("CustomHeader", envelope.Headers.Keys);
        Assert.Equal("custom-value", envelope.Headers["CustomHeader"]);
        Assert.DoesNotContain("MessageType", envelope.Headers.Keys); // Reserved property excluded
        Assert.DoesNotContain("Timestamp", envelope.Headers.Keys); // Reserved property excluded
        Assert.DoesNotContain("CausationId", envelope.Headers.Keys); // Reserved property excluded
    }

    /// <summary>
    /// EnvelopeMapper handles missing optional properties gracefully.
    /// </summary>
    [Fact]
    public void EnvelopeMapper_FromServiceBusMessage_HandlesNullProperties()
    {
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString("{}"),
            messageId: "msg-no-props");

        var envelope = EnvelopeMapper.FromServiceBusMessage(message);

        Assert.Equal("msg-no-props", envelope.MessageId);
        Assert.Null(envelope.CausationId);
        Assert.NotNull(envelope.Headers);
        Assert.Empty(envelope.Headers);
    }

    /// <summary>
    /// EnvelopeMapper uses default timestamp when not provided in message.
    /// </summary>
    [Fact]
    public void EnvelopeMapper_FromServiceBusMessage_UsesDefaultTimestampWhenNotProvided()
    {
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString("{}"),
            messageId: "msg-no-timestamp");

        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        var envelope = EnvelopeMapper.FromServiceBusMessage(message);
        var after = DateTimeOffset.UtcNow.AddSeconds(1);

        // Should use current UtcNow when timestamp not provided
        Assert.InRange(envelope.Timestamp, before, after);
    }

    /// <summary>
    /// EnvelopeMapper parses valid timestamp from message properties.
    /// </summary>
    [Fact]
    public void EnvelopeMapper_FromServiceBusMessage_ParsesValidTimestamp()
    {
        var expectedTimestamp = new DateTimeOffset(2025, 1, 15, 10, 30, 45, TimeSpan.Zero);
        var properties = new Dictionary<string, object>
        {
            ["Timestamp"] = expectedTimestamp.ToString("o")
        };

        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString("{}"),
            messageId: "msg-with-timestamp",
            properties: properties);

        var envelope = EnvelopeMapper.FromServiceBusMessage(message);

        // Should parse the provided timestamp
        Assert.Equal(expectedTimestamp, envelope.Timestamp);
    }

    /// <summary>
    /// EnvelopeMapper converts TransportEnvelope to ServiceBusMessage with all properties.
    /// </summary>
    [Fact]
    public void EnvelopeMapper_ToServiceBusMessage_MapsAllProperties()
    {
        var envelope = new TransportEnvelope
        {
            MessageId = "env-123",
            CorrelationId = "env-corr-456",
            CausationId = "env-cause-789",
            Timestamp = DateTimeOffset.UtcNow,
            MessageType = "Test.Envelope",
            Payload = BinaryData.FromString("{\"data\":\"value\"}").ToMemory(),
            Headers = new Dictionary<string, string>
            {
                ["Header1"] = "Value1",
                ["Header2"] = "Value2"
            }
        };

        var message = EnvelopeMapper.ToServiceBusMessage(envelope);

        Assert.Equal("env-123", message.MessageId);
        Assert.Equal("env-corr-456", message.CorrelationId);
        Assert.Equal("Test.Envelope", message.Subject);
        Assert.Equal("application/octet-stream", message.ContentType);
        Assert.Equal("Test.Envelope", message.ApplicationProperties["MessageType"]);
        Assert.Equal("env-cause-789", message.ApplicationProperties["CausationId"]);
        Assert.Equal("Value1", message.ApplicationProperties["Header1"]);
        Assert.Equal("Value2", message.ApplicationProperties["Header2"]);
    }

    /// <summary>
    /// EnvelopeMapper omits CausationId property when null or empty.
    /// </summary>
    [Fact]
    public void EnvelopeMapper_ToServiceBusMessage_OmitsCausationIdWhenEmpty()
    {
        var envelope = new TransportEnvelope
        {
            MessageId = "env-no-causation",
            CorrelationId = "corr",
            CausationId = null,
            MessageType = "Test.Type",
            Payload = BinaryData.FromString("{}").ToMemory(),
            Headers = new Dictionary<string, string>()
        };

        var message = EnvelopeMapper.ToServiceBusMessage(envelope);

        Assert.DoesNotContain("CausationId", message.ApplicationProperties.Keys);
    }

    /// <summary>
    /// ServiceBusTransportTransaction contains correct context data.
    /// </summary>
    [Fact]
    public void ServiceBusTransportTransaction_ContainsExpectedContext()
    {
        var enqueuedTime = DateTimeOffset.UtcNow.AddMinutes(-5);
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString("{}"),
            messageId: "txn-test",
            sequenceNumber: 12345,
            enqueuedTime: enqueuedTime);

        var transaction = EnvelopeMapper.CreateTransaction(message);

        Assert.Equal("txn-test", transaction.TransactionId);
        Assert.Contains("ServiceBusMessage", transaction.Context.Keys);
        Assert.Contains("LockToken", transaction.Context.Keys);
        Assert.Contains("SequenceNumber", transaction.Context.Keys);
        Assert.Contains("EnqueuedTime", transaction.Context.Keys);
        Assert.Equal(12345L, transaction.Context["SequenceNumber"]);
    }

    /// <summary>
    /// ServiceBusTransportTransaction CommitAsync completes successfully.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task ServiceBusTransportTransaction_CommitAsync_Succeeds()
    {
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString("{}"),
            messageId: "commit-test");

        var transaction = EnvelopeMapper.CreateTransaction(message);

        // Should not throw
        await transaction.CommitAsync();
    }

    /// <summary>
    /// ServiceBusTransportTransaction RollbackAsync completes successfully.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task ServiceBusTransportTransaction_RollbackAsync_Succeeds()
    {
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString("{}"),
            messageId: "rollback-test");

        var transaction = EnvelopeMapper.CreateTransaction(message);

        // Should not throw
        await transaction.RollbackAsync();
    }

    /// <summary>
    /// Consumer correctly starts with Debug logging disabled.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task StartAsync_DebugLoggingDisabled_NoDebugLogs()
    {
        var asb = new AzureServiceBusOptions
        {
            Address = "orders",
            EntityType = ServiceBusEntityType.Queue,
        };
        var options = new TestOptions(asb);

        var client = Substitute.For<ServiceBusClient>();
        var pipeline = Substitute.For<IMessagePipeline>();
        var logger = Substitute.For<ILogger<ServiceBusTransportConsumer>>();
        logger.IsEnabled(LogLevel.Debug).Returns(false);
        logger.IsEnabled(LogLevel.Information).Returns(false);  // Disable info logging too

        var processor = Substitute.For<ServiceBusProcessor>();
        client.CreateProcessor("orders", Arg.Any<ServiceBusProcessorOptions>()).Returns(processor);
        processor.StartProcessingAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        await using var consumer = new ServiceBusTransportConsumer(client, pipeline, options, logger);
        await consumer.StartAsync();

        // Verify Debug was NOT logged
        logger.DidNotReceive().Log(
            LogLevel.Debug,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    /// <summary>
    /// Consumer correctly starts with Information logging enabled.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task StartAsync_InformationLoggingEnabled_LogsStartup()
    {
        var asb = new AzureServiceBusOptions
        {
            Address = "orders",
            EntityType = ServiceBusEntityType.Queue,
            EndpointName = "my-endpoint"
        };
        var options = new TestOptions(asb);

        var client = Substitute.For<ServiceBusClient>();
        var pipeline = Substitute.For<IMessagePipeline>();
        var logger = Substitute.For<ILogger<ServiceBusTransportConsumer>>();
        logger.IsEnabled(LogLevel.Information).Returns(true);

        var processor = Substitute.For<ServiceBusProcessor>();
        client.CreateProcessor("orders", Arg.Any<ServiceBusProcessorOptions>()).Returns(processor);
        processor.StartProcessingAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        await using var consumer = new ServiceBusTransportConsumer(client, pipeline, options, logger);
        await consumer.StartAsync();

        // Verify Information logs were made
        logger.Received(2).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    /// <summary>
    /// StopAsync logs when Information logging is enabled.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task StopAsync_InformationLoggingEnabled_LogsShutdown()
    {
        var asb = new AzureServiceBusOptions
        {
            Address = "orders",
            EntityType = ServiceBusEntityType.Queue,
        };
        var options = new TestOptions(asb);

        var client = Substitute.For<ServiceBusClient>();
        var pipeline = Substitute.For<IMessagePipeline>();
        var logger = Substitute.For<ILogger<ServiceBusTransportConsumer>>();
        logger.IsEnabled(LogLevel.Information).Returns(true);

        var processor = Substitute.For<ServiceBusProcessor>();
        client.CreateProcessor("orders", Arg.Any<ServiceBusProcessorOptions>()).Returns(processor);
        processor.StartProcessingAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        processor.StopProcessingAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        await using var consumer = new ServiceBusTransportConsumer(client, pipeline, options, logger);
        await consumer.StartAsync();
        await consumer.StopAsync();

        // Verify Stop logs (2 from start, 2 from stop)
        logger.Received(4).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    /// <summary>
    /// EnvelopeMapper handles invalid timestamp format gracefully.
    /// </summary>
    [Fact]
    public void EnvelopeMapper_FromServiceBusMessage_InvalidTimestamp_UsesDefaultTimestamp()
    {
        var properties = new Dictionary<string, object>
        {
            ["Timestamp"] = "not-a-valid-timestamp"
        };

        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString("{}"),
            messageId: "msg-invalid-timestamp",
            properties: properties);

        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        var envelope = EnvelopeMapper.FromServiceBusMessage(message);
        var after = DateTimeOffset.UtcNow.AddSeconds(1);

        // Should fall back to current time when timestamp parsing fails
        Assert.InRange(envelope.Timestamp, before, after);
    }

    /// <summary>
    /// EnvelopeMapper handles non-string timestamp property gracefully.
    /// </summary>
    [Fact]
    public void EnvelopeMapper_FromServiceBusMessage_NonStringTimestamp_UsesDefaultTimestamp()
    {
        var properties = new Dictionary<string, object>
        {
            ["Timestamp"] = 12345 // Integer instead of string
        };

        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString("{}"),
            messageId: "msg-nonstring-timestamp",
            properties: properties);

        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        var envelope = EnvelopeMapper.FromServiceBusMessage(message);
        var after = DateTimeOffset.UtcNow.AddSeconds(1);

        // Should fall back to current time
        Assert.True(envelope.Timestamp >= before && envelope.Timestamp <= after);
    }

    /// <summary>
    /// EnvelopeMapper handles message with subject but no MessageType property.
    /// </summary>
    [Fact]
    public void EnvelopeMapper_FromServiceBusMessage_UsesSubjectWhenNoMessageType()
    {
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString("{}"),
            messageId: "msg-subject-only",
            subject: "SubjectAsMessageType");

        var envelope = EnvelopeMapper.FromServiceBusMessage(message);

        Assert.Equal("SubjectAsMessageType", envelope.MessageType);
    }

    /// <summary>
    /// EnvelopeMapper prefers MessageType property over subject.
    /// </summary>
    [Fact]
    public void EnvelopeMapper_FromServiceBusMessage_PrefersMessageTypeOverSubject()
    {
        var properties = new Dictionary<string, object>
        {
            ["MessageType"] = "PropertyMessageType"
        };

        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString("{}"),
            messageId: "msg-both-types",
            subject: "SubjectMessageType",
            properties: properties);

        var envelope = EnvelopeMapper.FromServiceBusMessage(message);

        Assert.Equal("PropertyMessageType", envelope.MessageType);
    }

    /// <summary>
    /// EnvelopeMapper handles non-string property values gracefully.
    /// </summary>
    [Fact]
    public void EnvelopeMapper_FromServiceBusMessage_ConvertsNonStringProperties()
    {
        var properties = new Dictionary<string, object>
        {
            ["IntProperty"] = 42,
            ["BoolProperty"] = true,
            ["DoubleProperty"] = 3.14
        };

        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString("{}"),
            messageId: "msg-mixed-properties",
            properties: properties);

        var envelope = EnvelopeMapper.FromServiceBusMessage(message);

        Assert.Equal("42", envelope.Headers["IntProperty"]);
        Assert.Equal("True", envelope.Headers["BoolProperty"]);
        Assert.Equal("3.14", envelope.Headers["DoubleProperty"]);
    }

    /// <summary>
    /// EnvelopeMapper filters out reserved properties from headers.
    /// </summary>
    [Fact]
    public void EnvelopeMapper_FromServiceBusMessage_FiltersReservedProperties()
    {
        var properties = new Dictionary<string, object>
        {
            ["MessageType"] = "Test.Type",
            ["Timestamp"] = DateTimeOffset.UtcNow.ToString("o"),
            ["CausationId"] = "cause-123",
            ["CorrelationId"] = "corr-456", // Also reserved
            ["CustomHeader"] = "custom"
        };

        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString("{}"),
            messageId: "msg-reserved",
            properties: properties);

        var envelope = EnvelopeMapper.FromServiceBusMessage(message);

        // Reserved properties should not be in headers
        Assert.DoesNotContain("MessageType", envelope.Headers.Keys);
        Assert.DoesNotContain("Timestamp", envelope.Headers.Keys);
        Assert.DoesNotContain("CausationId", envelope.Headers.Keys);

        // Custom header should be present
        Assert.Contains("CustomHeader", envelope.Headers.Keys);
        Assert.Equal("custom", envelope.Headers["CustomHeader"]);
    }

    /// <summary>
    /// EnvelopeMapper handles null causation ID in properties.
    /// </summary>
    [Fact]
    public void EnvelopeMapper_FromServiceBusMessage_NullCausationId()
    {
        var properties = new Dictionary<string, object>
        {
            ["CausationId"] = null!
        };

        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString("{}"),
            messageId: "msg-null-causation",
            properties: properties);

        var envelope = EnvelopeMapper.FromServiceBusMessage(message);

        Assert.Null(envelope.CausationId);
    }

    /// <summary>
    /// EnvelopeMapper handles empty string causation ID.
    /// </summary>
    [Fact]
    public void EnvelopeMapper_FromServiceBusMessage_EmptyStringCausationId()
    {
        var properties = new Dictionary<string, object>
        {
            ["CausationId"] = string.Empty
        };

        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString("{}"),
            messageId: "msg-empty-causation",
            properties: properties);

        var envelope = EnvelopeMapper.FromServiceBusMessage(message);

        // Empty string should be treated as null
        Assert.True(string.IsNullOrEmpty(envelope.CausationId));
    }

    /// <summary>
    /// ToServiceBusMessage handles empty headers correctly.
    /// Note: MessageType and Timestamp are always added to ApplicationProperties.
    /// </summary>
    [Fact]
    public void EnvelopeMapper_ToServiceBusMessage_EmptyHeaders()
    {
        var envelope = new TransportEnvelope
        {
            MessageId = "env-empty-headers",
            CorrelationId = "corr",
            MessageType = "Test.Type",
            Payload = BinaryData.FromString("{}").ToMemory(),
            Headers = new Dictionary<string, string>()
        };

        var message = EnvelopeMapper.ToServiceBusMessage(envelope);

        // Should have MessageType and Timestamp in application properties (always added)
        Assert.Equal(2, message.ApplicationProperties.Count);
        Assert.Equal("Test.Type", message.ApplicationProperties["MessageType"]);
        Assert.Contains("Timestamp", message.ApplicationProperties.Keys);
    }

    /// <summary>
    /// ToServiceBusMessage includes CausationId when present.
    /// </summary>
    [Fact]
    public void EnvelopeMapper_ToServiceBusMessage_IncludesCausationIdWhenPresent()
    {
        var envelope = new TransportEnvelope
        {
            MessageId = "env-with-causation",
            CorrelationId = "corr",
            CausationId = "cause-123",
            MessageType = "Test.Type",
            Payload = BinaryData.FromString("{}").ToMemory(),
            Headers = new Dictionary<string, string>()
        };

        var message = EnvelopeMapper.ToServiceBusMessage(envelope);

        Assert.Contains("CausationId", message.ApplicationProperties.Keys);
        Assert.Equal("cause-123", message.ApplicationProperties["CausationId"]);
    }

    /// <summary>
    /// ToServiceBusMessage omits CausationId when empty string.
    /// </summary>
    [Fact]
    public void EnvelopeMapper_ToServiceBusMessage_OmitsCausationIdWhenEmptyString()
    {
        var envelope = new TransportEnvelope
        {
            MessageId = "env-empty-causation",
            CorrelationId = "corr",
            CausationId = string.Empty,
            MessageType = "Test.Type",
            Payload = BinaryData.FromString("{}").ToMemory(),
            Headers = new Dictionary<string, string>()
        };

        var message = EnvelopeMapper.ToServiceBusMessage(envelope);

        Assert.DoesNotContain("CausationId", message.ApplicationProperties.Keys);
    }

    /// <summary>
    /// GetDeliveryCount handles zero delivery count.
    /// </summary>
    [Fact]
    public void EnvelopeMapper_GetDeliveryCount_HandlesZero()
    {
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString("{}"),
            messageId: "test-zero",
            deliveryCount: 0);

        var deliveryCount = EnvelopeMapper.GetDeliveryCount(message);

        Assert.Equal(0, deliveryCount);
    }

    /// <summary>
    /// CreateTransaction includes lock token in context.
    /// </summary>
    [Fact]
    public void EnvelopeMapper_CreateTransaction_IncludesLockToken()
    {
        var lockToken = Guid.NewGuid().ToString();
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString("{}"),
            messageId: "txn-lock",
            lockTokenGuid: Guid.Parse(lockToken));

        var transaction = EnvelopeMapper.CreateTransaction(message);

        Assert.Contains("LockToken", transaction.Context.Keys);
        Assert.Equal(lockToken, transaction.Context["LockToken"]);
    }

    /// <summary>
    /// CreateTransaction includes enqueued time in context.
    /// </summary>
    [Fact]
    public void EnvelopeMapper_CreateTransaction_IncludesEnqueuedTime()
    {
        var enqueuedTime = DateTimeOffset.UtcNow.AddHours(-1);
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString("{}"),
            messageId: "txn-enqueued",
            enqueuedTime: enqueuedTime);

        var transaction = EnvelopeMapper.CreateTransaction(message);

        Assert.Contains("EnqueuedTime", transaction.Context.Keys);
        Assert.Equal(enqueuedTime, transaction.Context["EnqueuedTime"]);
    }

    private sealed class TestOptions(AzureServiceBusOptions value) : IOptions<AzureServiceBusOptions>
    {
        public AzureServiceBusOptions Value { get; } = value;
    }
}
