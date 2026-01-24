using HoneyDrunk.Transport.Context;

namespace HoneyDrunk.Transport.Tests.Core.Context;

/// <summary>
/// Tests for TransportGridContext implementation.
/// </summary>
/// <remarks>
/// <b>Note:</b> TransportGridContext is obsolete as of v0.4.0. These tests verify the deprecated
/// API still works correctly until it is removed in v0.5.0. New code should use the DI-scoped
/// GridContext from Kernel instead.
/// </remarks>
#pragma warning disable CS0618 // Type or member is obsolete - testing deprecated TransportGridContext
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
        var tenantId = "tenant-1";
        var projectId = "project-1";
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
            tenantId,
            projectId,
            environment,
            baggage,
            createdAt,
            cts.Token);

        // Assert
        Assert.Equal(correlationId, context.CorrelationId);
        Assert.Equal(causationId, context.CausationId);
        Assert.Equal(nodeId, context.NodeId);
        Assert.Equal(studioId, context.StudioId);
        Assert.Equal(tenantId, context.TenantId);
        Assert.Equal(projectId, context.ProjectId);
        Assert.Equal(environment, context.Environment);
        Assert.Equal(createdAt, context.CreatedAtUtc);
        Assert.Equal(cts.Token, context.Cancellation);
        Assert.Single(context.Baggage);
        Assert.Equal("value", context.Baggage["key"]);
    }

    /// <summary>
    /// Verifies baggage dictionary is cloned and immutable from external modification.
    /// </summary>
    [Fact]
    public void Baggage_IsClonedFromOriginal()
    {
        // Arrange
        var originalBaggage = new Dictionary<string, string> { ["key1"] = "value1" };

        var context = new TransportGridContext(
            "corr",
            "cause",
            "node",
            "studio",
            "tenant",
            "project",
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
    /// Verifies IsInitialized always returns true for TransportGridContext.
    /// </summary>
    [Fact]
    public void IsInitialized_AlwaysReturnsTrue()
    {
        // Arrange
        var context = new TransportGridContext(
            "corr",
            null,
            "node",
            "studio",
            null,
            null,
            "env",
            new Dictionary<string, string>(),
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        // Assert
        Assert.True(context.IsInitialized);
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
            "tenant-1",
            "project-1",
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
        Assert.Equal("tenant-1", childContext.TenantId);
        Assert.Equal("project-1", childContext.ProjectId);
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
            "tenant-1",
            "project-1",
            "production",
            new Dictionary<string, string>(),
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        // Act
        var childContext = parentContext.CreateChildContext(nodeId: "node-2");

        // Assert
        Assert.Equal("node-2", childContext.NodeId);
        Assert.Equal("studio-1", childContext.StudioId); // Other fields unchanged
        Assert.Equal("tenant-1", childContext.TenantId);
        Assert.Equal("project-1", childContext.ProjectId);
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
            null,
            null,
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
    /// Verifies AddBaggage adds item to existing context (mutates in place).
    /// </summary>
    [Fact]
    public void AddBaggage_AddsItemToExistingContext()
    {
        // Arrange
        var context = new TransportGridContext(
            "corr",
            "cause",
            "node",
            "studio",
            null,
            null,
            "env",
            new Dictionary<string, string> { ["key1"] = "value1" },
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        // Act
        context.AddBaggage("key2", "value2");

        // Assert - context is mutated in place
        Assert.Equal(2, context.Baggage.Count);
        Assert.Equal("value1", context.Baggage["key1"]);
        Assert.Equal("value2", context.Baggage["key2"]);
    }

    /// <summary>
    /// Verifies AddBaggage can override existing baggage item.
    /// </summary>
    [Fact]
    public void AddBaggage_OverridesExistingItem()
    {
        // Arrange
        var context = new TransportGridContext(
            "corr",
            "cause",
            "node",
            "studio",
            null,
            null,
            "env",
            new Dictionary<string, string> { ["key1"] = "original" },
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        // Act
        context.AddBaggage("key1", "updated");

        // Assert - value is updated in place
        Assert.Single(context.Baggage);
        Assert.Equal("updated", context.Baggage["key1"]);
    }

    /// <summary>
    /// Verifies AddBaggage preserves all other context properties.
    /// </summary>
    [Fact]
    public void AddBaggage_PreservesOtherProperties()
    {
        // Arrange
        var createdAt = DateTimeOffset.UtcNow;
        using var cts = new CancellationTokenSource();

        var context = new TransportGridContext(
            "corr",
            "cause",
            "node",
            "studio",
            "tenant",
            "project",
            "env",
            new Dictionary<string, string>(),
            createdAt,
            cts.Token);

        // Act
        context.AddBaggage("key", "value");

        // Assert - all properties preserved, baggage updated
        Assert.Equal("corr", context.CorrelationId);
        Assert.Equal("cause", context.CausationId);
        Assert.Equal("node", context.NodeId);
        Assert.Equal("studio", context.StudioId);
        Assert.Equal("tenant", context.TenantId);
        Assert.Equal("project", context.ProjectId);
        Assert.Equal("env", context.Environment);
        Assert.Equal(createdAt, context.CreatedAtUtc);
        Assert.Equal(cts.Token, context.Cancellation);
        Assert.Single(context.Baggage);
        Assert.Equal("value", context.Baggage["key"]);
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
            null,
            null,
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
            null,
            null,
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
