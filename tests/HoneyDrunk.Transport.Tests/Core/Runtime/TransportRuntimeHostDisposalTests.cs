using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.Runtime;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace HoneyDrunk.Transport.Tests.Core.Runtime;

/// <summary>
/// Verifies overlapping host shutdown and container disposal.
/// </summary>
public sealed class TransportRuntimeHostDisposalTests
{
    /// <summary>
    /// Disposal does not invalidate the semaphore while consumers are stopping.
    /// </summary>
    /// <returns>The asynchronous test operation.</returns>
    [Fact]
    public async Task Dispose_DuringStop_AllowsActiveAndQueuedStopsToComplete()
    {
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var consumer = Substitute.For<ITransportConsumer>();
        consumer.StopAsync(Arg.Any<CancellationToken>()).Returns(stopped.Task);
        using var host = new TransportRuntimeHost([consumer], [], NullLogger<TransportRuntimeHost>.Instance);
        await host.StartAsync();

        var activeStop = host.StopAsync();
        var queuedStop = host.StopAsync();
        Assert.False(activeStop.IsCompleted);
        Assert.False(queuedStop.IsCompleted);

        host.Dispose();
        host.Dispose();
        stopped.SetResult();
        await Task.WhenAll(activeStop, queuedStop).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.False(host.IsRunning);
        await consumer.Received(1).StopAsync(Arg.Any<CancellationToken>());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => host.StartAsync());
    }

    /// <summary>
    /// A canceled waiter does not retain a pending lifecycle operation during disposal.
    /// </summary>
    /// <returns>The asynchronous test operation.</returns>
    [Fact]
    public async Task Dispose_WithCanceledQueuedStop_AllowsActiveStopToComplete()
    {
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var consumer = Substitute.For<ITransportConsumer>();
        consumer.StopAsync(Arg.Any<CancellationToken>()).Returns(stopped.Task);
        using var host = new TransportRuntimeHost([consumer], [], NullLogger<TransportRuntimeHost>.Instance);
        await host.StartAsync();
        var activeStop = host.StopAsync();
        using var cancellation = new CancellationTokenSource();
        var queuedStop = host.StopAsync(cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queuedStop);

        host.Dispose();
        stopped.SetResult();
        await activeStop.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.False(host.IsRunning);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => host.StopAsync());
    }
}
