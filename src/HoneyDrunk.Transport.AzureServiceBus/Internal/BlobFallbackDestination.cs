using System.Text.Json.Serialization;

namespace HoneyDrunk.Transport.AzureServiceBus.Internal;

/// <summary>
/// Destination metadata persisted alongside blob fallback records.
/// </summary>
internal sealed class BlobFallbackDestination
{
    [JsonPropertyName("address")]
    public required string Address { get; set; }

    [JsonPropertyName("properties")]
    public Dictionary<string, string> Properties { get; set; } = [];
}
