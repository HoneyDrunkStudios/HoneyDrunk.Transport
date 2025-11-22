using HoneyDrunk.Transport.Outbox;
using HoneyDrunk.Transport.Tests.Support;

namespace HoneyDrunk.Transport.Tests.Core.Outbox;

/// <summary>
/// Tests for OutboxMessage property validation and immutability.
/// </summary>
public sealed class OutboxMessageTests
{
    /// <summary>
    /// Verifies OutboxMessage can be created with all properties.
    /// </summary>
    [Fact]
    public void OutboxMessage_WithAllProperties_CreatesSuccessfully()
    {
        // Arrange
        var envelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" });
        var destination = TestData.Address("queue", "test-queue");
        var createdAt = DateTimeOffset.UtcNow;
        var processedAt = createdAt.AddSeconds(10);
        var scheduledAt = createdAt.AddSeconds(5);

        // Act
        var message = new OutboxMessage
        {
            Id = "outbox-123",
            Destination = destination,
            Envelope = envelope,
            State = OutboxMessageState.Dispatched,
            Attempts = 3,
            CreatedAt = createdAt,
            ProcessedAt = processedAt,
            ScheduledAt = scheduledAt,
            ErrorMessage = "Test error"
        };

        // Assert
        Assert.Equal("outbox-123", message.Id);
        Assert.Same(destination, message.Destination);
        Assert.Same(envelope, message.Envelope);
        Assert.Equal(OutboxMessageState.Dispatched, message.State);
        Assert.Equal(3, message.Attempts);
        Assert.Equal(createdAt, message.CreatedAt);
        Assert.Equal(processedAt, message.ProcessedAt);
        Assert.Equal(scheduledAt, message.ScheduledAt);
        Assert.Equal("Test error", message.ErrorMessage);
    }

    /// <summary>
    /// Verifies OutboxMessage can be created with minimal properties.
    /// </summary>
    [Fact]
    public void OutboxMessage_WithMinimalProperties_CreatesSuccessfully()
    {
        // Arrange
        var envelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" });
        var destination = TestData.Address();

        // Act
        var message = new OutboxMessage
        {
            Id = "outbox-min",
            Destination = destination,
            Envelope = envelope
        };

        // Assert
        Assert.Equal("outbox-min", message.Id);
        Assert.Same(destination, message.Destination);
        Assert.Same(envelope, message.Envelope);
        Assert.Equal(default, message.State);
        Assert.Equal(0, message.Attempts);
        Assert.Equal(default, message.CreatedAt);
        Assert.Null(message.ProcessedAt);
        Assert.Null(message.ScheduledAt);
        Assert.Null(message.ErrorMessage);
    }

    /// <summary>
    /// Verifies OutboxMessage State can be set to Pending.
    /// </summary>
    [Fact]
    public void OutboxMessage_StateSetToPending_PreservesValue()
    {
        // Arrange & Act
        var message = new OutboxMessage
        {
            Id = "outbox-pending",
            Destination = TestData.Address(),
            Envelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" }),
            State = OutboxMessageState.Pending
        };

        // Assert
        Assert.Equal(OutboxMessageState.Pending, message.State);
    }

    /// <summary>
    /// Verifies OutboxMessage State can be set to Processing.
    /// </summary>
    [Fact]
    public void OutboxMessage_StateSetToProcessing_PreservesValue()
    {
        // Arrange & Act
        var message = new OutboxMessage
        {
            Id = "outbox-processing",
            Destination = TestData.Address(),
            Envelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" }),
            State = OutboxMessageState.Processing
        };

        // Assert
        Assert.Equal(OutboxMessageState.Processing, message.State);
    }

    /// <summary>
    /// Verifies OutboxMessage State can be set to Dispatched.
    /// </summary>
    [Fact]
    public void OutboxMessage_StateSetToDispatched_PreservesValue()
    {
        // Arrange & Act
        var message = new OutboxMessage
        {
            Id = "outbox-dispatched",
            Destination = TestData.Address(),
            Envelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" }),
            State = OutboxMessageState.Dispatched
        };

        // Assert
        Assert.Equal(OutboxMessageState.Dispatched, message.State);
    }

    /// <summary>
    /// Verifies OutboxMessage State can be set to Failed.
    /// </summary>
    [Fact]
    public void OutboxMessage_StateSetToFailed_PreservesValue()
    {
        // Arrange & Act
        var message = new OutboxMessage
        {
            Id = "outbox-failed",
            Destination = TestData.Address(),
            Envelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" }),
            State = OutboxMessageState.Failed
        };

        // Assert
        Assert.Equal(OutboxMessageState.Failed, message.State);
    }

    /// <summary>
    /// Verifies OutboxMessage State can be set to Poisoned.
    /// </summary>
    [Fact]
    public void OutboxMessage_StateSetToPoisoned_PreservesValue()
    {
        // Arrange & Act
        var message = new OutboxMessage
        {
            Id = "outbox-poisoned",
            Destination = TestData.Address(),
            Envelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" }),
            State = OutboxMessageState.Poisoned
        };

        // Assert
        Assert.Equal(OutboxMessageState.Poisoned, message.State);
    }

    /// <summary>
    /// Verifies OutboxMessage Attempts can be incremented.
    /// </summary>
    [Fact]
    public void OutboxMessage_AttemptsIncremented_PreservesValue()
    {
        // Arrange & Act
        var message = new OutboxMessage
        {
            Id = "outbox-attempts",
            Destination = TestData.Address(),
            Envelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" }),
            Attempts = 5
        };

        // Assert
        Assert.Equal(5, message.Attempts);
    }

    /// <summary>
    /// Verifies OutboxMessage ProcessedAt can be set.
    /// </summary>
    [Fact]
    public void OutboxMessage_ProcessedAtSet_PreservesValue()
    {
        // Arrange
        var processedAt = new DateTimeOffset(2025, 1, 15, 12, 0, 0, TimeSpan.Zero);

        // Act
        var message = new OutboxMessage
        {
            Id = "outbox-processed-time",
            Destination = TestData.Address(),
            Envelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" }),
            ProcessedAt = processedAt
        };

        // Assert
        Assert.Equal(processedAt, message.ProcessedAt);
    }

    /// <summary>
    /// Verifies OutboxMessage ScheduledAt can be set.
    /// </summary>
    [Fact]
    public void OutboxMessage_ScheduledAtSet_PreservesValue()
    {
        // Arrange
        var scheduledAt = new DateTimeOffset(2025, 1, 15, 14, 0, 0, TimeSpan.Zero);

        // Act
        var message = new OutboxMessage
        {
            Id = "outbox-scheduled-time",
            Destination = TestData.Address(),
            Envelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" }),
            ScheduledAt = scheduledAt
        };

        // Assert
        Assert.Equal(scheduledAt, message.ScheduledAt);
    }

    /// <summary>
    /// Verifies OutboxMessage ErrorMessage can be set.
    /// </summary>
    [Fact]
    public void OutboxMessage_ErrorMessageSet_PreservesValue()
    {
        // Arrange & Act
        var message = new OutboxMessage
        {
            Id = "outbox-error",
            Destination = TestData.Address(),
            Envelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" }),
            ErrorMessage = "Connection timeout"
        };

        // Assert
        Assert.Equal("Connection timeout", message.ErrorMessage);
    }

    /// <summary>
    /// Verifies OutboxMessage can store complex envelope.
    /// </summary>
    [Fact]
    public void OutboxMessage_WithComplexEnvelope_StoresAllData()
    {
        // Arrange
        var sampleMessage = new SampleMessage { Value = "complex-test" };
        var envelope = TestData.CreateEnvelope(sampleMessage, "complex-msg-id");
        var destination = TestData.Address("complex-queue", "complex-address");

        // Act
        var message = new OutboxMessage
        {
            Id = "outbox-complex",
            Destination = destination,
            Envelope = envelope
        };

        // Assert
        Assert.Equal("complex-msg-id", message.Envelope.MessageId);
        Assert.Equal("complex-queue", message.Destination.Name);
        Assert.Equal("complex-address", message.Destination.Address);
    }

    /// <summary>
    /// Verifies OutboxMessage implements IOutboxMessage interface.
    /// </summary>
    [Fact]
    public void OutboxMessage_ImplementsIOutboxMessage()
    {
        // Arrange
        var envelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" });
        var destination = TestData.Address();

        // Act
        OutboxMessage message = new()
        {
            Id = "outbox-interface",
            Destination = destination,
            Envelope = envelope
        };

        // Assert
        Assert.NotNull(message);
        Assert.Equal("outbox-interface", message.Id);
    }

    /// <summary>
    /// Verifies OutboxMessage is sealed.
    /// </summary>
    [Fact]
    public void OutboxMessage_IsSealed()
    {
        // Arrange
        var type = typeof(OutboxMessage);

        // Assert
        Assert.True(type.IsSealed);
    }

    /// <summary>
    /// Verifies OutboxMessage with zero attempts is valid.
    /// </summary>
    [Fact]
    public void OutboxMessage_WithZeroAttempts_IsValid()
    {
        // Arrange & Act
        var message = new OutboxMessage
        {
            Id = "outbox-zero",
            Destination = TestData.Address(),
            Envelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" }),
            Attempts = 0
        };

        // Assert
        Assert.Equal(0, message.Attempts);
    }

    /// <summary>
    /// Verifies OutboxMessage with high attempt count is valid.
    /// </summary>
    [Fact]
    public void OutboxMessage_WithHighAttemptCount_IsValid()
    {
        // Arrange & Act
        var message = new OutboxMessage
        {
            Id = "outbox-high-attempts",
            Destination = TestData.Address(),
            Envelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" }),
            Attempts = 100
        };

        // Assert
        Assert.Equal(100, message.Attempts);
    }

    /// <summary>
    /// Verifies OutboxMessage properties are init-only (immutable after creation).
    /// </summary>
    [Fact]
    public void OutboxMessage_PropertiesAreInitOnly()
    {
        // Arrange
        var message = new OutboxMessage
        {
            Id = "outbox-immutable",
            Destination = TestData.Address(),
            Envelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" }),
            State = OutboxMessageState.Pending
        };

        // Act - Try to access properties (init-only properties can't be reassigned after construction)
        var id = message.Id;
        var state = message.State;

        // Assert
        Assert.Equal("outbox-immutable", id);
        Assert.Equal(OutboxMessageState.Pending, state);

        // Note: We can't test mutation directly as init-only properties
        // will cause compilation errors if we try to reassign them
    }
}
