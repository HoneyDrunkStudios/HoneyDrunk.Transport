namespace HoneyDrunk.Transport.Abstractions;

/// <summary>
/// Represents a transport-agnostic message envelope that wraps all messages moving through the system.
/// Aligned with Kernel IGridContext for Grid-aware context propagation.
/// </summary>
public interface ITransportEnvelope
{
    /// <summary>
    /// Gets the unique identifier for this message instance.
    /// </summary>
    string MessageId { get; }

    /// <summary>
    /// Gets the identifier that groups related messages together (e.g., all messages in a workflow).
    /// Maps to IGridContext.CorrelationId for distributed tracing across Node boundaries.
    /// </summary>
    string? CorrelationId { get; }

    /// <summary>
    /// Gets the identifier of the message that caused this message to be created.
    /// Maps to IGridContext.CausationId for cause-effect chain tracking.
    /// </summary>
    string? CausationId { get; }

    /// <summary>
    /// Gets the Node identifier where the message originated.
    /// Maps to IGridContext.NodeId for Grid topology tracking.
    /// </summary>
    string? NodeId { get; }

    /// <summary>
    /// Gets the Studio/Tenant identifier for multi-tenancy support.
    /// Maps to IGridContext.StudioId for tenant isolation.
    /// </summary>
    string? StudioId { get; }

    /// <summary>
    /// Gets the environment identifier (dev, staging, prod).
    /// Maps to IGridContext.Environment for environment-aware routing.
    /// </summary>
    string? Environment { get; }

    /// <summary>
    /// Gets the UTC timestamp when the message was created.
    /// </summary>
    DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Gets the logical type or name of the message content.
    /// </summary>
    string MessageType { get; }

    /// <summary>
    /// Gets the custom headers for extensibility (routing hints, security tokens, etc.).
    /// Includes Grid baggage for cross-Node context propagation.
    /// </summary>
    IReadOnlyDictionary<string, string> Headers { get; }

    /// <summary>
    /// Gets the serialized message body.
    /// </summary>
    ReadOnlyMemory<byte> Payload { get; }

    /// <summary>
    /// Creates a new envelope with updated headers.
    /// </summary>
    /// <param name="additionalHeaders">Headers to add or update.</param>
    /// <returns>A new envelope instance with the updated headers.</returns>
    ITransportEnvelope WithHeaders(IDictionary<string, string> additionalHeaders);

    /// <summary>
    /// Creates a new envelope with updated Grid context.
    /// </summary>
    /// <param name="correlationId">The correlation identifier.</param>
    /// <param name="causationId">The causation identifier.</param>
    /// <param name="nodeId">The node identifier.</param>
    /// <param name="studioId">The studio identifier.</param>
    /// <param name="environment">The environment identifier.</param>
    /// <returns>A new envelope instance with the updated Grid context.</returns>
    ITransportEnvelope WithGridContext(
        string? correlationId = null,
        string? causationId = null,
        string? nodeId = null,
        string? studioId = null,
        string? environment = null);
}
