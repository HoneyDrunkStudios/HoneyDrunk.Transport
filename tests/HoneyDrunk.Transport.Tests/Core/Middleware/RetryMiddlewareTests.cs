using HoneyDrunk.Transport.Pipeline.Middleware;
using HoneyDrunk.Transport.Tests.Support;

namespace HoneyDrunk.Transport.Tests.Core.Middleware;

/// <summary>
/// Tests for retry middleware behavior.
/// </summary>
public sealed class RetryMiddlewareTests
{
    /// <summary>
    /// Ensures the next delegate is called when under max attempts.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task InvokeAsync_DeliveryCountBelowMax_CallsNext()
    {
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<RetryMiddleware>.Instance;
        var mw = new RetryMiddleware(logger, maxAttempts: 3);
        var ctx = new HoneyDrunk.Transport.Abstractions.MessageContext
        {
            Envelope = TestData.CreateEnvelope(new SampleMessage { Value = "x" }),
            Transaction = HoneyDrunk.Transport.Abstractions.NoOpTransportTransaction.Instance,
            DeliveryCount = 2
        };

        var called = false;
        await mw.InvokeAsync(
            TestData.CreateEnvelope(new SampleMessage { Value = "x" }),
            ctx,
            () =>
            {
                called = true;
                return Task.CompletedTask;
            },
            CancellationToken.None);

        Assert.True(called);
    }

    /// <summary>
    /// Ensures exceeding max attempts throws MessageHandlerException.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task InvokeAsync_DeliveryCountExceedsMax_ThrowsMessageHandlerException()
    {
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<RetryMiddleware>.Instance;
        var mw = new RetryMiddleware(logger, maxAttempts: 1);
        var ctx = new HoneyDrunk.Transport.Abstractions.MessageContext
        {
            Envelope = TestData.CreateEnvelope(new SampleMessage { Value = "x" }),
            Transaction = HoneyDrunk.Transport.Abstractions.NoOpTransportTransaction.Instance,
            DeliveryCount = 2
        };

        await Assert.ThrowsAsync<HoneyDrunk.Transport.Pipeline.MessageHandlerException>(
            () => mw.InvokeAsync(
                TestData.CreateEnvelope(new SampleMessage { Value = "x" }),
                ctx,
                () => Task.CompletedTask,
                CancellationToken.None));
    }
}
