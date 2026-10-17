using System.Text.Json.Serialization;

namespace HoneyDrunk.Transport.StorageQueue.Internal;

/// <summary>
/// Serializable envelope for Azure Storage Queue messages.
/// Grid-aware with NodeId, StudioId, TenantId, ProjectId, and Environment for distributed context propagation.
/// </summary>
/// <remarks>
/// This class represents the serialized form of a transport envelope that can be
/// stored in Azure Storage Queue as JSON text. The payload is base64-encoded to
/// safely transmit binary data in text format.
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
    /// Gets or sets the Node identifier for Grid topology tracking.
    /// </summary>
    [JsonPropertyName("nodeId")]
    public string? NodeId { get; set; }

    /// <summary>
    /// Gets or sets the Studio identifier for multi-tenancy.
    /// </summary>
    [JsonPropertyName("studioId")]
    public string? StudioId { get; set; }

    /// <summary>
    /// Gets or sets the Tenant identifier.
    /// </summary>
    [JsonPropertyName("tenantId")]
    public string? TenantId { get; set; }

    /// <summary>
    /// Gets or sets the Project identifier.
    /// </summary>
    [JsonPropertyName("projectId")]
    public string? ProjectId { get; set; }

    /// <summary>
    /// Gets or sets the environment identifier (dev, staging, prod).
    /// </summary>
    [JsonPropertyName("environment")]
    public string? Environment { get; set; }

    /// <summary>
    /// Gets or sets the message timestamp (UTC).
    /// </summary>
    [JsonPropertyName("timestamp")]
    public required DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the fully qualified message type name.
    /// </summary>
    [JsonPropertyName("messageType")]
    public required string MessageType { get; set; }

    /// <summary>
    /// Gets or sets the message headers (includes Grid baggage).
    /// </summary>
    [JsonPropertyName("headers")]
    public Dictionary<string, string> Headers { get; set; } = [];

    /// <summary>
    /// Gets or sets the base64-encoded payload.
    /// </summary>
    /// <remarks>
    /// The actual message payload is stored as base64 to safely transmit binary data
    /// in Azure Storage Queue's text-based format. The payload is always JSON-serialized
    /// by the transport layer before base64 encoding.
    /// </remarks>
    [JsonPropertyName("payload")]
    public required string PayloadBase64 { get; set; }

    /// <summary>
    /// Gets or sets metadata about the envelope (version, encoding, etc.).
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }
}
