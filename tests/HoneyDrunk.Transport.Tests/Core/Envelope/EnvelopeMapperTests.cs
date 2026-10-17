using HoneyDrunk.Transport.Tests.Support;

namespace HoneyDrunk.Transport.Tests.Core.Envelope;

/// <summary>
/// Tests for Azure Service Bus envelope mapping.
/// </summary>
public sealed class EnvelopeMapperTests
{
    /// <summary>
    /// Ensures ToServiceBusMessage maps headers and payload.
    /// </summary>
    [Fact]
    public void ToServiceBusMessage_WithTransportEnvelope_MapsHeadersAndPayloadCorrectly()
    {
        var original = new HoneyDrunk.Transport.Primitives.TransportEnvelope
        {
            MessageId = Guid.NewGuid().ToString("N"),
            CorrelationId = Guid.NewGuid().ToString("N"),
            CausationId = Guid.NewGuid().ToString("N"),
            Timestamp = DateTimeOffset.UtcNow,
            MessageType = typeof(SampleMessage).AssemblyQualifiedName!,
            Headers = new Dictionary<string, string> { ["k1"] = "v1" },
            Payload = new byte[] { 1, 2, 3 }
        };

        var sb = HoneyDrunk.Transport.AzureServiceBus.Mapping.EnvelopeMapper.ToServiceBusMessage(original);

        Assert.Equal(original.MessageId, sb.MessageId);
        Assert.Equal(original.CorrelationId, sb.CorrelationId);
        Assert.Equal(original.MessageType, sb.Subject);
        Assert.Equal("application/octet-stream", sb.ContentType);
        Assert.Equal(original.Payload.ToArray(), sb.Body.ToArray());
        Assert.Equal(original.MessageType, (string)sb.ApplicationProperties["MessageType"]);
        Assert.Equal("v1", (string)sb.ApplicationProperties["k1"]);
        Assert.True(sb.ApplicationProperties.ContainsKey("Timestamp"));
    }
}
