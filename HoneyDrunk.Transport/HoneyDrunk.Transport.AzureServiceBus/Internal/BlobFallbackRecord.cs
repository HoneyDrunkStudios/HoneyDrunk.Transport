using System.Text.Json.Serialization;

namespace HoneyDrunk.Transport.AzureServiceBus.Internal;

/// <summary>
/// Serializable record persisted to Blob Storage when Service Bus publish fails.
/// </summary>
internal sealed class BlobFallbackRecord
{
    [JsonPropertyName("messageId")]
    public required string MessageId { get; set; }

    [JsonPropertyName("correlationId")]
    public string? CorrelationId { get; set; }

    [JsonPropertyName("causationId")]
    public string? CausationId { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; }

    [JsonPropertyName("messageType")]
    public required string MessageType { get; set; }

    [JsonPropertyName("headers")]
    public Dictionary<string, string> Headers { get; set; } = [];

    [JsonPropertyName("payload")]
    public required string PayloadBase64 { get; set; }

    [JsonPropertyName("destination")]
    public required BlobFallbackDestination Destination { get; set; }

    [JsonPropertyName("failureAt")]
    public DateTimeOffset FailureTimestamp { get; set; }

    [JsonPropertyName("failureType")]
    public string? FailureExceptionType { get; set; }

    [JsonPropertyName("failureMessage")]
    public string? FailureMessage { get; set; }
}
