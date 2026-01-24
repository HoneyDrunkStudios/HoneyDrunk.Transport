namespace HoneyDrunk.Transport.Exceptions;

/// <summary>
/// Exception thrown when an envelope fails validation before being sent to the broker.
/// This exception indicates that the message would have failed at the broker level
/// due to invalid structure, oversized headers, or missing required fields.
/// </summary>
/// <remarks>
/// <para>
/// This is a fail-fast exception that prevents invalid messages from reaching the broker,
/// providing clear error messages at the point of misuse rather than cryptic broker errors.
/// </para>
/// <para>
/// Common causes include:
/// <list type="bullet">
///   <item><description>Headers/baggage exceeding size limits (48KB default)</description></item>
///   <item><description>Missing required identifiers (CorrelationId, etc.) in an initialized GridContext</description></item>
/// </list>
/// </para>
/// <para>
/// Note: Attempting to publish from an uninitialized GridContext results in
/// <see cref="InvalidOperationException"/>, not this exception.
/// </para>
/// </remarks>
public sealed class EnvelopeValidationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EnvelopeValidationException"/> class.
    /// </summary>
    public EnvelopeValidationException()
        : base("Envelope validation failed")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EnvelopeValidationException"/> class
    /// with a specified error message.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public EnvelopeValidationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EnvelopeValidationException"/> class
    /// with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public EnvelopeValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
