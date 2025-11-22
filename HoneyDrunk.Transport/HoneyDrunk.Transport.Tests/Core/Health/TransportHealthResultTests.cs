using HoneyDrunk.Transport.Health;

namespace HoneyDrunk.Transport.Tests.Core.Health;

/// <summary>
/// Tests for transport health result value object.
/// </summary>
public sealed class TransportHealthResultTests
{
    /// <summary>
    /// Verifies Healthy factory method creates healthy result.
    /// </summary>
    [Fact]
    public void Healthy_WithDescription_CreatesHealthyResult()
    {
        // Act
        var result = TransportHealthResult.Healthy("All systems operational");

        // Assert
        Assert.True(result.IsHealthy);
        Assert.Equal("All systems operational", result.Description);
        Assert.Null(result.Metadata);
        Assert.Null(result.Exception);
    }

    /// <summary>
    /// Verifies Healthy factory method accepts metadata.
    /// </summary>
    [Fact]
    public void Healthy_WithMetadata_IncludesMetadata()
    {
        // Arrange
        var metadata = new Dictionary<string, object>
        {
            ["Latency"] = 42,
            ["QueueDepth"] = 100
        };

        // Act
        var result = TransportHealthResult.Healthy("Operational", metadata);

        // Assert
        Assert.True(result.IsHealthy);
        Assert.Equal("Operational", result.Description);
        Assert.NotNull(result.Metadata);
        Assert.Equal(2, result.Metadata.Count);
        Assert.Equal(42, result.Metadata["Latency"]);
        Assert.Equal(100, result.Metadata["QueueDepth"]);
    }

    /// <summary>
    /// Verifies Unhealthy factory method creates unhealthy result.
    /// </summary>
    [Fact]
    public void Unhealthy_WithDescription_CreatesUnhealthyResult()
    {
        // Act
        var result = TransportHealthResult.Unhealthy("Service unavailable");

        // Assert
        Assert.False(result.IsHealthy);
        Assert.Equal("Service unavailable", result.Description);
        Assert.Null(result.Metadata);
        Assert.Null(result.Exception);
    }

    /// <summary>
    /// Verifies Unhealthy factory method accepts exception.
    /// </summary>
    [Fact]
    public void Unhealthy_WithException_IncludesException()
    {
        // Arrange
        var exception = new InvalidOperationException("Connection lost");

        // Act
        var result = TransportHealthResult.Unhealthy("Database unreachable", exception);

        // Assert
        Assert.False(result.IsHealthy);
        Assert.Equal("Database unreachable", result.Description);
        Assert.NotNull(result.Exception);
        Assert.Same(exception, result.Exception);
    }

    /// <summary>
    /// Verifies Unhealthy factory method accepts metadata.
    /// </summary>
    [Fact]
    public void Unhealthy_WithMetadata_IncludesMetadata()
    {
        // Arrange
        var metadata = new Dictionary<string, object>
        {
            ["RetryCount"] = 5,
            ["LastAttempt"] = DateTimeOffset.UtcNow
        };

        // Act
        var result = TransportHealthResult.Unhealthy("Retry exhausted", metadata: metadata);

        // Assert
        Assert.False(result.IsHealthy);
        Assert.NotNull(result.Metadata);
        Assert.Equal(2, result.Metadata.Count);
        Assert.Equal(5, result.Metadata["RetryCount"]);
    }

    /// <summary>
    /// Verifies Unhealthy factory method accepts both exception and metadata.
    /// </summary>
    [Fact]
    public void Unhealthy_WithExceptionAndMetadata_IncludesBoth()
    {
        // Arrange
        var exception = new TimeoutException("Operation timed out");
        var metadata = new Dictionary<string, object>
        {
            ["TimeoutMs"] = 30000
        };

        // Act
        var result = TransportHealthResult.Unhealthy("Timeout occurred", exception, metadata);

        // Assert
        Assert.False(result.IsHealthy);
        Assert.Equal("Timeout occurred", result.Description);
        Assert.NotNull(result.Exception);
        Assert.Same(exception, result.Exception);
        Assert.NotNull(result.Metadata);
        Assert.Equal(30000, result.Metadata["TimeoutMs"]);
    }

    /// <summary>
    /// Verifies result properties are correctly exposed.
    /// </summary>
    [Fact]
    public void Properties_WhenSet_AreAccessible()
    {
        // Arrange
        var exception = new InvalidOperationException("test");
        var metadata = new Dictionary<string, object> { ["key"] = "value" };

        // Act
        var healthyResult = new TransportHealthResult
        {
            IsHealthy = true,
            Description = "healthy",
            Metadata = metadata
        };

        var unhealthyResult = new TransportHealthResult
        {
            IsHealthy = false,
            Description = "unhealthy",
            Exception = exception,
            Metadata = metadata
        };

        // Assert
        Assert.True(healthyResult.IsHealthy);
        Assert.Equal("healthy", healthyResult.Description);
        Assert.Same(metadata, healthyResult.Metadata);
        Assert.Null(healthyResult.Exception);

        Assert.False(unhealthyResult.IsHealthy);
        Assert.Equal("unhealthy", unhealthyResult.Description);
        Assert.Same(exception, unhealthyResult.Exception);
        Assert.Same(metadata, unhealthyResult.Metadata);
    }

    /// <summary>
    /// Verifies empty metadata dictionary works correctly.
    /// </summary>
    [Fact]
    public void Healthy_WithEmptyMetadata_WorksCorrectly()
    {
        // Arrange
        var emptyMetadata = new Dictionary<string, object>();

        // Act
        var result = TransportHealthResult.Healthy("OK", emptyMetadata);

        // Assert
        Assert.True(result.IsHealthy);
        Assert.NotNull(result.Metadata);
        Assert.Empty(result.Metadata);
    }

    /// <summary>
    /// Verifies metadata is optional for both factory methods.
    /// </summary>
    [Fact]
    public void FactoryMethods_WithoutMetadata_WorkCorrectly()
    {
        // Act
        var healthy = TransportHealthResult.Healthy("OK");
        var unhealthy = TransportHealthResult.Unhealthy("Error");

        // Assert
        Assert.Null(healthy.Metadata);
        Assert.Null(unhealthy.Metadata);
    }

    /// <summary>
    /// Verifies exception is optional for Unhealthy.
    /// </summary>
    [Fact]
    public void Unhealthy_WithoutException_WorksCorrectly()
    {
        // Act
        var result = TransportHealthResult.Unhealthy("Service degraded");

        // Assert
        Assert.False(result.IsHealthy);
        Assert.Null(result.Exception);
    }

    /// <summary>
    /// Verifies metadata with various object types.
    /// </summary>
    [Fact]
    public void Metadata_WithVariousTypes_StoresCorrectly()
    {
        // Arrange
        var metadata = new Dictionary<string, object>
        {
            ["StringValue"] = "test",
            ["IntValue"] = 42,
            ["BoolValue"] = true,
            ["DateValue"] = DateTimeOffset.UtcNow,
            ["DoubleValue"] = 3.14
        };

        // Act
        var result = TransportHealthResult.Healthy("OK", metadata);

        // Assert
        Assert.Equal("test", result.Metadata!["StringValue"]);
        Assert.Equal(42, result.Metadata["IntValue"]);
        Assert.Equal(true, result.Metadata["BoolValue"]);
        Assert.IsType<DateTimeOffset>(result.Metadata["DateValue"]);
        Assert.Equal(3.14, result.Metadata["DoubleValue"]);
    }

    /// <summary>
    /// Verifies Description property is named consistently with pattern.
    /// </summary>
    [Fact]
    public void Description_PropertyName_IsConsistent()
    {
        // Arrange & Act
        var result = TransportHealthResult.Healthy("Test description");

        // Assert - ensure property is called Description not Message
        Assert.Equal("Test description", result.Description);
    }
}
