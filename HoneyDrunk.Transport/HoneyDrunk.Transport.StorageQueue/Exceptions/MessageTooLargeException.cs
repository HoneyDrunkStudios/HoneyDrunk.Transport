namespace HoneyDrunk.Transport.StorageQueue.Exceptions;

/// <summary>
/// Exception thrown when a message exceeds Azure Storage Queue size limits.
/// </summary>
/// <remarks>
/// Azure Storage Queue messages are limited to approximately 64KB (base64-encoded).
/// Consider moving large payloads to Blob Storage and sending a pointer/reference.
/// </remarks>
public sealed class MessageTooLargeException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MessageTooLargeException"/> class.
    /// </summary>
    public MessageTooLargeException()
        : base("Message payload exceeds Azure Storage Queue size limit (~64KB)")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageTooLargeException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public MessageTooLargeException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageTooLargeException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public MessageTooLargeException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageTooLargeException"/> class.
    /// </summary>
    /// <param name="messageId">The message identifier.</param>
    /// <param name="actualSize">The actual message size in bytes.</param>
    /// <param name="maxSize">The maximum allowed size in bytes.</param>
    public MessageTooLargeException(string messageId, int actualSize, int maxSize)
        : base($"Message {messageId} size ({actualSize} bytes) exceeds maximum ({maxSize} bytes). " +
               "Consider using Blob Storage for large payloads and sending a reference instead.")
    {
        MessageId = messageId;
        ActualSize = actualSize;
        MaxSize = maxSize;
    }

    /// <summary>
    /// Gets the message identifier that exceeded size limits.
    /// </summary>
    public string? MessageId { get; }

    /// <summary>
    /// Gets the actual message size in bytes.
    /// </summary>
    public int ActualSize { get; }

    /// <summary>
    /// Gets the maximum allowed message size in bytes.
    /// </summary>
    public int MaxSize { get; }
}
