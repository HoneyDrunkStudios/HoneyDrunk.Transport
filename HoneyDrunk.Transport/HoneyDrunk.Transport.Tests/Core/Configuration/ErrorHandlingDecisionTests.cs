using HoneyDrunk.Transport.Configuration;

namespace HoneyDrunk.Transport.Tests.Core.Configuration;

/// <summary>
/// Tests for <see cref="ErrorHandlingDecision"/> static factory helpers verifying action, delay and reason mapping.
/// </summary>
public sealed class ErrorHandlingDecisionTests
{
    /// <summary>
    /// Ensures Retry decision sets action, delay and reason.
    /// </summary>
    [Fact]
    public void Retry_CreatesDecisionWithDelayAndReason()
    {
        var d = ErrorHandlingDecision.Retry(TimeSpan.FromSeconds(5), "transient");
        Assert.Equal(ErrorHandlingAction.Retry, d.Action);
        Assert.Equal(TimeSpan.FromSeconds(5), d.RetryDelay);
        Assert.Equal("transient", d.Reason);
    }

    /// <summary>
    /// Ensures DeadLetter decision has no retry delay.
    /// </summary>
    [Fact]
    public void DeadLetter_CreatesDecisionWithoutRetryDelay()
    {
        var d = ErrorHandlingDecision.DeadLetter("fatal");
        Assert.Equal(ErrorHandlingAction.DeadLetter, d.Action);
        Assert.Null(d.RetryDelay);
        Assert.Equal("fatal", d.Reason);
    }

    /// <summary>
    /// Ensures Abandon decision has no retry delay.
    /// </summary>
    [Fact]
    public void Abandon_CreatesDecisionWithoutRetryDelay()
    {
        var d = ErrorHandlingDecision.Abandon("giving up");
        Assert.Equal(ErrorHandlingAction.Abandon, d.Action);
        Assert.Null(d.RetryDelay);
        Assert.Equal("giving up", d.Reason);
    }
}
