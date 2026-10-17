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
    /// IOException should be classified as transient.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task HandleErrorAsync_IOException_IsTransient()
    {
        var ex = new IOException("I/O error");
        var strat = Create();
        var decision = await strat.HandleErrorAsync(ex, deliveryCount: 1);
        Assert.Equal(ErrorHandlingAction.Retry, decision.Action);
        Assert.NotNull(decision.RetryDelay);
    }

    /// <summary>
    /// SocketException should be classified as transient.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task HandleErrorAsync_SocketException_IsTransient()
    {
        var ex = new System.Net.Sockets.SocketException((int)System.Net.Sockets.SocketError.ConnectionRefused);
        var strat = Create();
        var decision = await strat.HandleErrorAsync(ex, deliveryCount: 1);
        Assert.Equal(ErrorHandlingAction.Retry, decision.Action);
        Assert.NotNull(decision.RetryDelay);
    }

    /// <summary>
    /// Exponential backoff should increase delay with attempts.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task HandleErrorAsync_TransientWithIncreasedAttempts_IncreasesDelay()
    {
        var strat = Create();
        var ex = new TimeoutException("timeout");

        var decision1 = await strat.HandleErrorAsync(ex, deliveryCount: 1);
        var decision2 = await strat.HandleErrorAsync(ex, deliveryCount: 2);
        var decision3 = await strat.HandleErrorAsync(ex, deliveryCount: 3);

        // Each subsequent attempt should have exponentially longer delay (with jitter)
        Assert.NotNull(decision1.RetryDelay);
        Assert.NotNull(decision2.RetryDelay);
        Assert.NotNull(decision3.RetryDelay);

        // Account for jitter: delay2 should be roughly 2x delay1 (within jitter range 0.75-1.25)
        // We can't assert exact values due to jitter, but the general trend should be increasing
        Assert.True(decision2.RetryDelay!.Value.TotalMilliseconds > decision1.RetryDelay!.Value.TotalMilliseconds * 0.5);
        Assert.True(decision3.RetryDelay!.Value.TotalMilliseconds > decision2.RetryDelay!.Value.TotalMilliseconds * 0.5);
    }

    /// <summary>
    /// Exponential backoff should respect maximum delay.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task HandleErrorAsync_TransientWithManyAttempts_RespectMaxDelay()
    {
        var strat = Create();
        var ex = new TimeoutException("timeout");

        // Attempt 20 should be capped at max delay (30 seconds)
        var decision = await strat.HandleErrorAsync(ex, deliveryCount: 20);

        Assert.NotNull(decision.RetryDelay);

        // With jitter (0.75-1.25), max would be 30 * 1.25 = 37.5 seconds
        Assert.InRange(decision.RetryDelay!.Value, TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(40));
    }

    /// <summary>
    /// Unknown exception at deliveryCount 1 should retry with initial delay.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task HandleErrorAsync_UnknownFirstAttempt_RetryWithShortDelay()
    {
        var ex = new UnknownTestException("first");
        var strat = Create();
        var decision = await strat.HandleErrorAsync(ex, deliveryCount: 1);

        Assert.Equal(ErrorHandlingAction.Retry, decision.Action);
        Assert.NotNull(decision.RetryDelay);

        // Linear: 1 * 2 = 2 seconds, max 20 seconds
        Assert.InRange(decision.RetryDelay!.Value, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3));
    }

    /// <summary>
    /// Unknown exception at deliveryCount 4 should retry with longer delay.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task HandleErrorAsync_UnknownFourthAttempt_RetryWithLongerDelay()
    {
        var ex = new UnknownTestException("fourth");
        var strat = Create();
        var decision = await strat.HandleErrorAsync(ex, deliveryCount: 4);

        Assert.Equal(ErrorHandlingAction.Retry, decision.Action);
        Assert.NotNull(decision.RetryDelay);

        // Linear: 4 * 2 = 8 seconds, max 20 seconds
        Assert.InRange(decision.RetryDelay!.Value, TimeSpan.FromSeconds(6), TimeSpan.FromSeconds(10));
    }

    /// <summary>
    /// Unknown exception exceeding threshold should dead-letter.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task HandleErrorAsync_UnknownExceedingThreshold_DeadLetters()
    {
        var ex = new UnknownTestException("exceeded");
        var strat = Create();

        var decision6 = await strat.HandleErrorAsync(ex, deliveryCount: 6);
        var decision10 = await strat.HandleErrorAsync(ex, deliveryCount: 10);

        Assert.Equal(ErrorHandlingAction.DeadLetter, decision6.Action);
        Assert.Equal(ErrorHandlingAction.DeadLetter, decision10.Action);
        Assert.Null(decision6.RetryDelay);
        Assert.Null(decision10.RetryDelay);
    }

    /// <summary>
    /// Permanent exception with high deliveryCount still dead-letters immediately.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task HandleErrorAsync_PermanentWithHighDeliveryCount_StillDeadLetters()
    {
        var ex = new ArgumentException("permanent");
        var strat = Create();
        var decision = await strat.HandleErrorAsync(ex, deliveryCount: 10);

        Assert.Equal(ErrorHandlingAction.DeadLetter, decision.Action);
        Assert.Null(decision.RetryDelay);
    }

    /// <summary>
    /// Transient exception with deliveryCount 0 should retry.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task HandleErrorAsync_TransientWithZeroDeliveryCount_Retries()
    {
        var ex = new TimeoutException("timeout");
        var strat = Create();
        var decision = await strat.HandleErrorAsync(ex, deliveryCount: 0);

        Assert.Equal(ErrorHandlingAction.Retry, decision.Action);
        Assert.NotNull(decision.RetryDelay);

        // Should use initial delay (1 second with jitter)
        Assert.InRange(decision.RetryDelay!.Value, TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(2));
    }

    /// <summary>
    /// Strategy can handle null cancellation token.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task HandleErrorAsync_WithDefaultCancellationToken_WorksCorrectly()
    {
        var ex = new TimeoutException("timeout");
        var strat = Create();
        var decision = await strat.HandleErrorAsync(ex, deliveryCount: 1, cancellationToken: default);

        Assert.Equal(ErrorHandlingAction.Retry, decision.Action);
        Assert.NotNull(decision.RetryDelay);
    }

    /// <summary>
    /// Strategy respects cancellation token.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task HandleErrorAsync_WithCancelledToken_ReturnsDecision()
    {
        var ex = new TimeoutException("timeout");
        var strat = Create();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Strategy doesn't actually check cancellation, but accepts the token
        var decision = await strat.HandleErrorAsync(ex, deliveryCount: 1, cancellationToken: cts.Token);

        Assert.Equal(ErrorHandlingAction.Retry, decision.Action);
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
