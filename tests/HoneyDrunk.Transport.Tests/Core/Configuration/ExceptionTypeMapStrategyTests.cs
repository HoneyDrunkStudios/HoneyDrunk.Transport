using HoneyDrunk.Transport.Configuration;

namespace HoneyDrunk.Transport.Tests.Core.Configuration;

/// <summary>
/// Tests for <see cref="ExceptionTypeMapStrategy"/> and its builder.
/// </summary>
public sealed class ExceptionTypeMapStrategyTests
{
    /// <summary>
    /// Builder maps exact exception types to retry decisions.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task HandleErrorAsync_WhenExactRetryMappingExists_ReturnsRetryDecision()
    {
        var strategy = ExceptionTypeMapStrategy.CreateBuilder()
            .MapToRetry<TimeoutException>()
            .Build();

        var decision = await strategy.HandleErrorAsync(new TimeoutException("timeout"), deliveryCount: 2);

        Assert.Equal(ErrorHandlingAction.Retry, decision.Action);
        Assert.Equal(TimeSpan.FromSeconds(2), decision.RetryDelay);
        Assert.Contains("Mapped action for TimeoutException", decision.Reason, StringComparison.Ordinal);
    }

    /// <summary>
    /// Builder maps exact exception types to dead-letter decisions.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task HandleErrorAsync_WhenExactDeadLetterMappingExists_ReturnsDeadLetterDecision()
    {
        var strategy = ExceptionTypeMapStrategy.CreateBuilder()
            .MapToDeadLetter<InvalidOperationException>()
            .Build();

        var decision = await strategy.HandleErrorAsync(new InvalidOperationException("bad"), deliveryCount: 1);

        Assert.Equal(ErrorHandlingAction.DeadLetter, decision.Action);
        Assert.Null(decision.RetryDelay);
        Assert.Contains("Mapped action for InvalidOperationException", decision.Reason, StringComparison.Ordinal);
    }

    /// <summary>
    /// Base exception mappings apply to derived exception instances.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task HandleErrorAsync_WhenBaseMappingMatchesDerivedException_ReturnsInheritedDecision()
    {
        var strategy = ExceptionTypeMapStrategy.CreateBuilder()
            .MapToDeadLetter<ArgumentException>()
            .Build();

        var decision = await strategy.HandleErrorAsync(new ArgumentNullException("name"), deliveryCount: 1);

        Assert.Equal(ErrorHandlingAction.DeadLetter, decision.Action);
        Assert.Contains("Inherited action from ArgumentException", decision.Reason, StringComparison.Ordinal);
    }

    /// <summary>
    /// Delivery count at the maximum dead-letters before mappings are evaluated.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task HandleErrorAsync_WhenMaxDeliveryCountReached_DeadLettersBeforeMappingLookup()
    {
        var strategy = ExceptionTypeMapStrategy.CreateBuilder()
            .WithMaxDeliveryCount(2)
            .MapToRetry<TimeoutException>()
            .Build();

        var decision = await strategy.HandleErrorAsync(new TimeoutException("timeout"), deliveryCount: 2);

        Assert.Equal(ErrorHandlingAction.DeadLetter, decision.Action);
        Assert.Contains("Maximum delivery count (2) exceeded", decision.Reason, StringComparison.Ordinal);
    }

    /// <summary>
    /// Unmapped exceptions use the configured default action.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task HandleErrorAsync_WhenNoMappingMatches_UsesConfiguredDefaultAction()
    {
        var strategy = ExceptionTypeMapStrategy.CreateBuilder()
            .WithDefaultAction(ErrorHandlingAction.Abandon)
            .Build();

        var decision = await strategy.HandleErrorAsync(new FormatException("unknown"), deliveryCount: 1);

        Assert.Equal(ErrorHandlingAction.Abandon, decision.Action);
        Assert.Contains("Default action", decision.Reason, StringComparison.Ordinal);
    }

    /// <summary>
    /// Retry decisions cap exponential backoff at 32 seconds.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task HandleErrorAsync_WhenRetryDeliveryCountIsHigh_CapsBackoffDelay()
    {
        var strategy = ExceptionTypeMapStrategy.CreateBuilder()
            .WithMaxDeliveryCount(200)
            .MapToRetry<TimeoutException>()
            .Build();

        var decision = await strategy.HandleErrorAsync(new TimeoutException("timeout"), deliveryCount: 100);

        Assert.Equal(ErrorHandlingAction.Retry, decision.Action);
        Assert.Equal(TimeSpan.FromSeconds(32), decision.RetryDelay);
    }
}
