using HoneyDrunk.Transport.Exceptions;

namespace HoneyDrunk.Transport.Tests.Core.Exceptions;

/// <summary>
/// Tests for <see cref="EnvelopeValidationException"/> constructors.
/// </summary>
public sealed class EnvelopeValidationExceptionConstructorTests
{
    /// <summary>
    /// Default constructor uses the standard validation message.
    /// </summary>
    [Fact]
    public void Constructor_WhenDefault_UsesStandardMessage()
    {
        var exception = new EnvelopeValidationException();

        Assert.Equal("Envelope validation failed", exception.Message);
    }

    /// <summary>
    /// Message constructor preserves the provided message.
    /// </summary>
    [Fact]
    public void Constructor_WhenMessageProvided_PreservesMessage()
    {
        var exception = new EnvelopeValidationException("bad envelope");

        Assert.Equal("bad envelope", exception.Message);
    }

    /// <summary>
    /// Message and inner exception constructor preserves both values.
    /// </summary>
    [Fact]
    public void Constructor_WhenInnerExceptionProvided_PreservesMessageAndInnerException()
    {
        var inner = new InvalidOperationException("inner");

        var exception = new EnvelopeValidationException("bad envelope", inner);

        Assert.Equal("bad envelope", exception.Message);
        Assert.Same(inner, exception.InnerException);
    }
}
