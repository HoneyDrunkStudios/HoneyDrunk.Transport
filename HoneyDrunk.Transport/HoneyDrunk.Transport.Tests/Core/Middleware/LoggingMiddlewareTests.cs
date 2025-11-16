using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.Pipeline.Middleware;
using HoneyDrunk.Transport.Tests.Support;

namespace HoneyDrunk.Transport.Tests.Core.Middleware;

/// <summary>
/// Tests for logging middleware.
/// </summary>
public sealed class LoggingMiddlewareTests
{
    /// <summary>
    /// Verifies logging middleware calls next delegate.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task InvokeAsync_WithValidMessage_CallsNextDelegate()
    {
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<LoggingMiddleware>.Instance;
        var mw = new LoggingMiddleware(logger);
        var envelope = TestData.CreateEnvelope(new SampleMessage { Value = "x" });
        var ctx = new MessageContext
        {
            Envelope = envelope,
            Transaction = NoOpTransportTransaction.Instance,
            DeliveryCount = 1
        };

        var called = false;
        await mw.InvokeAsync(
            envelope,
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
    /// Verifies logging middleware propagates exceptions.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task InvokeAsync_WhenNextThrows_PropagatesException()
    {
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<LoggingMiddleware>.Instance;
        var mw = new LoggingMiddleware(logger);
        var envelope = TestData.CreateEnvelope(new SampleMessage { Value = "x" });
        var ctx = new MessageContext
        {
            Envelope = envelope,
            Transaction = NoOpTransportTransaction.Instance,
            DeliveryCount = 1
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => mw.InvokeAsync(
                envelope,
                ctx,
                () => throw new InvalidOperationException("test"),
                CancellationToken.None));
    }
}
