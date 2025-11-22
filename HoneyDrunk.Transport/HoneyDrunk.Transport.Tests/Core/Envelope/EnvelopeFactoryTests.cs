using HoneyDrunk.Transport.Primitives;

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
    /// Test time provider that returns a fixed time.
    /// </summary>
    private sealed class TestTimeProvider(DateTimeOffset fixedTime) : TimeProvider
    {
        private readonly DateTimeOffset _fixedTime = fixedTime;

        public override DateTimeOffset GetUtcNow() => _fixedTime;
    }
}
