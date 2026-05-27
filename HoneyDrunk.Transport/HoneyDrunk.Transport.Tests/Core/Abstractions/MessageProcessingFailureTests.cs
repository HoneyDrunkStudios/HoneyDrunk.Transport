using HoneyDrunk.Transport.Abstractions;

namespace HoneyDrunk.Transport.Tests.Core.Abstractions;

/// <summary>
/// Tests for <see cref="MessageProcessingFailure"/> factory behavior.
/// </summary>
public sealed class MessageProcessingFailureTests
{
    /// <summary>
    /// Gets transient exception identifiers for classification tests.
    /// Test rows are <see cref="string"/>s — not Exceptions — so Test Explorer
    /// can enumerate the rows individually (Sonar S6562 — TheoryData type
    /// arguments must be serializable). The test method constructs the
    /// exception from the identifier via <see cref="BuildTransientException(string)"/>.
    /// </summary>
    public static TheoryData<string> TransientExceptionKinds => new() { "timeout", "service-unavailable" };

    /// <summary>
    /// Gets permanent exception identifiers for classification tests.
    /// See <see cref="TransientExceptionKinds"/> for the rationale.
    /// </summary>
    public static TheoryData<string> PermanentExceptionKinds => new() { "argument", "argument-null", "invalid-operation" };

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
    /// <param name="kind">The transient exception kind to construct and classify.</param>
    [Theory]
    [MemberData(nameof(TransientExceptionKinds))]
    public void FromException_WhenExceptionIsTransient_ReturnsRetry(string kind)
    {
        var exception = BuildTransientException(kind);

        var result = MessageProcessingFailure.FromException(exception, deliveryCount: 1);

        Assert.Equal(MessageProcessingResult.Retry, result.Result);
        Assert.Equal("TransientError", result.Category);
        Assert.Same(exception, result.Exception);
    }

    /// <summary>
    /// Permanent exceptions are dead-lettered.
    /// </summary>
    /// <param name="kind">The permanent exception kind to construct and classify.</param>
    [Theory]
    [MemberData(nameof(PermanentExceptionKinds))]
    public void FromException_WhenExceptionIsPermanent_ReturnsPermanentDeadLetter(string kind)
    {
        var exception = BuildPermanentException(kind);

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

    private static Exception BuildTransientException(string kind) => kind switch
    {
        "timeout" => new TimeoutException("timeout"),
        "service-unavailable" => new HttpRequestException("service unavailable", null, System.Net.HttpStatusCode.ServiceUnavailable),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown transient exception kind."),
    };

    private static Exception BuildPermanentException(string kind) => kind switch
    {
        "argument" => new ArgumentException("argument"),

        // paramName must match a method parameter name (CA2208) — `kind` is the
        // only one available here; the actual paramName isn't asserted by tests.
        "argument-null" => new ArgumentNullException(nameof(kind)),
        "invalid-operation" => new InvalidOperationException("invalid"),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown permanent exception kind."),
    };

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
