using HoneyDrunk.Transport.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace HoneyDrunk.Transport.Tests.Core.Configuration;

/// <summary>
/// Tests for <see cref="DefaultErrorHandlingStrategy"/> classification logic.
/// </summary>
public sealed class DefaultErrorHandlingStrategyTests
{
    /// <summary>
    /// Permanent exceptions should dead-letter immediately.
    /// </summary>
    /// <param name="exceptionType">Exception type under test.</param>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Theory]
    [InlineData(typeof(ArgumentException))]
    [InlineData(typeof(InvalidOperationException))]
    [InlineData(typeof(NotImplementedException))]
    public async Task HandleErrorAsync_PermanentExceptions_DeadLetter(Type exceptionType)
    {
        var ex = (Exception)Activator.CreateInstance(exceptionType, "perm")!;
        var strat = Create();
        var decision = await strat.HandleErrorAsync(ex, deliveryCount: 1);
        Assert.Equal(ErrorHandlingAction.DeadLetter, decision.Action);
        Assert.Equal("perm", decision.Reason);
        Assert.Null(decision.RetryDelay);
    }

    /// <summary>
    /// Transient exceptions should retry with delay.
    /// </summary>
    /// <param name="exceptionType">Exception type under test.</param>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Theory]
    [InlineData(typeof(TimeoutException))]
    [InlineData(typeof(TaskCanceledException))]
    [InlineData(typeof(OperationCanceledException))]
    public async Task HandleErrorAsync_TransientExceptions_RetryWithDelay(Type exceptionType)
    {
        var ex = (Exception)Activator.CreateInstance(exceptionType, "trans")!;
        var strat = Create();
        var decision = await strat.HandleErrorAsync(ex, deliveryCount: 2);
        Assert.Equal(ErrorHandlingAction.Retry, decision.Action);
        Assert.NotNull(decision.RetryDelay);
        Assert.True(decision.RetryDelay!.Value >= TimeSpan.FromMilliseconds(500));
    }

    /// <summary>
    /// Unknown exceptions below threshold should retry with linear backoff.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task HandleErrorAsync_UnknownBelowThreshold_RetryLinear()
    {
        var ex = new UnknownTestException("unknown");
        var strat = Create();
        var decision = await strat.HandleErrorAsync(ex, deliveryCount: 3);
        Assert.Equal(ErrorHandlingAction.Retry, decision.Action);
        Assert.NotNull(decision.RetryDelay);
        Assert.InRange(decision.RetryDelay!.Value, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(7));
    }

    /// <summary>
    /// Unknown exceptions at threshold should dead-letter.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task HandleErrorAsync_UnknownAtThreshold_DeadLetter()
    {
        var ex = new UnknownTestException("limit");
        var strat = Create();
        var decision = await strat.HandleErrorAsync(ex, deliveryCount: 5);
        Assert.Equal(ErrorHandlingAction.DeadLetter, decision.Action);
        Assert.Null(decision.RetryDelay);
    }

    /// <summary>
    /// Creates a new strategy instance with a null logger.
    /// </summary>
    /// <returns>The strategy instance.</returns>
    private static DefaultErrorHandlingStrategy Create() => new(NullLogger<DefaultErrorHandlingStrategy>.Instance);

    /// <summary>
    /// Custom unknown test exception used to represent unclassified failures.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="UnknownTestException"/> class.
    /// </remarks>
    /// <param name="message">The exception message.</param>
    private sealed class UnknownTestException(string message) : Exception(message)
    {
    }
}
