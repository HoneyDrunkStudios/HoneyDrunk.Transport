namespace HoneyDrunk.Transport.Exceptions;

/// <summary>
/// Exception thrown when a message exceeds the transport's size limits.
/// </summary>
/// <remarks>
/// Different transports have different size limits:
/// <list type="bullet">
/// <item><description>Azure Storage Queue: ~64KB (base64-encoded)</description></item>
/// <item><description>Azure Service Bus: 256KB (Standard tier) or 1MB (Premium tier)</description></item>
/// <item><description>RabbitMQ: Configurable, typically up to 128MB</description></item>
/// </list>
/// For messages exceeding these limits, consider using the claim check pattern
/// by storing large payloads in Blob Storage and sending only a reference/pointer.
/// </remarks>
public sealed class MessageTooLargeException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MessageTooLargeException"/> class.
    /// </summary>
    public MessageTooLargeException()
        : base("Message payload exceeds transport size limit")
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
    /// <param name="transportName">Optional transport name for context.</param>
    public MessageTooLargeException(string messageId, long actualSize, long maxSize, string? transportName = null)
        : base(FormatMessage(messageId, actualSize, maxSize, transportName))
    {
        MessageId = messageId;
        ActualSize = actualSize;
        MaxSize = maxSize;
        TransportName = transportName;
    }

    /// <summary>
    /// Gets the message identifier that exceeded size limits.
    /// </summary>
    public string? MessageId { get; }

    /// <summary>
    /// Gets the actual message size in bytes.
    /// </summary>
    public long ActualSize { get; }

    /// <summary>
    /// Gets the maximum allowed message size in bytes.
    /// </summary>
    public long MaxSize { get; }

    /// <summary>
    /// Gets the transport name where the size limit was exceeded.
    /// </summary>
    public string? TransportName { get; }

    private static string FormatMessage(string messageId, long actualSize, long maxSize, string? transportName)
    {
        var transport = string.IsNullOrEmpty(transportName) ? "transport" : transportName;
        return $"Message {messageId} size ({actualSize:N0} bytes) exceeds {transport} maximum ({maxSize:N0} bytes). " +
               "Consider using claim check pattern with Blob Storage for large payloads.";
    }
}
