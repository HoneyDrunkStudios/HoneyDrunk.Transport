using HoneyDrunk.Transport.Configuration;

namespace HoneyDrunk.Transport.Tests.Core.Configuration;

/// <summary>
/// Tests for <see cref="ConfigurableErrorHandlingStrategy"/> rule selection.
/// </summary>
public sealed class ConfigurableErrorHandlingStrategyTests
{
    /// <summary>
    /// Configured retry decisions are returned for exact exception matches.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task HandleErrorAsync_WhenExactRetryRuleMatches_ReturnsConfiguredRetry()
    {
        var strategy = new ConfigurableErrorHandlingStrategy()
            .RetryOn<TimeoutException>(TimeSpan.FromSeconds(7));

        var decision = await strategy.HandleErrorAsync(new TimeoutException("timeout"), deliveryCount: 1);

        Assert.Equal(ErrorHandlingAction.Retry, decision.Action);
        Assert.Equal(TimeSpan.FromSeconds(7), decision.RetryDelay);
    }

    /// <summary>
    /// Configured dead-letter decisions are returned for exact exception matches.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task HandleErrorAsync_WhenExactDeadLetterRuleMatches_ReturnsConfiguredDeadLetter()
    {
        var strategy = new ConfigurableErrorHandlingStrategy()
            .DeadLetterOn<InvalidOperationException>();

        var decision = await strategy.HandleErrorAsync(new InvalidOperationException("bad"), deliveryCount: 1);

        Assert.Equal(ErrorHandlingAction.DeadLetter, decision.Action);
        Assert.Null(decision.RetryDelay);
    }

    /// <summary>
    /// Base exception rules apply to derived exception instances.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task HandleErrorAsync_WhenBaseRuleMatchesDerivedException_ReturnsInheritedDecision()
    {
        var strategy = new ConfigurableErrorHandlingStrategy()
            .DeadLetterOn<ArgumentException>();

        var decision = await strategy.HandleErrorAsync(new ArgumentNullException("value"), deliveryCount: 1);

        Assert.Equal(ErrorHandlingAction.DeadLetter, decision.Action);
    }

    /// <summary>
    /// Delivery count at the configured maximum dead-letters before rule lookup.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task HandleErrorAsync_WhenMaxDeliveryCountReached_DeadLettersBeforeRuleLookup()
    {
        var strategy = new ConfigurableErrorHandlingStrategy(maxDeliveryCount: 3)
            .RetryOn<TimeoutException>(TimeSpan.FromSeconds(1));

        var decision = await strategy.HandleErrorAsync(new TimeoutException("timeout"), deliveryCount: 3);

        Assert.Equal(ErrorHandlingAction.DeadLetter, decision.Action);
        Assert.Contains("Maximum delivery count (3) exceeded", decision.Reason, StringComparison.Ordinal);
    }

    /// <summary>
    /// Unknown exceptions retry with exponential backoff.
    /// </summary>
    /// <param name="deliveryCount">The delivery attempt count.</param>
    /// <param name="expectedDelay">The expected retry delay.</param>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(4, 8)]
    [InlineData(10, 32)]
    public async Task HandleErrorAsync_WhenNoRuleMatches_ReturnsBackoffRetry(int deliveryCount, int expectedDelay)
    {
        var strategy = new ConfigurableErrorHandlingStrategy(maxDeliveryCount: 100);

        var decision = await strategy.HandleErrorAsync(new FormatException("unknown"), deliveryCount);

        Assert.Equal(ErrorHandlingAction.Retry, decision.Action);
        Assert.Equal(TimeSpan.FromSeconds(expectedDelay), decision.RetryDelay);
        Assert.Equal("Unknown exception type - retry with backoff", decision.Reason);
    }
}
