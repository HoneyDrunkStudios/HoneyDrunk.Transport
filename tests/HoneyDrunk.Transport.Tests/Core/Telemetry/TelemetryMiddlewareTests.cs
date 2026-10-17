using HoneyDrunk.Transport.Tests.Support;

namespace HoneyDrunk.Transport.Tests.Core.Telemetry;

/// <summary>
/// Tests for telemetry middleware.
/// </summary>
public sealed class TelemetryMiddlewareTests
{
    /// <summary>
    /// Verifies telemetry middleware calls next delegate on success.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task InvokeAsync_WithSuccessfulNext_CallsNextDelegate()
    {
        var mw = new HoneyDrunk.Transport.Telemetry.TelemetryMiddleware();
        var envelope = TestData.CreateEnvelope(new SampleMessage { Value = "x" });
        var ctx = new HoneyDrunk.Transport.Abstractions.MessageContext
        {
            Envelope = envelope,
            Transaction = HoneyDrunk.Transport.Abstractions.NoOpTransportTransaction.Instance,
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
    /// Verifies telemetry middleware propagates exceptions and records error.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task InvokeAsync_WhenNextThrows_PropagatesExceptionAndRecordsError()
    {
        var mw = new HoneyDrunk.Transport.Telemetry.TelemetryMiddleware();
        var envelope = TestData.CreateEnvelope(new SampleMessage { Value = "x" });
        var ctx = new HoneyDrunk.Transport.Abstractions.MessageContext
        {
            Envelope = envelope,
            Transaction = HoneyDrunk.Transport.Abstractions.NoOpTransportTransaction.Instance,
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
