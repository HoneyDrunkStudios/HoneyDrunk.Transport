using HoneyDrunk.Transport.Health;
using HoneyDrunk.Transport.Outbox;
using NSubstitute;

namespace HoneyDrunk.Transport.Tests.Core.Health;

/// <summary>
/// Tests for outbox health contributor.
/// </summary>
public sealed class OutboxHealthContributorTests
{
    /// <summary>
    /// Verifies health check returns healthy when no pending messages.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task CheckHealthAsync_WithNoPendingMessages_ReturnsHealthy()
    {
        // Arrange
        var outboxStore = Substitute.For<IOutboxStore>();
        outboxStore.LoadPendingAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);

        var contributor = new OutboxHealthContributor(outboxStore);

        // Act
        var result = await contributor.CheckHealthAsync();

        // Assert
        Assert.True(result.IsHealthy);
        Assert.Contains("no pending messages", result.Description);
        Assert.NotNull(result.Metadata);
        Assert.Equal(0, result.Metadata["PendingCount"]);
    }

    /// <summary>
    /// Verifies health check returns healthy with warning metadata when approaching threshold.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task CheckHealthAsync_WithPendingBelowWarning_ReturnsHealthy()
    {
        // Arrange
        var outboxStore = Substitute.For<IOutboxStore>();
        var messages = Enumerable.Range(0, 500)
            .Select(_ => Substitute.For<IOutboxMessage>())
            .ToList();

        outboxStore.LoadPendingAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(messages);

        var contributor = new OutboxHealthContributor(outboxStore);

        // Act
        var result = await contributor.CheckHealthAsync();

        // Assert
        Assert.True(result.IsHealthy);
        Assert.Contains("500 pending messages", result.Description);
        Assert.Equal(500, result.Metadata!["PendingCount"]);
        Assert.Equal(1000, result.Metadata["WarningThreshold"]);
    }

    /// <summary>
    /// Verifies health check returns healthy with warning when at warning threshold.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task CheckHealthAsync_AtWarningThreshold_ReturnsHealthyWithWarning()
    {
        // Arrange
        var outboxStore = Substitute.For<IOutboxStore>();
        var messages = Enumerable.Range(0, 1000)
            .Select(_ => Substitute.For<IOutboxMessage>())
            .ToList();

        outboxStore.LoadPendingAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(messages);

        var contributor = new OutboxHealthContributor(outboxStore);

        // Act
        var result = await contributor.CheckHealthAsync();

        // Assert
        Assert.True(result.IsHealthy);
        Assert.Contains("1000 pending messages", result.Description);
        Assert.Equal(1000, result.Metadata!["PendingCount"]);
        Assert.True(result.Metadata.TryGetValue("Warning", out var warning));
        Assert.Contains("1000/1000", warning?.ToString());
    }

    /// <summary>
    /// Verifies health check returns healthy with warning when above warning threshold.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task CheckHealthAsync_AboveWarningThreshold_ReturnsHealthyWithWarning()
    {
        // Arrange
        var outboxStore = Substitute.For<IOutboxStore>();
        var messages = Enumerable.Range(0, 5000)
            .Select(_ => Substitute.For<IOutboxMessage>())
            .ToList();

        outboxStore.LoadPendingAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(messages);

        var contributor = new OutboxHealthContributor(outboxStore);

        // Act
        var result = await contributor.CheckHealthAsync();

        // Assert
        Assert.True(result.IsHealthy);
        Assert.Contains("5000 pending messages", result.Description);
        Assert.Equal(5000, result.Metadata!["PendingCount"]);
        Assert.True(result.Metadata.ContainsKey("Warning"));
    }

    /// <summary>
    /// Verifies health check returns unhealthy at critical threshold.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task CheckHealthAsync_AtCriticalThreshold_ReturnsUnhealthy()
    {
        // Arrange
        var outboxStore = Substitute.For<IOutboxStore>();
        var messages = Enumerable.Range(0, 10000)
            .Select(_ => Substitute.For<IOutboxMessage>())
            .ToList();

        outboxStore.LoadPendingAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(messages);

        var contributor = new OutboxHealthContributor(outboxStore);

        // Act
        var result = await contributor.CheckHealthAsync();

        // Assert
        Assert.False(result.IsHealthy);
        Assert.Contains("critical", result.Description.ToLowerInvariant());
        Assert.Contains("10000", result.Description);
        Assert.Equal(10000, result.Metadata!["PendingCount"]);
        Assert.Equal(10000, result.Metadata["CriticalThreshold"]);
    }

    /// <summary>
    /// Verifies health check returns unhealthy above critical threshold.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task CheckHealthAsync_AboveCriticalThreshold_ReturnsUnhealthy()
    {
        // Arrange
        var outboxStore = Substitute.For<IOutboxStore>();
        var messages = Enumerable.Range(0, 15000)
            .Select(_ => Substitute.For<IOutboxMessage>())
            .ToList();

        outboxStore.LoadPendingAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(messages);

        var contributor = new OutboxHealthContributor(outboxStore);

        // Act
        var result = await contributor.CheckHealthAsync();

        // Assert
        Assert.False(result.IsHealthy);
        Assert.Contains("15000", result.Description);
        Assert.Equal(15000, result.Metadata!["PendingCount"]);
    }

    /// <summary>
    /// Verifies health check returns unhealthy when outbox store throws exception.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task CheckHealthAsync_WhenStoreThrows_ReturnsUnhealthy()
    {
        // Arrange
        var outboxStore = Substitute.For<IOutboxStore>();
        var exception = new InvalidOperationException("Database connection failed");

        outboxStore.LoadPendingAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns<IEnumerable<IOutboxMessage>>(_ => throw exception);

        var contributor = new OutboxHealthContributor(outboxStore);

        // Act
        var result = await contributor.CheckHealthAsync();

        // Assert
        Assert.False(result.IsHealthy);
        Assert.Contains("failed", result.Description.ToLowerInvariant());
        Assert.NotNull(result.Exception);
        Assert.Same(exception, result.Exception);
    }

    /// <summary>
    /// Verifies Name property returns expected value.
    /// </summary>
    [Fact]
    public void Name_ReturnsExpectedValue()
    {
        // Arrange
        var outboxStore = Substitute.For<IOutboxStore>();
        var contributor = new OutboxHealthContributor(outboxStore);

        // Act
        var name = contributor.Name;

        // Assert
        Assert.Equal("Transport.Outbox", name);
    }

    /// <summary>
    /// Verifies health check passes cancellation token to outbox store.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task CheckHealthAsync_PassesCancellationToken()
    {
        // Arrange
        var outboxStore = Substitute.For<IOutboxStore>();
        outboxStore.LoadPendingAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);

        var contributor = new OutboxHealthContributor(outboxStore);
        using var cts = new CancellationTokenSource();

        // Act
        await contributor.CheckHealthAsync(cts.Token);

        // Assert
        await outboxStore.Received(1).LoadPendingAsync(Arg.Any<int>(), cts.Token);
    }

    /// <summary>
    /// Verifies health check includes threshold metadata.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task CheckHealthAsync_IncludesThresholdMetadata()
    {
        // Arrange
        var outboxStore = Substitute.For<IOutboxStore>();
        outboxStore.LoadPendingAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);

        var contributor = new OutboxHealthContributor(outboxStore);

        // Act
        var result = await contributor.CheckHealthAsync();

        // Assert
        Assert.NotNull(result.Metadata);
        Assert.True(result.Metadata.TryGetValue("WarningThreshold", out var warningThreshold));
        Assert.True(result.Metadata.TryGetValue("CriticalThreshold", out var criticalThreshold));
        Assert.Equal(1000, warningThreshold);
        Assert.Equal(10000, criticalThreshold);
    }
}
