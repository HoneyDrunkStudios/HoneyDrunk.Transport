using HoneyDrunk.Transport.Exceptions;
using HoneyDrunk.Transport.Pipeline;

namespace HoneyDrunk.Transport.Tests.Core.Exceptions;

/// <summary>
/// Tests for exception types.
/// </summary>
public sealed class ExceptionTests
{
    /// <summary>
    /// Verifies MessageTooLargeException default constructor.
    /// </summary>
    [Fact]
    public void MessageTooLargeException_DefaultConstructor_HasDefaultMessage()
    {
        // Act
        var ex = new MessageTooLargeException();

        // Assert
        Assert.Equal("Message payload exceeds transport size limit", ex.Message);
        Assert.Null(ex.MessageId);
        Assert.Equal(0, ex.ActualSize);
        Assert.Equal(0, ex.MaxSize);
        Assert.Null(ex.TransportName);
    }

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
    /// Verifies parametric constructor sets all properties correctly.
    /// </summary>
    [Fact]
    public void MessageTooLargeException_ParametricConstructor_SetsAllProperties()
    {
        // Act
        var ex = new MessageTooLargeException("msg-123", 500000, 256000, "Azure Service Bus");

        // Assert
        Assert.Equal("msg-123", ex.MessageId);
        Assert.Equal(500000, ex.ActualSize);
        Assert.Equal(256000, ex.MaxSize);
        Assert.Equal("Azure Service Bus", ex.TransportName);
        Assert.Contains("msg-123", ex.Message);
        Assert.Contains("500,000", ex.Message);
        Assert.Contains("256,000", ex.Message);
        Assert.Contains("Azure Service Bus", ex.Message);
    }

    /// <summary>
    /// Verifies parametric constructor without transport name.
    /// </summary>
    [Fact]
    public void MessageTooLargeException_ParametricConstructorWithoutTransportName_UsesGenericTerm()
    {
        // Act
        var ex = new MessageTooLargeException("msg-456", 100000, 64000);

        // Assert
        Assert.Equal("msg-456", ex.MessageId);
        Assert.Equal(100000, ex.ActualSize);
        Assert.Equal(64000, ex.MaxSize);
        Assert.Null(ex.TransportName);
        Assert.Contains("transport", ex.Message.ToLowerInvariant());
        Assert.DoesNotContain("Azure", ex.Message);
    }

    /// <summary>
    /// Verifies parametric constructor with null transport name.
    /// </summary>
    [Fact]
    public void MessageTooLargeException_ParametricConstructorWithNullTransportName_UsesGenericTerm()
    {
        // Act
        var ex = new MessageTooLargeException("msg-789", 200000, 128000, null);

        // Assert
        Assert.Null(ex.TransportName);
        Assert.Contains("transport", ex.Message.ToLowerInvariant());
    }

    /// <summary>
    /// Verifies parametric constructor with empty transport name.
    /// </summary>
    [Fact]
    public void MessageTooLargeException_ParametricConstructorWithEmptyTransportName_UsesGenericTerm()
    {
        // Act
        var ex = new MessageTooLargeException("msg-empty", 300000, 256000, string.Empty);

        // Assert
        Assert.Equal(string.Empty, ex.TransportName);
        Assert.Contains("transport", ex.Message.ToLowerInvariant());
    }

    /// <summary>
    /// Verifies message formatting includes claim check pattern recommendation.
    /// </summary>
    [Fact]
    public void MessageTooLargeException_MessageFormat_IncludesClaimCheckRecommendation()
    {
        // Act
        var ex = new MessageTooLargeException("msg-claim", 1000000, 256000, "Azure Service Bus");

        // Assert
        Assert.Contains("claim check pattern", ex.Message.ToLowerInvariant());
        Assert.Contains("Blob Storage", ex.Message);
    }

    /// <summary>
    /// Verifies parametric constructor with zero sizes.
    /// </summary>
    [Fact]
    public void MessageTooLargeException_WithZeroSizes_FormatsCorrectly()
    {
        // Act
        var ex = new MessageTooLargeException("msg-zero", 0, 0);

        // Assert
        Assert.Equal(0, ex.ActualSize);
        Assert.Equal(0, ex.MaxSize);
        Assert.Contains("0 bytes", ex.Message);
    }

    /// <summary>
    /// Verifies parametric constructor with large sizes.
    /// </summary>
    [Fact]
    public void MessageTooLargeException_WithLargeSizes_FormatsWithCommas()
    {
        // Act
        var ex = new MessageTooLargeException("msg-large", 5000000, 1000000);

        // Assert
        Assert.Equal(5000000, ex.ActualSize);
        Assert.Equal(1000000, ex.MaxSize);
        Assert.Contains("5,000,000", ex.Message);
        Assert.Contains("1,000,000", ex.Message);
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
