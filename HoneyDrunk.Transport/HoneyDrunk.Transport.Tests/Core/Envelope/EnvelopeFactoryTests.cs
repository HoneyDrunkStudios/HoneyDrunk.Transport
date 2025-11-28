using HoneyDrunk.Kernel.Abstractions.Context;
using HoneyDrunk.Transport.Primitives;
using NSubstitute;

namespace HoneyDrunk.Transport.Tests.Core.Envelope;

/// <summary>
/// Tests for <see cref="EnvelopeFactory"/> covering envelope creation variants and reply semantics.
/// </summary>
public sealed class EnvelopeFactoryTests
{
    private static readonly DateTimeOffset FixedTime = new(2024, 12, 31, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Verifies basic envelope creation sets IDs, timestamp and defaults.
    /// </summary>
    [Fact]
    public void CreateEnvelope_AssignsIdsAndDefaults()
    {
        var timeProvider = new TestTimeProvider(FixedTime);
        var factory = new EnvelopeFactory(timeProvider);
        var env = factory.CreateEnvelope<EnvelopeFactoryTests>(payload: new ReadOnlyMemory<byte>([1, 2, 3]));

        Assert.NotEmpty(env.MessageId); // ULID-based ID
        Assert.Equal(typeof(EnvelopeFactoryTests).FullName, env.MessageType);
        Assert.Empty(env.Headers);
        Assert.Null(env.CorrelationId);
        Assert.Null(env.CausationId);
        Assert.Equal(FixedTime, env.Timestamp);
        Assert.Equal(new byte[] { 1, 2, 3 }, env.Payload.ToArray());
    }

    /// <summary>
    /// Ensures headers dictionary passed in is cloned (mutation safe).
    /// </summary>
    [Fact]
    public void CreateEnvelope_WithHeaders_ClonesDictionary()
    {
        var timeProvider = new TestTimeProvider(FixedTime);
        var factory = new EnvelopeFactory(timeProvider);
        var originalHeaders = new Dictionary<string, string> { { "x", "1" } };
        var env = factory.CreateEnvelope<EnvelopeFactoryTests>(ReadOnlyMemory<byte>.Empty, headers: originalHeaders);

        Assert.NotSame(originalHeaders, env.Headers); // cloned
        Assert.Equal("1", env.Headers["x"]);
        originalHeaders["x"] = "2"; // mutate original after creation
        Assert.Equal("1", env.Headers["x"]); // unchanged
    }

    /// <summary>
    /// Verifies explicit ID creation uses provided values.
    /// </summary>
    [Fact]
    public void CreateEnvelopeWithId_UsesProvidedValues()
    {
        var timeProvider = new TestTimeProvider(FixedTime);
        var factory = new EnvelopeFactory(timeProvider);
        var env = factory.CreateEnvelopeWithId("custom", "CustomType", new ReadOnlyMemory<byte>([5]));

        Assert.Equal("custom", env.MessageId);
        Assert.Equal("CustomType", env.MessageType);
        Assert.Equal(new byte[] { 5 }, env.Payload.ToArray());
    }

    /// <summary>
    /// Verifies reply creation merges headers and sets correlation/causation.
    /// </summary>
    [Fact]
    public void CreateReply_CopiesHeadersAndSetsCorrelationAndCausation()
    {
        var timeProvider = new TestTimeProvider(FixedTime);
        var factory = new EnvelopeFactory(timeProvider);
        var original = factory.CreateEnvelope<EnvelopeFactoryTests>(
            ReadOnlyMemory<byte>.Empty,
            correlationId: "corr",
            causationId: "cause",
            headers: new Dictionary<string, string> { { "a", "1" } });

        var reply = factory.CreateReply(
            original,
            "ReplyType",
            new ReadOnlyMemory<byte>([9]),
            additionalHeaders: new Dictionary<string, string> { { "b", "2" }, { "a", "3" } });

        Assert.NotEmpty(reply.MessageId);
        Assert.Equal("ReplyType", reply.MessageType);
        Assert.Equal("corr", original.CorrelationId); // original unchanged

        // Correlation should remain original.CorrelationId if set; causation should be original.MessageId.
        Assert.Equal("corr", reply.CorrelationId);
        Assert.Equal(original.MessageId, reply.CausationId);
        Assert.Equal("3", reply.Headers["a"]); // overridden
        Assert.Equal("2", reply.Headers["b"]);
        Assert.Equal("1", original.Headers["a"]); // original not mutated
    }

    /// <summary>
    /// When original has no correlation ID reply should derive correlation from original message id.
    /// </summary>
    [Fact]
    public void CreateReply_WhenOriginalHasNoCorrelationId_UsesOriginalMessageId()
    {
        var timeProvider = new TestTimeProvider(FixedTime);
        var factory = new EnvelopeFactory(timeProvider);
        var original = factory.CreateEnvelope<EnvelopeFactoryTests>(ReadOnlyMemory<byte>.Empty); // no correlation
        var reply = factory.CreateReply(original, "ReplyType", ReadOnlyMemory<byte>.Empty);

        Assert.Equal(original.MessageId, reply.CorrelationId); // fallback
        Assert.Equal(original.MessageId, reply.CausationId);
    }

    /// <summary>
    /// Verifies CreateEnvelopeWithGridContext maps TenantId and ProjectId from IGridContext.
    /// </summary>
    [Fact]
    public void CreateEnvelopeWithGridContext_MapsTenantIdAndProjectId()
    {
        // Arrange
        var timeProvider = new TestTimeProvider(FixedTime);
        var factory = new EnvelopeFactory(timeProvider);
        var gridContext = CreateTestGridContext(
            tenantId: "tenant-abc-123",
            projectId: "project-xyz-789");

        // Act
        var envelope = factory.CreateEnvelopeWithGridContext<EnvelopeFactoryTests>(
            new ReadOnlyMemory<byte>([1, 2, 3]),
            gridContext);

        // Assert
        Assert.Equal("tenant-abc-123", envelope.TenantId);
        Assert.Equal("project-xyz-789", envelope.ProjectId);
    }

    /// <summary>
    /// Verifies CreateEnvelopeWithGridContext handles null TenantId and ProjectId correctly.
    /// </summary>
    [Fact]
    public void CreateEnvelopeWithGridContext_HandlesNullTenantIdAndProjectId()
    {
        // Arrange
        var timeProvider = new TestTimeProvider(FixedTime);
        var factory = new EnvelopeFactory(timeProvider);
        var gridContext = CreateTestGridContext(
            tenantId: null,
            projectId: null);

        // Act
        var envelope = factory.CreateEnvelopeWithGridContext<EnvelopeFactoryTests>(
            new ReadOnlyMemory<byte>([1, 2, 3]),
            gridContext);

        // Assert
        Assert.Null(envelope.TenantId);
        Assert.Null(envelope.ProjectId);
    }

    /// <summary>
    /// Verifies CreateEnvelopeWithGridContext maps all Grid context fields correctly.
    /// </summary>
    [Fact]
    public void CreateEnvelopeWithGridContext_MapsAllGridContextFields()
    {
        // Arrange
        var timeProvider = new TestTimeProvider(FixedTime);
        var factory = new EnvelopeFactory(timeProvider);
        var gridContext = CreateTestGridContext(
            correlationId: "corr-123",
            causationId: "cause-456",
            nodeId: "node-789",
            studioId: "studio-abc",
            tenantId: "tenant-def",
            projectId: "project-ghi",
            environment: "production");

        // Act
        var envelope = factory.CreateEnvelopeWithGridContext<EnvelopeFactoryTests>(
            new ReadOnlyMemory<byte>([1, 2, 3]),
            gridContext);

        // Assert
        Assert.Equal("corr-123", envelope.CorrelationId);
        Assert.Equal("cause-456", envelope.CausationId);
        Assert.Equal("node-789", envelope.NodeId);
        Assert.Equal("studio-abc", envelope.StudioId);
        Assert.Equal("tenant-def", envelope.TenantId);
        Assert.Equal("project-ghi", envelope.ProjectId);
        Assert.Equal("production", envelope.Environment);
    }

    /// <summary>
    /// Verifies CreateEnvelopeWithGridContext merges Grid baggage into envelope headers.
    /// </summary>
    [Fact]
    public void CreateEnvelopeWithGridContext_MergesBaggageIntoHeaders()
    {
        // Arrange
        var timeProvider = new TestTimeProvider(FixedTime);
        var factory = new EnvelopeFactory(timeProvider);
        var baggage = new Dictionary<string, string>
        {
            ["baggage-key-1"] = "baggage-value-1",
            ["baggage-key-2"] = "baggage-value-2"
        };
        var gridContext = CreateTestGridContext(baggage: baggage);

        // Act
        var envelope = factory.CreateEnvelopeWithGridContext<EnvelopeFactoryTests>(
            new ReadOnlyMemory<byte>([1, 2, 3]),
            gridContext);

        // Assert
        Assert.Equal("baggage-value-1", envelope.Headers["baggage-key-1"]);
        Assert.Equal("baggage-value-2", envelope.Headers["baggage-key-2"]);
    }

    /// <summary>
    /// Verifies CreateEnvelopeWithGridContext merges additional headers with Grid baggage,
    /// with additional headers taking precedence.
    /// </summary>
    [Fact]
    public void CreateEnvelopeWithGridContext_MergesAdditionalHeadersWithBaggage()
    {
        // Arrange
        var timeProvider = new TestTimeProvider(FixedTime);
        var factory = new EnvelopeFactory(timeProvider);
        var baggage = new Dictionary<string, string>
        {
            ["key1"] = "from-baggage",
            ["key2"] = "also-from-baggage"
        };
        var gridContext = CreateTestGridContext(baggage: baggage);
        var additionalHeaders = new Dictionary<string, string>
        {
            ["key1"] = "overridden",  // Should override baggage
            ["key3"] = "from-headers"
        };

        // Act
        var envelope = factory.CreateEnvelopeWithGridContext<EnvelopeFactoryTests>(
            new ReadOnlyMemory<byte>([1, 2, 3]),
            gridContext,
            additionalHeaders);

        // Assert
        Assert.Equal("overridden", envelope.Headers["key1"]);       // Header overrides baggage
        Assert.Equal("also-from-baggage", envelope.Headers["key2"]); // From baggage only
        Assert.Equal("from-headers", envelope.Headers["key3"]);      // From headers only
    }

    /// <summary>
    /// Verifies CreateReply propagates TenantId and ProjectId from original envelope.
    /// </summary>
    [Fact]
    public void CreateReply_PropagatesTenantIdAndProjectId()
    {
        // Arrange
        var timeProvider = new TestTimeProvider(FixedTime);
        var factory = new EnvelopeFactory(timeProvider);
        var gridContext = CreateTestGridContext(
            tenantId: "original-tenant",
            projectId: "original-project");

        var original = factory.CreateEnvelopeWithGridContext<EnvelopeFactoryTests>(
            new ReadOnlyMemory<byte>([1, 2, 3]),
            gridContext);

        // Act
        var reply = factory.CreateReply(
            original,
            "ReplyType",
            new ReadOnlyMemory<byte>([4, 5, 6]));

        // Assert
        Assert.Equal("original-tenant", reply.TenantId);
        Assert.Equal("original-project", reply.ProjectId);
    }

    /// <summary>
    /// Verifies CreateReply propagates all Grid context fields from original envelope.
    /// </summary>
    [Fact]
    public void CreateReply_PropagatesAllGridContextFields()
    {
        // Arrange
        var timeProvider = new TestTimeProvider(FixedTime);
        var factory = new EnvelopeFactory(timeProvider);
        var gridContext = CreateTestGridContext(
            correlationId: "corr-123",
            nodeId: "node-789",
            studioId: "studio-abc",
            tenantId: "tenant-def",
            projectId: "project-ghi",
            environment: "production");

        var original = factory.CreateEnvelopeWithGridContext<EnvelopeFactoryTests>(
            new ReadOnlyMemory<byte>([1, 2, 3]),
            gridContext);

        // Act
        var reply = factory.CreateReply(
            original,
            "ReplyType",
            new ReadOnlyMemory<byte>([4, 5, 6]));

        // Assert
        Assert.Equal(original.CorrelationId, reply.CorrelationId);
        Assert.Equal(original.MessageId, reply.CausationId); // Causation links to original
        Assert.Equal(original.NodeId, reply.NodeId);
        Assert.Equal(original.StudioId, reply.StudioId);
        Assert.Equal(original.TenantId, reply.TenantId);
        Assert.Equal(original.ProjectId, reply.ProjectId);
        Assert.Equal(original.Environment, reply.Environment);
    }

    /// <summary>
    /// Verifies CreateReply handles null TenantId and ProjectId in original envelope.
    /// </summary>
    [Fact]
    public void CreateReply_HandlesNullTenantIdAndProjectIdInOriginal()
    {
        // Arrange
        var timeProvider = new TestTimeProvider(FixedTime);
        var factory = new EnvelopeFactory(timeProvider);
        var gridContext = CreateTestGridContext(
            tenantId: null,
            projectId: null);

        var original = factory.CreateEnvelopeWithGridContext<EnvelopeFactoryTests>(
            new ReadOnlyMemory<byte>([1, 2, 3]),
            gridContext);

        // Act
        var reply = factory.CreateReply(
            original,
            "ReplyType",
            new ReadOnlyMemory<byte>([4, 5, 6]));

        // Assert
        Assert.Null(reply.TenantId);
        Assert.Null(reply.ProjectId);
    }

    /// <summary>
    /// Creates a test Grid context with customizable field values.
    /// </summary>
    private static IGridContext CreateTestGridContext(
        string correlationId = "test-correlation",
        string? causationId = null,
        string nodeId = "test-node",
        string studioId = "test-studio",
        string? tenantId = null,
        string? projectId = null,
        string environment = "test",
        Dictionary<string, string>? baggage = null)
    {
        var context = Substitute.For<IGridContext>();
        context.CorrelationId.Returns(correlationId);
        context.CausationId.Returns(causationId);
        context.NodeId.Returns(nodeId);
        context.StudioId.Returns(studioId);
        context.TenantId.Returns(tenantId);
        context.ProjectId.Returns(projectId);
        context.Environment.Returns(environment);
        context.Baggage.Returns(baggage ?? []);
        context.CreatedAtUtc.Returns(FixedTime);
        return context;
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
