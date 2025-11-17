using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.DependencyInjection;
using HoneyDrunk.Transport.Primitives;

namespace HoneyDrunk.Transport.Tests.Core.Middleware;

/// <summary>
/// Tests for delegate adapter middleware ensuring delegate invocation and pipeline continuation.
/// </summary>
public sealed class DelegateMessageMiddlewareTests
{
    /// <summary>
    /// Verifies delegate executes and next is invoked.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task InvokeAsync_ExecutesDelegateAndNext()
    {
        var envelope = new TransportEnvelope
        {
            MessageId = "id",
            MessageType = typeof(DelegateMessageMiddlewareTests).FullName!,
            Headers = new Dictionary<string, string>(),
            Payload = Array.Empty<byte>(),
            Timestamp = DateTimeOffset.UtcNow
        };
        var context = new MessageContext
        {
            Envelope = envelope,
            Transaction = NoOpTransportTransaction.Instance,
            DeliveryCount = 1,
            Properties = []
        };

        bool delegateCalled = false;
        bool nextCalled = false;

        async Task Del(ITransportEnvelope env, MessageContext ctx, Func<Task> next, CancellationToken ct)
        {
            delegateCalled = env == envelope && ctx == context;
            await next();
        }

        var middleware = new DelegateMessageMiddleware(Del);
        await middleware.InvokeAsync(
            envelope,
            context,
            () =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            });

        Assert.True(delegateCalled);
        Assert.True(nextCalled);
    }
}
