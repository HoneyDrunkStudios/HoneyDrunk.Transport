using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.Pipeline;
using HoneyDrunk.Transport.Tests.Support;

namespace HoneyDrunk.Transport.Tests.Core.Pipeline;

/// <summary>
/// Tests for <see cref="TransportExecutionContext"/>.
/// </summary>
public sealed class TransportExecutionContextTests
{
    /// <summary>
    /// ToMessageContext should copy broker properties and expose derived delivery count.
    /// </summary>
    [Fact]
    public void ToMessageContext_CopiesExecutionState()
    {
        var envelope = TestData.CreateEnvelope(new SampleMessage { Value = "payload" });
        var context = new TransportExecutionContext
        {
            Envelope = envelope,
            Transaction = NoOpTransportTransaction.Instance,
            RetryAttempt = 2,
            ProcessingDuration = TimeSpan.FromMilliseconds(42),
            BrokerProperties =
            {
                ["LockToken"] = "abc",
                ["SequenceNumber"] = 123L,
            },
        };

        var messageContext = context.ToMessageContext();

        Assert.Same(envelope, messageContext.Envelope);
        Assert.Same(NoOpTransportTransaction.Instance, messageContext.Transaction);
        Assert.Equal(3, context.DeliveryCount);
        Assert.Equal(3, messageContext.DeliveryCount);
        Assert.Equal(TimeSpan.FromMilliseconds(42), context.ProcessingDuration);
        Assert.Equal("abc", messageContext.Properties["LockToken"]);
        Assert.Equal(123L, messageContext.Properties["SequenceNumber"]);

        context.BrokerProperties["LockToken"] = "changed";
        Assert.Equal("abc", messageContext.Properties["LockToken"]);
    }
}
