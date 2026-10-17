using HoneyDrunk.Transport.Metrics;

namespace HoneyDrunk.Transport.Tests.Core.Metrics;

/// <summary>
/// Tests for no-op transport metrics implementation.
/// </summary>
public sealed class NoOpTransportMetricsTests
{
    /// <summary>
    /// Verifies Instance property returns singleton.
    /// </summary>
    [Fact]
    public void Instance_ReturnsSingleton()
    {
        // Act
        var instance1 = NoOpTransportMetrics.Instance;
        var instance2 = NoOpTransportMetrics.Instance;

        // Assert
        Assert.Same(instance1, instance2);
    }

    /// <summary>
    /// Verifies RecordMessagePublished does not throw.
    /// </summary>
    [Fact]
    public void RecordMessagePublished_DoesNotThrow()
    {
        // Arrange
        var metrics = NoOpTransportMetrics.Instance;

        // Act & Assert
        var ex = Record.Exception(() =>
        {
            metrics.RecordMessagePublished("TestMessage", "test-queue");
            metrics.RecordMessagePublished("TestMessage", "test-queue");
            metrics.RecordMessagePublished("AnotherMessage", "another-queue");
        });
        Assert.Null(ex);
    }

    /// <summary>
    /// Verifies RecordMessageConsumed does not throw.
    /// </summary>
    [Fact]
    public void RecordMessageConsumed_DoesNotThrow()
    {
        // Arrange
        var metrics = NoOpTransportMetrics.Instance;

        // Act & Assert
        var ex = Record.Exception(() =>
        {
            metrics.RecordMessageConsumed("TestMessage", "test-queue");
            metrics.RecordMessageConsumed("TestMessage", "test-queue");
            metrics.RecordMessageConsumed("AnotherMessage", "another-queue");
        });
        Assert.Null(ex);
    }

    /// <summary>
    /// Verifies RecordProcessingDuration does not throw.
    /// </summary>
    [Fact]
    public void RecordProcessingDuration_DoesNotThrow()
    {
        // Arrange
        var metrics = NoOpTransportMetrics.Instance;

        // Act & Assert
        var ex = Record.Exception(() =>
        {
            metrics.RecordProcessingDuration("TestMessage", TimeSpan.FromMilliseconds(100), "success");
            metrics.RecordProcessingDuration("TestMessage", TimeSpan.FromSeconds(1), "retry");
            metrics.RecordProcessingDuration("TestMessage", TimeSpan.FromMinutes(1), "dead-letter");
        });
        Assert.Null(ex);
    }

    /// <summary>
    /// Verifies RecordMessageRetry does not throw.
    /// </summary>
    [Fact]
    public void RecordMessageRetry_DoesNotThrow()
    {
        // Arrange
        var metrics = NoOpTransportMetrics.Instance;

        // Act & Assert
        var ex = Record.Exception(() =>
        {
            metrics.RecordMessageRetry("TestMessage", 1);
            metrics.RecordMessageRetry("TestMessage", 2);
            metrics.RecordMessageRetry("TestMessage", 10);
        });
        Assert.Null(ex);
    }

    /// <summary>
    /// Verifies RecordMessageDeadLettered does not throw.
    /// </summary>
    [Fact]
    public void RecordMessageDeadLettered_DoesNotThrow()
    {
        // Arrange
        var metrics = NoOpTransportMetrics.Instance;

        // Act & Assert
        var ex = Record.Exception(() =>
        {
            metrics.RecordMessageDeadLettered("TestMessage", "max-retries-exceeded");
            metrics.RecordMessageDeadLettered("TestMessage", "invalid-format");
            metrics.RecordMessageDeadLettered("AnotherMessage", "processing-error");
        });
        Assert.Null(ex);
    }

    /// <summary>
    /// Verifies RecordPayloadSize does not throw.
    /// </summary>
    [Fact]
    public void RecordPayloadSize_DoesNotThrow()
    {
        // Arrange
        var metrics = NoOpTransportMetrics.Instance;

        // Act & Assert
        var ex = Record.Exception(() =>
        {
            metrics.RecordPayloadSize("TestMessage", 1024, "publish");
            metrics.RecordPayloadSize("TestMessage", 2048, "consume");
            metrics.RecordPayloadSize("LargeMessage", 1_000_000, "publish");
        });
        Assert.Null(ex);
    }

    /// <summary>
    /// Verifies all methods can be called with null or empty strings.
    /// </summary>
    [Fact]
    public void Methods_WithNullOrEmptyStrings_DoNotThrow()
    {
        // Arrange
        var metrics = NoOpTransportMetrics.Instance;

        // Act & Assert
        var ex = Record.Exception(() =>
        {
            metrics.RecordMessagePublished(string.Empty, string.Empty);
            metrics.RecordMessagePublished(null!, null!);

            metrics.RecordMessageConsumed(string.Empty, string.Empty);
            metrics.RecordMessageConsumed(null!, null!);

            metrics.RecordProcessingDuration(string.Empty, TimeSpan.Zero, string.Empty);
            metrics.RecordProcessingDuration(null!, TimeSpan.Zero, null!);

            metrics.RecordMessageRetry(string.Empty, 0);
            metrics.RecordMessageRetry(null!, 0);

            metrics.RecordMessageDeadLettered(string.Empty, string.Empty);
            metrics.RecordMessageDeadLettered(null!, null!);

            metrics.RecordPayloadSize(string.Empty, 0, string.Empty);
            metrics.RecordPayloadSize(null!, 0, null!);
        });
        Assert.Null(ex);
    }

    /// <summary>
    /// Verifies all methods can be called concurrently without issues.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Methods_CalledConcurrently_DoNotThrow()
    {
        // Arrange
        var metrics = NoOpTransportMetrics.Instance;
        var tasks = new List<Task>();

        // Act - call all methods concurrently
        for (int i = 0; i < 100; i++)
        {
            tasks.Add(Task.Run(() => metrics.RecordMessagePublished("TestMessage", "queue")));
            tasks.Add(Task.Run(() => metrics.RecordMessageConsumed("TestMessage", "queue")));
            tasks.Add(Task.Run(() => metrics.RecordProcessingDuration("TestMessage", TimeSpan.FromMilliseconds(i), "success")));
            tasks.Add(Task.Run(() => metrics.RecordMessageRetry("TestMessage", i)));
            tasks.Add(Task.Run(() => metrics.RecordMessageDeadLettered("TestMessage", "reason")));
            tasks.Add(Task.Run(() => metrics.RecordPayloadSize("TestMessage", i, "publish")));
        }

        // Assert
        var ex = await Record.ExceptionAsync(() => Task.WhenAll(tasks));
        Assert.Null(ex);
    }

    /// <summary>
    /// Verifies Instance implements ITransportMetrics interface.
    /// </summary>
    [Fact]
    public void Instance_ImplementsITransportMetrics()
    {
        // Arrange
        var metrics = NoOpTransportMetrics.Instance;

        // Assert
        Assert.IsType<ITransportMetrics>(metrics, exactMatch: false);
    }

    /// <summary>
    /// Verifies constructor is private (singleton pattern).
    /// </summary>
    [Fact]
    public void Constructor_IsPrivate()
    {
        // Arrange
        var type = typeof(NoOpTransportMetrics);
        var constructors = type.GetConstructors(
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance);

        // Assert - no public constructors
        Assert.Empty(constructors);
    }

    /// <summary>
    /// Verifies RecordProcessingDuration accepts extreme values.
    /// </summary>
    [Fact]
    public void RecordProcessingDuration_WithExtremeValues_DoesNotThrow()
    {
        // Arrange
        var metrics = NoOpTransportMetrics.Instance;

        // Act & Assert
        var ex = Record.Exception(() =>
        {
            metrics.RecordProcessingDuration("Test", TimeSpan.Zero, "result");
            metrics.RecordProcessingDuration("Test", TimeSpan.MaxValue, "result");
            metrics.RecordProcessingDuration("Test", TimeSpan.MinValue, "result");
        });
        Assert.Null(ex);
    }

    /// <summary>
    /// Verifies RecordPayloadSize accepts extreme values.
    /// </summary>
    [Fact]
    public void RecordPayloadSize_WithExtremeValues_DoesNotThrow()
    {
        // Arrange
        var metrics = NoOpTransportMetrics.Instance;

        // Act & Assert
        var ex = Record.Exception(() =>
        {
            metrics.RecordPayloadSize("Test", 0, "direction");
            metrics.RecordPayloadSize("Test", long.MaxValue, "direction");
            metrics.RecordPayloadSize("Test", -1, "direction"); // Negative sizes
        });
        Assert.Null(ex);
    }

    /// <summary>
    /// Verifies RecordMessageRetry accepts zero and negative values.
    /// </summary>
    [Fact]
    public void RecordMessageRetry_WithZeroAndNegativeValues_DoesNotThrow()
    {
        // Arrange
        var metrics = NoOpTransportMetrics.Instance;

        // Act & Assert
        var ex = Record.Exception(() =>
        {
            metrics.RecordMessageRetry("Test", 0);
            metrics.RecordMessageRetry("Test", -1);
            metrics.RecordMessageRetry("Test", int.MinValue);
        });
        Assert.Null(ex);
    }
}
