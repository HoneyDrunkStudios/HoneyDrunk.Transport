namespace HoneyDrunk.Transport.SandboxNode;

/// <summary>
/// Sample message for testing publish/consume flow.
/// </summary>
public sealed record SampleMessage
{
    /// <summary>
    /// Gets or sets the message content.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Gets or sets the timestamp when the message was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets an optional correlation tag for verification.
    /// </summary>
    public string? Tag { get; init; }
}
