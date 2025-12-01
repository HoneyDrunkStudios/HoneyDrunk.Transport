namespace HoneyDrunk.Transport.Abstractions;

/// <summary>
/// Default implementation of endpoint address with typed metadata.
/// </summary>
public sealed class EndpointAddress : IEndpointAddress
{
    /// <summary>
    /// Gets or initializes the logical name of the endpoint.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets or initializes the transport-specific address.
    /// </summary>
    public string Address { get; init; } = string.Empty;

    /// <summary>
    /// Gets or initializes the session identifier for session-based processing.
    /// </summary>
    public string? SessionId { get; init; }

    /// <summary>
    /// Gets or initializes the partition key for partitioned endpoints.
    /// </summary>
    public string? PartitionKey { get; init; }

    /// <summary>
    /// Gets or initializes the scheduled enqueue time for delayed delivery.
    /// </summary>
    public DateTimeOffset? ScheduledEnqueueTime { get; init; }

    /// <summary>
    /// Gets or initializes the time-to-live for message expiration.
    /// </summary>
    public TimeSpan? TimeToLive { get; init; }

    /// <summary>
    /// Gets or initializes the optional adapter-specific properties.
    /// </summary>
    public IReadOnlyDictionary<string, string> AdditionalProperties { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Creates an endpoint address with the specified name and address.
    /// </summary>
    /// <param name="name">The logical name.</param>
    /// <param name="address">The physical address.</param>
    /// <returns>A new endpoint address instance.</returns>
    public static IEndpointAddress Create(string name, string address)
    {
        return new EndpointAddress
        {
            Name = name,
            Address = address
        };
    }

    /// <summary>
    /// Creates an endpoint address with the specified name, address, and typed metadata.
    /// </summary>
    /// <param name="name">The logical name.</param>
    /// <param name="address">The physical address.</param>
    /// <param name="sessionId">Optional session identifier.</param>
    /// <param name="partitionKey">Optional partition key.</param>
    /// <param name="scheduledEnqueueTime">Optional scheduled delivery time.</param>
    /// <param name="timeToLive">Optional message expiration time.</param>
    /// <param name="additionalProperties">Optional adapter-specific properties.</param>
    /// <returns>A new endpoint address instance.</returns>
    public static IEndpointAddress Create(
        string name,
        string address,
        string? sessionId = null,
        string? partitionKey = null,
        DateTimeOffset? scheduledEnqueueTime = null,
        TimeSpan? timeToLive = null,
        IDictionary<string, string>? additionalProperties = null)
    {
        return new EndpointAddress
        {
            Name = name,
            Address = address,
            SessionId = sessionId,
            PartitionKey = partitionKey,
            ScheduledEnqueueTime = scheduledEnqueueTime,
            TimeToLive = timeToLive,
            AdditionalProperties = additionalProperties != null
                ? new Dictionary<string, string>(additionalProperties)
                : []
        };
    }
}
