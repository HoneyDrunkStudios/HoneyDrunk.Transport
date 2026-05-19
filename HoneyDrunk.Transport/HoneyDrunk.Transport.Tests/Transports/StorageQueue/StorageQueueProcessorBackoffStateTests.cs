using HoneyDrunk.Transport.StorageQueue.Internal;
using System.Reflection;

namespace HoneyDrunk.Transport.Tests.Transports.StorageQueue;

/// <summary>
/// Tests for the per-consumer backoff state used by <see cref="StorageQueueProcessor"/>.
/// </summary>
public sealed class StorageQueueProcessorBackoffStateTests
{
    /// <summary>
    /// Empty receive backoff grows up to the configured maximum and reset restores the initial interval.
    /// </summary>
    [Fact]
    public void ConsumerBackoffState_AppliesExponentialBackoffAndReset()
    {
        var state = CreateState(TimeSpan.FromMilliseconds(100));

        Invoke(state, "IncrementEmptyReceive");
        Invoke(state, "ApplyExponentialBackoff", TimeSpan.FromMilliseconds(200));
        Assert.Equal(TimeSpan.FromMilliseconds(100), CurrentPollingInterval(state));

        Invoke(state, "IncrementEmptyReceive");
        Invoke(state, "ApplyExponentialBackoff", TimeSpan.FromMilliseconds(200));
        Assert.Equal(TimeSpan.FromMilliseconds(150), CurrentPollingInterval(state));

        Invoke(state, "IncrementEmptyReceive");
        Invoke(state, "ApplyExponentialBackoff", TimeSpan.FromMilliseconds(200));
        Assert.Equal(TimeSpan.FromMilliseconds(200), CurrentPollingInterval(state));

        Invoke(state, "Reset", TimeSpan.FromMilliseconds(100));
        Assert.Equal(TimeSpan.FromMilliseconds(100), CurrentPollingInterval(state));
    }

    /// <summary>
    /// Reset is a no-op when there have been no empty receives.
    /// </summary>
    [Fact]
    public void ConsumerBackoffState_ResetWithoutEmptyReceives_LeavesIntervalUnchanged()
    {
        var state = CreateState(TimeSpan.FromMilliseconds(250));

        Invoke(state, "Reset", TimeSpan.FromMilliseconds(100));

        Assert.Equal(TimeSpan.FromMilliseconds(250), CurrentPollingInterval(state));
    }

    private static object CreateState(TimeSpan initialPollingInterval)
    {
        var type = typeof(StorageQueueProcessor).GetNestedType("ConsumerBackoffState", BindingFlags.NonPublic)!;
        return Activator.CreateInstance(type, initialPollingInterval)!;
    }

    private static TimeSpan CurrentPollingInterval(object state)
    {
        var property = state.GetType().GetProperty("CurrentPollingInterval")!;
        return (TimeSpan)property.GetValue(state)!;
    }

    private static void Invoke(object state, string methodName, params object[] args)
    {
        var method = state.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public)!;
        method.Invoke(state, args);
    }
}
