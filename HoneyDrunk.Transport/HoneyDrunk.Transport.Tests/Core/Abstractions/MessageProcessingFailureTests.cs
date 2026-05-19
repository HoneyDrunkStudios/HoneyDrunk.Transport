using HoneyDrunk.Transport.Abstractions;

namespace HoneyDrunk.Transport.Tests.Core.Abstractions;

/// <summary>
/// Tests for <see cref="MessageProcessingFailure"/> factory behavior.
/// </summary>
public sealed class MessageProcessingFailureTests
{
    /// <summary>
    /// Gets transient exceptions for classification tests.
    /// </summary>
    public static TheoryData<Exception> TransientExceptions => new()
    {
        new TimeoutException("timeout"),
        new HttpRequestException("service unavailable", null, System.Net.HttpStatusCode.ServiceUnavailable)
    };

    /// <summary>
    /// Gets permanent exceptions for classification tests.
    /// </summary>
    public static TheoryData<Exception> PermanentExceptions => new()
    {
        new ArgumentException("argument"),
        new ArgumentNullException("value"),
        new InvalidOperationException("invalid")
    };

    /// <summary>
    /// Success creates a success result without failure metadata.
    /// </summary>
    [Fact]
    public void Success_WhenCalled_ReturnsSuccessResult()
    {
        var result = MessageProcessingFailure.Success();

        Assert.Equal(MessageProcessingResult.Success, result.Result);
        Assert.Null(result.Reason);
        Assert.Null(result.Exception);
    }

    /// <summary>
    /// Retry preserves diagnostic metadata.
    /// </summary>
    [Fact]
    public void Retry_WithMetadata_PreservesFailureDetails()
    {
        var exception = new TimeoutException("timeout");
        var metadata = new Dictionary<string, object> { ["attempt"] = 2 };

        var result = MessageProcessingFailure.Retry("try again", exception, "Transient", metadata);

        Assert.Equal(MessageProcessingResult.Retry, result.Result);
        Assert.Equal("try again", result.Reason);
        Assert.Equal("Transient", result.Category);
        Assert.Same(exception, result.Exception);
        Assert.Same(metadata, result.Metadata);
    }

    /// <summary>
    /// Dead-letter preserves diagnostic metadata.
    /// </summary>
    [Fact]
    public void DeadLetter_WithMetadata_PreservesFailureDetails()
    {
        var exception = new InvalidOperationException("bad");
        var metadata = new Dictionary<string, object> { ["field"] = "value" };

        var result = MessageProcessingFailure.DeadLetter("do not retry", "Permanent", exception, metadata);

        Assert.Equal(MessageProcessingResult.DeadLetter, result.Result);
        Assert.Equal("do not retry", result.Reason);
        Assert.Equal("Permanent", result.Category);
        Assert.Same(exception, result.Exception);
        Assert.Same(metadata, result.Metadata);
    }

    /// <summary>
    /// Exceptions over the retry limit become dead-letter failures.
    /// </summary>
    [Fact]
    public void FromException_WhenRetryLimitExceeded_ReturnsMaxRetriesDeadLetter()
    {
        var exception = new TimeoutException("timeout");

        var result = MessageProcessingFailure.FromException(exception, deliveryCount: 6, maxRetries: 5);

        Assert.Equal(MessageProcessingResult.DeadLetter, result.Result);
        Assert.Equal("MaxRetriesExceeded", result.Category);
        Assert.Same(exception, result.Exception);
    }

    /// <summary>
    /// Transient exceptions are retried.
    /// </summary>
    /// <param name="exception">The exception to classify.</param>
    [Theory]
    [MemberData(nameof(TransientExceptions))]
    public void FromException_WhenExceptionIsTransient_ReturnsRetry(Exception exception)
    {
        var result = MessageProcessingFailure.FromException(exception, deliveryCount: 1);

        Assert.Equal(MessageProcessingResult.Retry, result.Result);
        Assert.Equal("TransientError", result.Category);
        Assert.Same(exception, result.Exception);
    }

    /// <summary>
    /// Permanent exceptions are dead-lettered.
    /// </summary>
    /// <param name="exception">The exception to classify.</param>
    [Theory]
    [MemberData(nameof(PermanentExceptions))]
    public void FromException_WhenExceptionIsPermanent_ReturnsPermanentDeadLetter(Exception exception)
    {
        var result = MessageProcessingFailure.FromException(exception, deliveryCount: 1);

        Assert.Equal(MessageProcessingResult.DeadLetter, result.Result);
        Assert.Equal("PermanentError", result.Category);
        Assert.Same(exception, result.Exception);
    }

    /// <summary>
    /// Unknown exceptions are retried cautiously.
    /// </summary>
    [Fact]
    public void FromException_WhenExceptionIsUnknown_ReturnsUnknownRetry()
    {
        var exception = new UnknownFailureException("unknown");

        var result = MessageProcessingFailure.FromException(exception, deliveryCount: 1);

        Assert.Equal(MessageProcessingResult.Retry, result.Result);
        Assert.Equal("UnknownError", result.Category);
        Assert.Same(exception, result.Exception);
    }

    /// <summary>
    /// Custom exception used to represent an unclassified failure.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="UnknownFailureException"/> class.
    /// </remarks>
    /// <param name="message">The exception message.</param>
    private sealed class UnknownFailureException(string message) : Exception(message)
    {
    }
}
