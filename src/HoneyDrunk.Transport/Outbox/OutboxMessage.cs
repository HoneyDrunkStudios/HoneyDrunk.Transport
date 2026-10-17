using HoneyDrunk.Transport.Abstractions;

namespace HoneyDrunk.Transport.Outbox;

/// <summary>
/// Default implementation of outbox message.
/// </summary>
public sealed class OutboxMessage : IOutboxMessage
{
    /// <inheritdoc/>
    public required string Id { get; init; }

    /// <inheritdoc/>
    public required IEndpointAddress Destination { get; init; }

    /// <inheritdoc/>
    public required ITransportEnvelope Envelope { get; init; }

    /// <inheritdoc/>
    public OutboxMessageState State { get; init; }

    /// <inheritdoc/>
    public int Attempts { get; init; }

    /// <inheritdoc/>
    public DateTimeOffset CreatedAt { get; init; }

    /// <inheritdoc/>
    public DateTimeOffset? ProcessedAt { get; init; }

    /// <inheritdoc/>
    public DateTimeOffset? ScheduledAt { get; init; }

    /// <inheritdoc/>
    public string? ErrorMessage { get; init; }
}
