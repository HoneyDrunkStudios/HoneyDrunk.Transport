using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.Pipeline;

namespace HoneyDrunk.Transport.Tests.Support;

/// <summary>
/// Test pipeline that returns a fixed <see cref="MessageProcessingResult"/> (or throws
/// a configured exception) and records every invocation so tests can assert how many
/// messages flowed through.
/// </summary>
internal sealed class StubMessagePipeline : IMessagePipeline
{
    private readonly Func<ITransportEnvelope, MessageContext, CancellationToken, Task<MessageProcessingResult>> _handler;
    private int _processedCount;

    /// <summary>Initializes a new instance of the <see cref="StubMessagePipeline"/> class returning a fixed result.</summary>
    /// <param name="result">The result every <c>ProcessAsync</c> call should return.</param>
    public StubMessagePipeline(MessageProcessingResult result)
    {
        _handler = (_, _, _) => Task.FromResult(result);
    }

    /// <summary>Initializes a new instance of the <see cref="StubMessagePipeline"/> class that throws on each call.</summary>
    /// <param name="throwOnProcess">The exception to throw.</param>
    public StubMessagePipeline(Exception throwOnProcess)
    {
        _handler = (_, _, _) => throw throwOnProcess;
    }

    /// <summary>Initializes a new instance of the <see cref="StubMessagePipeline"/> class with a custom handler.</summary>
    /// <param name="handler">The handler invoked on each <c>ProcessAsync</c> call.</param>
    public StubMessagePipeline(Func<ITransportEnvelope, MessageContext, CancellationToken, Task<MessageProcessingResult>> handler)
    {
        _handler = handler;
    }

    /// <summary>Gets the number of times <c>ProcessAsync</c> was invoked.</summary>
    public int ProcessedCount => _processedCount;

    /// <inheritdoc />
    public Task<MessageProcessingResult> ProcessAsync(
        ITransportEnvelope envelope,
        MessageContext context,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _processedCount);
        return _handler(envelope, context, cancellationToken);
    }
}
