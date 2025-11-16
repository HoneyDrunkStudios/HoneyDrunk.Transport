using HoneyDrunk.Transport.Exceptions;
using HoneyDrunk.Transport.Pipeline;

namespace HoneyDrunk.Transport.Tests.Core.Exceptions;

/// <summary>
/// Tests for exception types.
/// </summary>
public sealed class ExceptionTests
{
    /// <summary>
    /// Verifies MessageTooLargeException has expected properties.
    /// </summary>
    [Fact]
    public void MessageTooLargeException_WithMessage_ContainsProvidedMessage()
    {
        var ex = new MessageTooLargeException("test message");
        Assert.Equal("test message", ex.Message);
    }

    /// <summary>
    /// Verifies MessageTooLargeException with inner exception.
    /// </summary>
    [Fact]
    public void MessageTooLargeException_WithInnerException_ContainsInnerException()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new MessageTooLargeException("outer", inner);
        Assert.Equal("outer", ex.Message);
        Assert.Same(inner, ex.InnerException);
    }

    /// <summary>
    /// Verifies MessageHandlerException has result property.
    /// </summary>
    [Fact]
    public void MessageHandlerException_WithResult_ContainsExpectedResult()
    {
        var ex = new MessageHandlerException("test", HoneyDrunk.Transport.Abstractions.MessageProcessingResult.DeadLetter);
        Assert.Equal("test", ex.Message);
        Assert.Equal(HoneyDrunk.Transport.Abstractions.MessageProcessingResult.DeadLetter, ex.Result);
    }

    /// <summary>
    /// Verifies MessageHandlerException with inner exception.
    /// </summary>
    [Fact]
    public void MessageHandlerException_WithInnerException_ContainsInnerException()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new MessageHandlerException("outer", HoneyDrunk.Transport.Abstractions.MessageProcessingResult.Retry, inner);
        Assert.Equal("outer", ex.Message);
        Assert.Equal(HoneyDrunk.Transport.Abstractions.MessageProcessingResult.Retry, ex.Result);
        Assert.Same(inner, ex.InnerException);
    }
}
