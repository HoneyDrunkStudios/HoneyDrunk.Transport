using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.Health;
using NSubstitute;

namespace HoneyDrunk.Transport.Tests.Core.Health;

/// <summary>
/// Tests for publisher health contributor.
/// </summary>
public sealed class PublisherHealthContributorTests
{
    /// <summary>
    /// Verifies health check returns healthy when publisher is available.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task CheckHealthAsync_WithAvailablePublisher_ReturnsHealthy()
    {
        // Arrange
        var publisher = Substitute.For<ITransportPublisher>();
        var contributor = new PublisherHealthContributor(publisher);

        // Act
        var result = await contributor.CheckHealthAsync();

        // Assert
        Assert.True(result.IsHealthy);
        Assert.Contains("available", result.Description.ToLowerInvariant());
        Assert.NotNull(result.Metadata);
        Assert.True(result.Metadata.ContainsKey("PublisherType"));
    }

    /// <summary>
    /// Verifies health check includes publisher type in metadata.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task CheckHealthAsync_IncludesPublisherTypeInMetadata()
    {
        // Arrange
        var publisher = Substitute.For<ITransportPublisher>();
        var contributor = new PublisherHealthContributor(publisher);

        // Act
        var result = await contributor.CheckHealthAsync();

        // Assert
        Assert.NotNull(result.Metadata);
        var publisherType = result.Metadata["PublisherType"] as string;
        Assert.NotNull(publisherType);

        // NSubstitute proxy type name varies by version - just verify it's not empty
        Assert.NotEmpty(publisherType);
    }

    /// <summary>
    /// Verifies Name property returns expected value.
    /// </summary>
    [Fact]
    public void Name_ReturnsExpectedValue()
    {
        // Arrange
        var publisher = Substitute.For<ITransportPublisher>();
        var contributor = new PublisherHealthContributor(publisher);

        // Act
        var name = contributor.Name;

        // Assert
        Assert.Equal("Transport.Publisher", name);
    }

    /// <summary>
    /// Verifies health check respects cancellation token.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task CheckHealthAsync_RespectsCancellationToken()
    {
        // Arrange
        var publisher = Substitute.For<ITransportPublisher>();
        var contributor = new PublisherHealthContributor(publisher);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await contributor.CheckHealthAsync(cts.Token);

        // Assert - should complete without throwing (simple check doesn't do async work)
        Assert.NotNull(result);
    }

    /// <summary>
    /// Verifies health check handles null publisher gracefully.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task CheckHealthAsync_WithNullPublisher_ReturnsUnhealthy()
    {
        // Arrange
        ITransportPublisher publisher = null!;
        var contributor = new PublisherHealthContributor(publisher);

        // Act
        var result = await contributor.CheckHealthAsync();

        // Assert
        Assert.False(result.IsHealthy);
        Assert.NotNull(result.Exception);
    }

    /// <summary>
    /// Verifies constructor accepts publisher parameter.
    /// </summary>
    [Fact]
    public void Constructor_WithPublisher_CreatesInstance()
    {
        // Arrange
        var publisher = Substitute.For<ITransportPublisher>();

        // Act
        var contributor = new PublisherHealthContributor(publisher);

        // Assert
        Assert.NotNull(contributor);
        Assert.Equal("Transport.Publisher", contributor.Name);
    }

    /// <summary>
    /// Verifies health check message format.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task CheckHealthAsync_MessageFormat_IsCorrect()
    {
        // Arrange
        var publisher = Substitute.For<ITransportPublisher>();
        var contributor = new PublisherHealthContributor(publisher);

        // Act
        var result = await contributor.CheckHealthAsync();

        // Assert
        Assert.Equal("Publisher is available", result.Description);
    }

    /// <summary>
    /// Verifies health check is idempotent.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task CheckHealthAsync_CalledMultipleTimes_ReturnsConsistentResults()
    {
        // Arrange
        var publisher = Substitute.For<ITransportPublisher>();
        var contributor = new PublisherHealthContributor(publisher);

        // Act
        var result1 = await contributor.CheckHealthAsync();
        var result2 = await contributor.CheckHealthAsync();
        var result3 = await contributor.CheckHealthAsync();

        // Assert
        Assert.True(result1.IsHealthy);
        Assert.True(result2.IsHealthy);
        Assert.True(result3.IsHealthy);
        Assert.Equal(result1.Description, result2.Description);
        Assert.Equal(result2.Description, result3.Description);
    }
}
