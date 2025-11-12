using System.Text.Json.Serialization;

namespace HoneyDrunk.Transport.StorageQueue.Internal;

/// <summary>
/// Serializable envelope for Azure Storage Queue messages.
/// </summary>
/// <remarks>
/// This class represents the serialized form of a transport envelope that can be
/// stored in Azure Storage Queue as JSON text.
/// </remarks>
internal sealed class StorageQueueEnvelope
{
    /// <summary>
    /// Gets or sets the unique message identifier.
    /// </summary>
    [JsonPropertyName("messageId")]
    public required string MessageId { get; set; }

    /// <summary>
    /// Gets or sets the correlation identifier for distributed tracing.
    /// </summary>
    [JsonPropertyName("correlationId")]
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the causation identifier (parent message ID).
    /// </summary>
    [JsonPropertyName("causationId")]
    public string? CausationId { get; set; }

    /// <summary>
    /// Gets or sets the message timestamp (UTC).
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the fully qualified message type name.
    /// </summary>
    [JsonPropertyName("messageType")]
    public required string MessageType { get; set; }

    /// <summary>
    /// Gets or sets the content type (e.g., "application/json").
    /// </summary>
    [JsonPropertyName("contentType")]
    public string? ContentType { get; set; }

    /// <summary>
    /// Gets or sets the message headers.
    /// </summary>
    [JsonPropertyName("headers")]
    public Dictionary<string, string> Headers { get; set; } = [];

    /// <summary>
    /// Gets or sets the base64-encoded payload.
    /// </summary>
    /// <remarks>
    /// The actual message payload is stored as base64 to safely transmit binary data.
    /// </remarks>
    [JsonPropertyName("payload")]
    public required string PayloadBase64 { get; set; }

    /// <summary>
    /// Gets or sets metadata about the envelope (version, encoding, etc.).
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }
}
