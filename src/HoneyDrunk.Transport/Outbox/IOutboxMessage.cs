using HoneyDrunk.Transport.Abstractions;

namespace HoneyDrunk.Transport.Outbox;

/// <summary>
/// Represents an outbox message pending dispatch.
/// </summary>
public interface IOutboxMessage
{
    /// <summary>
    /// Gets the unique identifier for the outbox record.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the destination endpoint for the message.
    /// </summary>
    IEndpointAddress Destination { get; }

    /// <summary>
    /// Gets the envelope to be sent.
    /// </summary>
    ITransportEnvelope Envelope { get; }

    /// <summary>
    /// Gets the current state of the outbox message.
    /// </summary>
    OutboxMessageState State { get; }

    /// <summary>
    /// Gets the number of dispatch attempts.
    /// </summary>
    int Attempts { get; }

    /// <summary>
    /// Gets the timestamp when the message was created.
    /// </summary>
    DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Gets the timestamp when the message was last processed.
    /// </summary>
    DateTimeOffset? ProcessedAt { get; }

    /// <summary>
    /// Gets the timestamp when to attempt next dispatch (for retry scenarios).
    /// </summary>
    DateTimeOffset? ScheduledAt { get; }

    /// <summary>
    /// Gets the error message if dispatch failed.
    /// </summary>
    string? ErrorMessage { get; }
}
