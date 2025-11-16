using HoneyDrunk.Transport.Configuration;

namespace HoneyDrunk.Transport.Tests.Core.Configuration;

/// <summary>
/// Tests for <see cref="RetryOptions.CalculateDelay(int)"/> covering strategies, jitter, caps and invalid attempts.
/// </summary>
public sealed class RetryOptionsDelayTests
{
    /// <summary>
    /// Verifies fixed and linear strategies produce expected delay without jitter.
    /// </summary>
    /// <param name="strategy">The backoff strategy under test.</param>
    /// <param name="attempt">The attempt number.</param>
    /// <param name="expectedMs">The expected delay in milliseconds.</param>
    [Theory]
    [InlineData(BackoffStrategy.Fixed, 1, 1000)]
    [InlineData(BackoffStrategy.Fixed, 3, 1000)]
    [InlineData(BackoffStrategy.Linear, 1, 1000)]
    [InlineData(BackoffStrategy.Linear, 3, 3000)]
    public void CalculateDelay_FixedAndLinear_NoJitter_ReturnsExpected(BackoffStrategy strategy, int attempt, int expectedMs)
    {
        var opts = new RetryOptions
        {
            Strategy = strategy,
            InitialDelay = TimeSpan.FromMilliseconds(1000),
            UseJitter = false
        };
        var delay = opts.CalculateDelay(attempt);
        Assert.Equal(TimeSpan.FromMilliseconds(expectedMs), delay);
    }

    /// <summary>
    /// Verifies exponential strategy applies multiplier power sequence.
    /// </summary>
    [Fact]
    public void CalculateDelay_Exponential_NoJitter_ComputesPow()
    {
        var opts = new RetryOptions
        {
            Strategy = BackoffStrategy.Exponential,
            InitialDelay = TimeSpan.FromMilliseconds(500),
            BackoffMultiplier = 2,
            UseJitter = false
        };
        Assert.Equal(TimeSpan.FromMilliseconds(500), opts.CalculateDelay(1));
        Assert.Equal(TimeSpan.FromMilliseconds(2000), opts.CalculateDelay(3));
    }

    /// <summary>
    /// Ensures delay is capped at MaxDelay.
    /// </summary>
    [Fact]
    public void CalculateDelay_AboveMax_CapsAtMax()
    {
        var opts = new RetryOptions
        {
            Strategy = BackoffStrategy.Exponential,
            InitialDelay = TimeSpan.FromSeconds(1),
            BackoffMultiplier = 10,
            MaxDelay = TimeSpan.FromSeconds(5),
            UseJitter = false
        };
        var delay = opts.CalculateDelay(3);
        Assert.Equal(TimeSpan.FromSeconds(5), delay);
    }

    /// <summary>
    /// Confirms jitter randomizes delay within expected range.
    /// </summary>
    [Fact]
    public void CalculateDelay_WithJitter_AppliesRange()
    {
        var opts = new RetryOptions
        {
            Strategy = BackoffStrategy.Fixed,
            InitialDelay = TimeSpan.FromSeconds(10),
            UseJitter = true
        };
        var delay = opts.CalculateDelay(2);
        Assert.InRange(delay, TimeSpan.FromSeconds(7), TimeSpan.FromSeconds(13));
    }

    /// <summary>
    /// Returns zero for non-positive attempt numbers.
    /// </summary>
    [Fact]
    public void CalculateDelay_InvalidAttempt_ReturnsZero()
    {
        var opts = new RetryOptions { UseJitter = false };
        Assert.Equal(TimeSpan.Zero, opts.CalculateDelay(0));
        Assert.Equal(TimeSpan.Zero, opts.CalculateDelay(-1));
    }
}
