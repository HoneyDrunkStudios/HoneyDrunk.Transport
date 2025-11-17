using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.Tests.Support;

namespace HoneyDrunk.Transport.Tests.Core.Context;

/// <summary>
/// Tests for message context and related types.
/// </summary>
public sealed class MessageContextTests
{
    /// <summary>
    /// Verifies MessageContext initializes properties dictionary.
    /// </summary>
    [Fact]
    public void MessageContext_WhenCreated_InitializesPropertiesDictionary()
    {
        var envelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" });
        var ctx = new MessageContext
        {
            Envelope = envelope,
            Transaction = NoOpTransportTransaction.Instance,
            DeliveryCount = 1
        };

        Assert.NotNull(ctx.Properties);
        Assert.Empty(ctx.Properties);
    }

    /// <summary>
    /// Verifies NoOpTransportTransaction instance is singleton.
    /// </summary>
    [Fact]
    public void NoOpTransportTransaction_WhenAccessed_ReturnsSingletonInstance()
    {
        var instance1 = NoOpTransportTransaction.Instance;
        var instance2 = NoOpTransportTransaction.Instance;
        Assert.Same(instance1, instance2);
    }

    /// <summary>
    /// Verifies NoOpTransportTransaction commit does nothing.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task CommitAsync_WhenCalled_CompletesSuccessfully()
    {
        var tx = NoOpTransportTransaction.Instance;
        await tx.CommitAsync();
    }

    /// <summary>
    /// Verifies NoOpTransportTransaction rollback does nothing.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task RollbackAsync_WhenCalled_CompletesSuccessfully()
    {
        var tx = NoOpTransportTransaction.Instance;
        await tx.RollbackAsync();
    }

    /// <summary>
    /// Verifies MessageProcessingResult enum values.
    /// </summary>
    [Fact]
    public void MessageProcessingResult_WhenValidating_HasExpectedEnumValues()
    {
        Assert.Equal(0, (int)MessageProcessingResult.Success);
        Assert.Equal(1, (int)MessageProcessingResult.Retry);
        Assert.Equal(2, (int)MessageProcessingResult.DeadLetter);
    }
}
