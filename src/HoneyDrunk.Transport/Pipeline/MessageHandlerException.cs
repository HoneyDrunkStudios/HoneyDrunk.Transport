using HoneyDrunk.Transport.Abstractions;

namespace HoneyDrunk.Transport.Pipeline;

/// <summary>
/// Exception thrown by message handlers to control processing outcome.
/// </summary>
public sealed class MessageHandlerException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MessageHandlerException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="result">The desired processing result.</param>
    public MessageHandlerException(string message, MessageProcessingResult result)
        : base(message)
    {
        Result = result;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageHandlerException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="result">The desired processing result.</param>
    /// <param name="innerException">The inner exception.</param>
    public MessageHandlerException(string message, MessageProcessingResult result, Exception innerException)
        : base(message, innerException)
    {
        Result = result;
    }

    /// <summary>
    /// Gets the desired message processing result.
    /// </summary>
    public MessageProcessingResult Result { get; }
}
