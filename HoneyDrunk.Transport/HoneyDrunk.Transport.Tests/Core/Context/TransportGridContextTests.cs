using HoneyDrunk.Transport.Context;

namespace HoneyDrunk.Transport.Tests.Core.Context;

/// <summary>
/// Tests for TransportGridContext implementation.
/// </summary>
public sealed class TransportGridContextTests
{
    /// <summary>
    /// Verifies all property getters return expected values.
    /// </summary>
    [Fact]
    public void Properties_WhenSet_ReturnExpectedValues()
    {
        // Arrange
        var correlationId = "corr-123";
        var causationId = "cause-456";
        var nodeId = "node-1";
        var studioId = "studio-1";
        var environment = "production";
        var baggage = new Dictionary<string, string> { ["key"] = "value" };
        var createdAt = DateTimeOffset.UtcNow;
        using var cts = new CancellationTokenSource();

        // Act
        var context = new TransportGridContext(
            correlationId,
            causationId,
            nodeId,
            studioId,
            environment,
            baggage,
            createdAt,
            cts.Token);

        // Assert
        Assert.Equal(correlationId, context.CorrelationId);
        Assert.Equal(causationId, context.CausationId);
        Assert.Equal(nodeId, context.NodeId);
        Assert.Equal(studioId, context.StudioId);
        Assert.Equal(environment, context.Environment);
        Assert.Equal(createdAt, context.CreatedAtUtc);
        Assert.Equal(cts.Token, context.Cancellation);
        Assert.Single(context.Baggage);
        Assert.Equal("value", context.Baggage["key"]);
    }

    /// <summary>
    /// Verifies baggage dictionary is cloned and immutable.
    /// </summary>
    [Fact]
    public void Baggage_IsClonedAndImmutable()
    {
        // Arrange
        var originalBaggage = new Dictionary<string, string> { ["key1"] = "value1" };

        var context = new TransportGridContext(
            "corr",
            "cause",
            "node",
            "studio",
            "env",
            originalBaggage,
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        // Act - mutate original after construction
        originalBaggage["key1"] = "mutated";
        originalBaggage["key2"] = "added";

        // Assert - context baggage should not be affected
        Assert.Single(context.Baggage);
        Assert.Equal("value1", context.Baggage["key1"]);
        Assert.False(context.Baggage.ContainsKey("key2"));
    }

    /// <summary>
    /// Verifies BeginScope returns a disposable scope.
    /// </summary>
    [Fact]
    public void BeginScope_ReturnsDisposableScope()
    {
        // Arrange
        var context = new TransportGridContext(
            "corr",
            null,
            "node",
            "studio",
            "env",
            new Dictionary<string, string>(),
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        // Act
        var scope = context.BeginScope();

        // Assert
        Assert.NotNull(scope);
        scope.Dispose(); // Should not throw
    }

    /// <summary>
    /// Verifies BeginScope can be disposed multiple times safely.
    /// </summary>
    [Fact]
    public void BeginScope_MultipleDispose_DoesNotThrow()
    {
        // Arrange
        var context = new TransportGridContext(
            "corr",
            null,
            "node",
            "studio",
            "env",
            new Dictionary<string, string>(),
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        var scope = context.BeginScope();

        // Act & Assert - multiple disposes should not throw
        scope.Dispose();
        scope.Dispose();
        scope.Dispose();
    }

    /// <summary>
    /// Verifies CreateChildContext creates new context with current correlation as causation.
    /// </summary>
    [Fact]
    public void CreateChildContext_CreatesContextWithCorrelationAsCausation()
    {
        // Arrange
        var parentContext = new TransportGridContext(
            "parent-corr",
            "parent-cause",
            "node-1",
            "studio-1",
            "production",
            new Dictionary<string, string> { ["key"] = "value" },
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        // Act
        var childContext = parentContext.CreateChildContext();

        // Assert
        Assert.Equal("parent-corr", childContext.CorrelationId); // Correlation flows through
        Assert.Equal("parent-corr", childContext.CausationId); // Parent correlation becomes causation
        Assert.Equal("node-1", childContext.NodeId); // Inherited
        Assert.Equal("studio-1", childContext.StudioId);
        Assert.Equal("production", childContext.Environment);
        Assert.Single(childContext.Baggage);
        Assert.Equal("value", childContext.Baggage["key"]);
    }

    /// <summary>
    /// Verifies CreateChildContext can override nodeId.
    /// </summary>
    [Fact]
    public void CreateChildContext_WithNodeId_OverridesNodeId()
    {
        // Arrange
        var parentContext = new TransportGridContext(
            "corr",
            "cause",
            "node-1",
            "studio-1",
            "production",
            new Dictionary<string, string>(),
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        // Act
        var childContext = parentContext.CreateChildContext(nodeId: "node-2");

        // Assert
        Assert.Equal("node-2", childContext.NodeId);
        Assert.Equal("studio-1", childContext.StudioId); // Other fields unchanged
        Assert.Equal("production", childContext.Environment);
    }

    /// <summary>
    /// Verifies CreateChildContext preserves baggage immutably.
    /// </summary>
    [Fact]
    public void CreateChildContext_PreservesBaggageImmutably()
    {
        // Arrange
        var parentContext = new TransportGridContext(
            "corr",
            "cause",
            "node",
            "studio",
            "env",
            new Dictionary<string, string> { ["key1"] = "value1" },
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        // Act
        var childContext = parentContext.CreateChildContext();

        // Modify parent's baggage (simulate mutation attempt)
        // Note: Parent baggage is IReadOnlyDictionary so we can't modify it directly
        // But we verify child has its own copy
        Assert.NotSame(parentContext.Baggage, childContext.Baggage);
        Assert.Equal(parentContext.Baggage["key1"], childContext.Baggage["key1"]);
    }

    /// <summary>
    /// Verifies WithBaggage creates new context with added baggage item.
    /// </summary>
    [Fact]
    public void WithBaggage_CreatesNewContextWithAddedItem()
    {
        // Arrange
        var originalContext = new TransportGridContext(
            "corr",
            "cause",
            "node",
            "studio",
            "env",
            new Dictionary<string, string> { ["key1"] = "value1" },
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        // Act
        var newContext = originalContext.WithBaggage("key2", "value2");

        // Assert - original context unchanged
        Assert.Single(originalContext.Baggage);
        Assert.Equal("value1", originalContext.Baggage["key1"]);
        Assert.False(originalContext.Baggage.ContainsKey("key2"));

        // New context has both items
        Assert.Equal(2, newContext.Baggage.Count);
        Assert.Equal("value1", newContext.Baggage["key1"]);
        Assert.Equal("value2", newContext.Baggage["key2"]);
    }

    /// <summary>
    /// Verifies WithBaggage can override existing baggage item.
    /// </summary>
    [Fact]
    public void WithBaggage_OverridesExistingItem()
    {
        // Arrange
        var originalContext = new TransportGridContext(
            "corr",
            "cause",
            "node",
            "studio",
            "env",
            new Dictionary<string, string> { ["key1"] = "original" },
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        // Act
        var newContext = originalContext.WithBaggage("key1", "updated");

        // Assert - original context unchanged
        Assert.Equal("original", originalContext.Baggage["key1"]);

        // New context has updated value
        Assert.Single(newContext.Baggage);
        Assert.Equal("updated", newContext.Baggage["key1"]);
    }

    /// <summary>
    /// Verifies WithBaggage preserves all other context properties.
    /// </summary>
    [Fact]
    public void WithBaggage_PreservesOtherProperties()
    {
        // Arrange
        var createdAt = DateTimeOffset.UtcNow;
        using var cts = new CancellationTokenSource();

        var originalContext = new TransportGridContext(
            "corr",
            "cause",
            "node",
            "studio",
            "env",
            new Dictionary<string, string>(),
            createdAt,
            cts.Token);

        // Act
        var newContext = originalContext.WithBaggage("key", "value");

        // Assert - all properties preserved except baggage
        Assert.Equal(originalContext.CorrelationId, newContext.CorrelationId);
        Assert.Equal(originalContext.CausationId, newContext.CausationId);
        Assert.Equal(originalContext.NodeId, newContext.NodeId);
        Assert.Equal(originalContext.StudioId, newContext.StudioId);
        Assert.Equal(originalContext.Environment, newContext.Environment);
        Assert.Equal(originalContext.CreatedAtUtc, newContext.CreatedAtUtc);
        Assert.Equal(originalContext.Cancellation, newContext.Cancellation);
    }

    /// <summary>
    /// Verifies context works with null causationId.
    /// </summary>
    [Fact]
    public void Constructor_WithNullCausationId_WorksCorrectly()
    {
        // Arrange & Act
        var context = new TransportGridContext(
            "corr",
            null, // Null causation
            "node",
            "studio",
            "env",
            new Dictionary<string, string>(),
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        // Assert
        Assert.Equal("corr", context.CorrelationId);
        Assert.Null(context.CausationId);
    }

    /// <summary>
    /// Verifies CreateChildContext preserves CreatedAtUtc and Cancellation.
    /// </summary>
    [Fact]
    public void CreateChildContext_PreservesCreatedAtUtcAndCancellation()
    {
        // Arrange
        var createdAt = new DateTimeOffset(2024, 6, 15, 10, 0, 0, TimeSpan.Zero);
        using var cts = new CancellationTokenSource();

        var parentContext = new TransportGridContext(
            "corr",
            "cause",
            "node",
            "studio",
            "env",
            new Dictionary<string, string>(),
            createdAt,
            cts.Token);

        // Act
        var childContext = parentContext.CreateChildContext();

        // Assert
        Assert.Equal(createdAt, childContext.CreatedAtUtc);
        Assert.Equal(cts.Token, childContext.Cancellation);
    }
}
