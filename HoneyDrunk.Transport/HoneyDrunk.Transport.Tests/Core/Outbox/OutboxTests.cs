using HoneyDrunk.Transport.Outbox;
using HoneyDrunk.Transport.Tests.Support;

namespace HoneyDrunk.Transport.Tests.Core.Outbox;

/// <summary>
/// Tests for outbox pattern types.
/// </summary>
public sealed class OutboxTests
{
    /// <summary>
    /// Verifies OutboxMessageState enum values.
    /// </summary>
    [Fact]
    public void OutboxMessageState_WhenValidating_HasExpectedEnumValues()
    {
        Assert.Equal(0, (int)OutboxMessageState.Pending);
        Assert.Equal(1, (int)OutboxMessageState.Processing);
        Assert.Equal(2, (int)OutboxMessageState.Dispatched);
        Assert.Equal(3, (int)OutboxMessageState.Failed);
        Assert.Equal(4, (int)OutboxMessageState.Poisoned);
    }

    /// <summary>
    /// Verifies OutboxMessage can be instantiated.
    /// </summary>
    [Fact]
    public void OutboxMessage_WithRequiredProperties_CreatesSuccessfully()
    {
        var envelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" });
        var destination = TestData.Address("test", "queue");

        var msg = new OutboxMessage
        {
            Id = "test-id",
            Envelope = envelope,
            Destination = destination,
            State = OutboxMessageState.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };

        Assert.Equal("test-id", msg.Id);
        Assert.Equal(OutboxMessageState.Pending, msg.State);
        Assert.Equal(envelope, msg.Envelope);
        Assert.Equal(destination, msg.Destination);
    }
}
