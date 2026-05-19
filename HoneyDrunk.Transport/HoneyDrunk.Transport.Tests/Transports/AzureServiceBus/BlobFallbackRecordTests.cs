using HoneyDrunk.Transport.AzureServiceBus.Internal;
using System.Globalization;
using System.Text.Json;

namespace HoneyDrunk.Transport.Tests.Transports.AzureServiceBus;

/// <summary>
/// Tests for blob fallback serialization DTOs.
/// </summary>
public sealed class BlobFallbackRecordTests
{
    /// <summary>
    /// Blob fallback records serialize with the wire property names expected by fallback tooling.
    /// </summary>
    [Fact]
    public void BlobFallbackRecord_SerializesWithExpectedNames()
    {
        var record = new BlobFallbackRecord
        {
            MessageId = "msg-1",
            CorrelationId = "corr-1",
            CausationId = "cause-1",
            Timestamp = DateTimeOffset.Parse("2026-01-02T03:04:05Z", CultureInfo.InvariantCulture),
            MessageType = "Sample",
            Headers = new Dictionary<string, string> { ["h"] = "v" },
            PayloadBase64 = "cGF5bG9hZA==",
            Destination = new BlobFallbackDestination
            {
                Address = "queue/name",
                Properties = new Dictionary<string, string> { ["SessionId"] = "session-1" },
            },
            FailureTimestamp = DateTimeOffset.Parse("2026-01-02T03:05:05Z", CultureInfo.InvariantCulture),
            FailureExceptionType = typeof(InvalidOperationException).FullName,
            FailureMessage = "boom",
        };

        var json = JsonSerializer.Serialize(record);

        Assert.Contains("\"messageId\":\"msg-1\"", json);
        Assert.Contains("\"correlationId\":\"corr-1\"", json);
        Assert.Contains("\"causationId\":\"cause-1\"", json);
        Assert.Contains("\"messageType\":\"Sample\"", json);
        Assert.Contains("\"payload\":\"cGF5bG9hZA==\"", json);
        Assert.Contains("\"destination\"", json);
        Assert.Contains("\"address\":\"queue/name\"", json);
        Assert.Contains("\"properties\"", json);
        Assert.Contains("\"failureAt\"", json);
        Assert.Contains("\"failureType\"", json);
        Assert.Contains("\"failureMessage\":\"boom\"", json);
    }
}
