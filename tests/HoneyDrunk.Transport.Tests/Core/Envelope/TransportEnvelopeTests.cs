using HoneyDrunk.Transport.Primitives;
using HoneyDrunk.Transport.Tests.Support;

namespace HoneyDrunk.Transport.Tests.Core.Envelope;

/// <summary>
/// Tests for TransportEnvelope mutation helpers.
/// </summary>
public sealed class TransportEnvelopeTests
{
    /// <summary>
    /// Ensures WithHeaders merges and returns a new instance.
    /// </summary>
    [Fact]
    public void WithHeaders_WithAdditionalHeaders_MergesAndReturnsNewInstance()
    {
        var original = new TransportEnvelope
        {
            MessageId = "id",
            MessageType = typeof(SampleMessage).FullName!,
            Headers = new Dictionary<string, string> { ["x"] = "1" },
            Payload = Array.Empty<byte>(),
            Timestamp = DateTimeOffset.UtcNow
        };

        var updated = original.WithHeaders(new Dictionary<string, string> { ["y"] = "2", ["x"] = "3" });

        Assert.NotSame(original, updated);
        Assert.Equal("3", updated.Headers["x"]);
        Assert.Equal("2", updated.Headers["y"]);
    }

    /// <summary>
    /// Ensures WithHeaders preserves original when additional headers are added.
    /// </summary>
    [Fact]
    public void WithHeaders_PreservesOriginalEnvelope()
    {
        // Arrange
        var original = new TransportEnvelope
        {
            MessageId = "id",
            MessageType = "TestMessage",
            Headers = new Dictionary<string, string> { ["original"] = "value1" },
            Payload = new byte[] { 1, 2, 3 },
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        var updated = original.WithHeaders(new Dictionary<string, string> { ["new"] = "value2" });

        // Assert - original unchanged
        Assert.Single(original.Headers);
        Assert.Equal("value1", original.Headers["original"]);
        Assert.False(original.Headers.ContainsKey("new"));

        // Updated has both
        Assert.Equal(2, updated.Headers.Count);
        Assert.Equal("value1", updated.Headers["original"]);
        Assert.Equal("value2", updated.Headers["new"]);
    }

    /// <summary>
    /// Ensures WithHeaders with empty dictionary works correctly.
    /// </summary>
    [Fact]
    public void WithHeaders_WithEmptyDictionary_ReturnsNewInstanceWithSameHeaders()
    {
        // Arrange
        var original = new TransportEnvelope
        {
            MessageId = "id",
            MessageType = "TestMessage",
            Headers = new Dictionary<string, string> { ["key"] = "value" },
            Payload = Array.Empty<byte>(),
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        var updated = original.WithHeaders(new Dictionary<string, string>());

        // Assert
        Assert.NotSame(original, updated);
        Assert.Single(updated.Headers);
        Assert.Equal("value", updated.Headers["key"]);
    }

    /// <summary>
    /// Ensures WithHeaders preserves all other envelope properties.
    /// </summary>
    [Fact]
    public void WithHeaders_PreservesAllOtherProperties()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;
        var payload = new byte[] { 1, 2, 3 };

        var original = new TransportEnvelope
        {
            MessageId = "msg-123",
            MessageType = "TestMessage",
            CorrelationId = "corr-123",
            CausationId = "cause-123",
            NodeId = "node-1",
            StudioId = "studio-1",
            Environment = "production",
            Headers = new Dictionary<string, string> { ["original"] = "value" },
            Payload = payload,
            Timestamp = timestamp
        };

        // Act
        var updated = original.WithHeaders(new Dictionary<string, string> { ["new"] = "value2" });

        // Assert - all properties preserved except Headers
        Assert.Equal(original.MessageId, updated.MessageId);
        Assert.Equal(original.MessageType, updated.MessageType);
        Assert.Equal(original.CorrelationId, updated.CorrelationId);
        Assert.Equal(original.CausationId, updated.CausationId);
        Assert.Equal(original.NodeId, updated.NodeId);
        Assert.Equal(original.StudioId, updated.StudioId);
        Assert.Equal(original.Environment, updated.Environment);
        Assert.Equal(original.Timestamp, updated.Timestamp);
        Assert.Equal(original.Payload.ToArray(), updated.Payload.ToArray());
    }

    /// <summary>
    /// Ensures WithGridContext overrides Grid context values when provided.
    /// </summary>
    [Fact]
    public void WithGridContext_WithNewValues_OverridesExistingValues()
    {
        var original = new TransportEnvelope
        {
            MessageId = "id",
            MessageType = typeof(SampleMessage).FullName!,
            Headers = new Dictionary<string, string>(),
            Payload = Array.Empty<byte>(),
            Timestamp = DateTimeOffset.UtcNow,
            CorrelationId = "c1",
            CausationId = "ca1",
            NodeId = "node1",
            StudioId = "studio1",
            Environment = "dev"
        };

        var updated = original.WithGridContext(correlationId: "c2", nodeId: "node2");
        Assert.Equal("c2", updated.CorrelationId);
        Assert.Equal("ca1", updated.CausationId); // Not changed
        Assert.Equal("node2", updated.NodeId);
        Assert.Equal("studio1", updated.StudioId); // Not changed
        Assert.Equal("dev", updated.Environment); // Not changed
    }

    /// <summary>
    /// Ensures WithGridContext preserves original envelope immutably.
    /// </summary>
    [Fact]
    public void WithGridContext_PreservesOriginalEnvelope()
    {
        // Arrange
        var original = new TransportEnvelope
        {
            MessageId = "id",
            MessageType = "TestMessage",
            Headers = new Dictionary<string, string>(),
            Payload = Array.Empty<byte>(),
            Timestamp = DateTimeOffset.UtcNow,
            CorrelationId = "original-corr",
            CausationId = "original-cause",
            NodeId = "original-node",
            StudioId = "original-studio",
            Environment = "original-env"
        };

        // Act
        var updated = original.WithGridContext(
            correlationId: "new-corr",
            causationId: "new-cause",
            nodeId: "new-node",
            studioId: "new-studio",
            environment: "new-env");

        // Assert - original unchanged
        Assert.Equal("original-corr", original.CorrelationId);
        Assert.Equal("original-cause", original.CausationId);
        Assert.Equal("original-node", original.NodeId);
        Assert.Equal("original-studio", original.StudioId);
        Assert.Equal("original-env", original.Environment);

        // Updated has new values
        Assert.Equal("new-corr", updated.CorrelationId);
        Assert.Equal("new-cause", updated.CausationId);
        Assert.Equal("new-node", updated.NodeId);
        Assert.Equal("new-studio", updated.StudioId);
        Assert.Equal("new-env", updated.Environment);
    }

    /// <summary>
    /// Ensures WithGridContext with null parameters preserves existing values.
    /// </summary>
    [Fact]
    public void WithGridContext_WithNullParameters_PreservesExistingValues()
    {
        // Arrange
        var original = new TransportEnvelope
        {
            MessageId = "id",
            MessageType = "TestMessage",
            Headers = new Dictionary<string, string>(),
            Payload = Array.Empty<byte>(),
            Timestamp = DateTimeOffset.UtcNow,
            CorrelationId = "corr-123",
            CausationId = "cause-123",
            NodeId = "node-1",
            StudioId = "studio-1",
            Environment = "production"
        };

        // Act - pass all nulls
        var updated = original.WithGridContext();

        // Assert - all values preserved
        Assert.NotSame(original, updated);
        Assert.Equal(original.CorrelationId, updated.CorrelationId);
        Assert.Equal(original.CausationId, updated.CausationId);
        Assert.Equal(original.NodeId, updated.NodeId);
        Assert.Equal(original.StudioId, updated.StudioId);
        Assert.Equal(original.Environment, updated.Environment);
    }

    /// <summary>
    /// Ensures WithGridContext selective update works (only some parameters).
    /// </summary>
    [Fact]
    public void WithGridContext_SelectiveUpdate_OnlyUpdatesSpecifiedFields()
    {
        // Arrange
        var original = new TransportEnvelope
        {
            MessageId = "id",
            MessageType = "TestMessage",
            Headers = new Dictionary<string, string>(),
            Payload = Array.Empty<byte>(),
            Timestamp = DateTimeOffset.UtcNow,
            CorrelationId = "corr-old",
            CausationId = "cause-old",
            NodeId = "node-old",
            StudioId = "studio-old",
            Environment = "env-old"
        };

        // Act - only update correlationId and environment
        var updated = original.WithGridContext(correlationId: "corr-new", environment: "env-new");

        // Assert
        Assert.Equal("corr-new", updated.CorrelationId);
        Assert.Equal("cause-old", updated.CausationId); // Unchanged
        Assert.Equal("node-old", updated.NodeId); // Unchanged
        Assert.Equal("studio-old", updated.StudioId); // Unchanged
        Assert.Equal("env-new", updated.Environment);
    }

    /// <summary>
    /// Ensures WithGridContext preserves all non-Grid properties.
    /// </summary>
    [Fact]
    public void WithGridContext_PreservesAllNonGridProperties()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;
        var headers = new Dictionary<string, string> { ["key"] = "value" };
        var payload = new byte[] { 1, 2, 3 };

        var original = new TransportEnvelope
        {
            MessageId = "msg-123",
            MessageType = "TestMessage",
            Headers = headers,
            Payload = payload,
            Timestamp = timestamp,
            CorrelationId = "corr-old"
        };

        // Act
        var updated = original.WithGridContext(correlationId: "corr-new");

        // Assert
        Assert.Equal(original.MessageId, updated.MessageId);
        Assert.Equal(original.MessageType, updated.MessageType);
        Assert.Same(original.Headers, updated.Headers);
        Assert.Equal(original.Payload.ToArray(), updated.Payload.ToArray());
        Assert.Equal(original.Timestamp, updated.Timestamp);
    }

    /// <summary>
    /// Ensures both methods can be chained for fluent API usage.
    /// </summary>
    [Fact]
    public void FluentChaining_WithHeadersAndWithGridContext_WorksCorrectly()
    {
        // Arrange
        var original = new TransportEnvelope
        {
            MessageId = "id",
            MessageType = "TestMessage",
            Headers = new Dictionary<string, string> { ["original"] = "value" },
            Payload = Array.Empty<byte>(),
            Timestamp = DateTimeOffset.UtcNow,
            CorrelationId = "corr-old"
        };

        // Act - chain both methods
        var updated = original
            .WithHeaders(new Dictionary<string, string> { ["new"] = "header" })
            .WithGridContext(correlationId: "corr-new", nodeId: "node-1");

        // Assert
        Assert.NotSame(original, updated);
        Assert.Equal(2, updated.Headers.Count);
        Assert.Equal("header", updated.Headers["new"]);
        Assert.Equal("corr-new", updated.CorrelationId);
        Assert.Equal("node-1", updated.NodeId);
    }
}
