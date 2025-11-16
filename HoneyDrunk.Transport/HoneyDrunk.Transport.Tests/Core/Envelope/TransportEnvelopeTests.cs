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
    /// Ensures WithCorrelation overrides correlation values when provided.
    /// </summary>
    [Fact]
    public void WithCorrelation_WithNewCorrelationId_OverridesExistingValue()
    {
        var original = new TransportEnvelope
        {
            MessageId = "id",
            MessageType = typeof(SampleMessage).FullName!,
            Headers = new Dictionary<string, string>(),
            Payload = Array.Empty<byte>(),
            Timestamp = DateTimeOffset.UtcNow,
            CorrelationId = "c1",
            CausationId = "ca1"
        };

        var updated = original.WithCorrelation("c2", null);
        Assert.Equal("c2", updated.CorrelationId);
        Assert.Equal("ca1", updated.CausationId);
    }
}
