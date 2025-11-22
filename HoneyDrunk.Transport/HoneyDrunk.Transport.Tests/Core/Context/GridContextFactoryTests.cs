using HoneyDrunk.Transport.Context;
using HoneyDrunk.Transport.Primitives;

namespace HoneyDrunk.Transport.Tests.Core.Context;

/// <summary>
/// Tests for Grid context factory.
/// </summary>
public sealed class GridContextFactoryTests
{
    private static readonly DateTimeOffset FixedTime = new(2024, 12, 31, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Verifies factory creates Grid context from envelope with all fields populated.
    /// </summary>
    [Fact]
    public void CreateFromEnvelope_WithAllFields_CreatesGridContextWithAllProperties()
    {
        // Arrange
        var timeProvider = new TestTimeProvider(FixedTime);
        var factory = new GridContextFactory(timeProvider);

        var envelope = new TransportEnvelope
        {
            MessageId = "msg-123",
            CorrelationId = "corr-456",
            CausationId = "cause-789",
            NodeId = "node-1",
            StudioId = "studio-1",
            Environment = "production",
            Headers = new Dictionary<string, string>
            {
                ["key1"] = "value1",
                ["key2"] = "value2"
            },
            MessageType = "TestMessage",
            Payload = ReadOnlyMemory<byte>.Empty,
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        var gridContext = factory.CreateFromEnvelope(envelope, CancellationToken.None);

        // Assert
        Assert.NotNull(gridContext);
        Assert.Equal("corr-456", gridContext.CorrelationId);
        Assert.Equal("cause-789", gridContext.CausationId);
        Assert.Equal("node-1", gridContext.NodeId);
        Assert.Equal("studio-1", gridContext.StudioId);
        Assert.Equal("production", gridContext.Environment);
        Assert.Equal(2, gridContext.Baggage.Count);
        Assert.Equal("value1", gridContext.Baggage["key1"]);
        Assert.Equal("value2", gridContext.Baggage["key2"]);
        Assert.Equal(FixedTime, gridContext.CreatedAtUtc);
    }

    /// <summary>
    /// Verifies factory falls back to messageId when correlationId is missing.
    /// </summary>
    [Fact]
    public void CreateFromEnvelope_WithMissingCorrelationId_FallsBackToMessageId()
    {
        // Arrange
        var timeProvider = new TestTimeProvider(FixedTime);
        var factory = new GridContextFactory(timeProvider);

        var envelope = new TransportEnvelope
        {
            MessageId = "msg-abc",
            CorrelationId = null, // Missing
            MessageType = "TestMessage",
            Payload = ReadOnlyMemory<byte>.Empty,
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        var gridContext = factory.CreateFromEnvelope(envelope, CancellationToken.None);

        // Assert
        Assert.Equal("msg-abc", gridContext.CorrelationId);
    }

    /// <summary>
    /// Verifies factory falls back to messageId when causationId is missing.
    /// </summary>
    [Fact]
    public void CreateFromEnvelope_WithMissingCausationId_FallsBackToMessageId()
    {
        // Arrange
        var timeProvider = new TestTimeProvider(FixedTime);
        var factory = new GridContextFactory(timeProvider);

        var envelope = new TransportEnvelope
        {
            MessageId = "msg-xyz",
            CausationId = null, // Missing
            MessageType = "TestMessage",
            Payload = ReadOnlyMemory<byte>.Empty,
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        var gridContext = factory.CreateFromEnvelope(envelope, CancellationToken.None);

        // Assert
        Assert.Equal("msg-xyz", gridContext.CausationId);
    }

    /// <summary>
    /// Verifies factory creates empty baggage when headers are null.
    /// </summary>
    [Fact]
    public void CreateFromEnvelope_WithNullHeaders_CreatesEmptyBaggage()
    {
        // Arrange
        var timeProvider = new TestTimeProvider(FixedTime);
        var factory = new GridContextFactory(timeProvider);

        var envelope = new TransportEnvelope
        {
            MessageId = "msg-123",
            Headers = null!, // Null headers
            MessageType = "TestMessage",
            Payload = ReadOnlyMemory<byte>.Empty,
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        var gridContext = factory.CreateFromEnvelope(envelope, CancellationToken.None);

        // Assert
        Assert.NotNull(gridContext.Baggage);
        Assert.Empty(gridContext.Baggage);
    }

    /// <summary>
    /// Verifies factory creates empty baggage when headers are empty.
    /// </summary>
    [Fact]
    public void CreateFromEnvelope_WithEmptyHeaders_CreatesEmptyBaggage()
    {
        // Arrange
        var timeProvider = new TestTimeProvider(FixedTime);
        var factory = new GridContextFactory(timeProvider);

        var envelope = new TransportEnvelope
        {
            MessageId = "msg-123",
            Headers = new Dictionary<string, string>(), // Empty
            MessageType = "TestMessage",
            Payload = ReadOnlyMemory<byte>.Empty,
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        var gridContext = factory.CreateFromEnvelope(envelope, CancellationToken.None);

        // Assert
        Assert.NotNull(gridContext.Baggage);
        Assert.Empty(gridContext.Baggage);
    }

    /// <summary>
    /// Verifies factory defaults missing Grid fields to empty strings.
    /// </summary>
    [Fact]
    public void CreateFromEnvelope_WithMissingGridFields_DefaultsToEmptyStrings()
    {
        // Arrange
        var timeProvider = new TestTimeProvider(FixedTime);
        var factory = new GridContextFactory(timeProvider);

        var envelope = new TransportEnvelope
        {
            MessageId = "msg-123",
            NodeId = null,
            StudioId = null,
            Environment = null,
            MessageType = "TestMessage",
            Payload = ReadOnlyMemory<byte>.Empty,
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        var gridContext = factory.CreateFromEnvelope(envelope, CancellationToken.None);

        // Assert
        Assert.Equal(string.Empty, gridContext.NodeId);
        Assert.Equal(string.Empty, gridContext.StudioId);
        Assert.Equal(string.Empty, gridContext.Environment);
    }

    /// <summary>
    /// Verifies factory uses provided TimeProvider for timestamp.
    /// </summary>
    [Fact]
    public void CreateFromEnvelope_UsesTimeProviderForCreatedAtUtc()
    {
        // Arrange
        var expectedTime = new DateTimeOffset(2025, 1, 15, 10, 30, 45, TimeSpan.Zero);
        var timeProvider = new TestTimeProvider(expectedTime);
        var factory = new GridContextFactory(timeProvider);

        var envelope = new TransportEnvelope
        {
            MessageId = "msg-123",
            MessageType = "TestMessage",
            Payload = ReadOnlyMemory<byte>.Empty,
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        var gridContext = factory.CreateFromEnvelope(envelope, CancellationToken.None);

        // Assert
        Assert.Equal(expectedTime, gridContext.CreatedAtUtc);
    }

    /// <summary>
    /// Verifies factory passes cancellation token to Grid context.
    /// </summary>
    [Fact]
    public void CreateFromEnvelope_PassesCancellationTokenToGridContext()
    {
        // Arrange
        var timeProvider = new TestTimeProvider(FixedTime);
        var factory = new GridContextFactory(timeProvider);

        var envelope = new TransportEnvelope
        {
            MessageId = "msg-123",
            MessageType = "TestMessage",
            Payload = ReadOnlyMemory<byte>.Empty,
            Timestamp = DateTimeOffset.UtcNow
        };

        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        // Act
        var gridContext = factory.CreateFromEnvelope(envelope, token);

        // Assert
        Assert.Equal(token, gridContext.Cancellation);
    }

    /// <summary>
    /// Verifies factory clones headers dictionary (mutation safety).
    /// </summary>
    [Fact]
    public void CreateFromEnvelope_ClonesHeadersDictionary_MutationSafe()
    {
        // Arrange
        var timeProvider = new TestTimeProvider(FixedTime);
        var factory = new GridContextFactory(timeProvider);

        var originalHeaders = new Dictionary<string, string>
        {
            ["key1"] = "original"
        };

        var envelope = new TransportEnvelope
        {
            MessageId = "msg-123",
            Headers = originalHeaders,
            MessageType = "TestMessage",
            Payload = ReadOnlyMemory<byte>.Empty,
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        var gridContext = factory.CreateFromEnvelope(envelope, CancellationToken.None);

        // Mutate original headers after creation
        originalHeaders["key1"] = "mutated";
        originalHeaders["key2"] = "added";

        // Assert - Grid context baggage should not be affected
        Assert.Equal("original", gridContext.Baggage["key1"]);
        Assert.False(gridContext.Baggage.ContainsKey("key2"));
    }

    /// <summary>
    /// Verifies factory works with empty string Grid fields.
    /// </summary>
    [Fact]
    public void CreateFromEnvelope_WithEmptyStringGridFields_PreservesEmptyStrings()
    {
        // Arrange
        var timeProvider = new TestTimeProvider(FixedTime);
        var factory = new GridContextFactory(timeProvider);

        var envelope = new TransportEnvelope
        {
            MessageId = "msg-123",
            NodeId = string.Empty,
            StudioId = string.Empty,
            Environment = string.Empty,
            MessageType = "TestMessage",
            Payload = ReadOnlyMemory<byte>.Empty,
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        var gridContext = factory.CreateFromEnvelope(envelope, CancellationToken.None);

        // Assert
        Assert.Equal(string.Empty, gridContext.NodeId);
        Assert.Equal(string.Empty, gridContext.StudioId);
        Assert.Equal(string.Empty, gridContext.Environment);
    }

    /// <summary>
    /// Verifies factory handles both correlation and causation being null.
    /// </summary>
    [Fact]
    public void CreateFromEnvelope_WithBothIdsNull_FallsBackToMessageId()
    {
        // Arrange
        var timeProvider = new TestTimeProvider(FixedTime);
        var factory = new GridContextFactory(timeProvider);

        var envelope = new TransportEnvelope
        {
            MessageId = "msg-fallback",
            CorrelationId = null,
            CausationId = null,
            MessageType = "TestMessage",
            Payload = ReadOnlyMemory<byte>.Empty,
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        var gridContext = factory.CreateFromEnvelope(envelope, CancellationToken.None);

        // Assert
        Assert.Equal("msg-fallback", gridContext.CorrelationId);
        Assert.Equal("msg-fallback", gridContext.CausationId);
    }

    /// <summary>
    /// Verifies factory creates Grid context that can be used multiple times.
    /// </summary>
    [Fact]
    public void CreateFromEnvelope_CalledMultipleTimes_CreatesIndependentContexts()
    {
        // Arrange
        var timeProvider = new TestTimeProvider(FixedTime);
        var factory = new GridContextFactory(timeProvider);

        var envelope1 = new TransportEnvelope
        {
            MessageId = "msg-1",
            CorrelationId = "corr-1",
            MessageType = "TestMessage",
            Payload = ReadOnlyMemory<byte>.Empty,
            Timestamp = DateTimeOffset.UtcNow
        };

        var envelope2 = new TransportEnvelope
        {
            MessageId = "msg-2",
            CorrelationId = "corr-2",
            MessageType = "TestMessage",
            Payload = ReadOnlyMemory<byte>.Empty,
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        var context1 = factory.CreateFromEnvelope(envelope1, CancellationToken.None);
        var context2 = factory.CreateFromEnvelope(envelope2, CancellationToken.None);

        // Assert - contexts should be independent
        Assert.NotSame(context1, context2);
        Assert.Equal("corr-1", context1.CorrelationId);
        Assert.Equal("corr-2", context2.CorrelationId);
    }

    /// <summary>
    /// Verifies factory handles large headers dictionary.
    /// </summary>
    [Fact]
    public void CreateFromEnvelope_WithLargeHeadersDictionary_CopiesAllItems()
    {
        // Arrange
        var timeProvider = new TestTimeProvider(FixedTime);
        var factory = new GridContextFactory(timeProvider);

        var headers = new Dictionary<string, string>();
        for (int i = 0; i < 100; i++)
        {
            headers[$"key{i}"] = $"value{i}";
        }

        var envelope = new TransportEnvelope
        {
            MessageId = "msg-123",
            Headers = headers,
            MessageType = "TestMessage",
            Payload = ReadOnlyMemory<byte>.Empty,
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        var gridContext = factory.CreateFromEnvelope(envelope, CancellationToken.None);

        // Assert
        Assert.Equal(100, gridContext.Baggage.Count);
        for (int i = 0; i < 100; i++)
        {
            Assert.Equal($"value{i}", gridContext.Baggage[$"key{i}"]);
        }
    }

    /// <summary>
    /// Test time provider that returns a fixed time.
    /// </summary>
    private sealed class TestTimeProvider(DateTimeOffset fixedTime) : TimeProvider
    {
        private readonly DateTimeOffset _fixedTime = fixedTime;

        public override DateTimeOffset GetUtcNow() => _fixedTime;
    }
}
