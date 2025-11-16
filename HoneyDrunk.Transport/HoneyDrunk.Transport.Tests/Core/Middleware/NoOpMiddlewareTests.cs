using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.Primitives;

namespace HoneyDrunk.Transport.Tests.Core.Middleware;

/// <summary>
/// Tests for <see cref="HoneyDrunk.Transport.DependencyInjection.NoOpMiddleware"/> behavior.
/// </summary>
public sealed class NoOpMiddlewareTests
{
    /// <summary>
    /// Ensures the middleware only invokes the next delegate.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task InvokeAsync_OnlyCallsNext()
    {
        var envelope = new TransportEnvelope
        {
            MessageId = "id",
            MessageType = typeof(NoOpMiddlewareTests).FullName!,
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

        bool nextCalled = false;
        var middleware = new HoneyDrunk.Transport.DependencyInjection.NoOpMiddleware();
        await middleware.InvokeAsync(
            envelope,
            context,
            () =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            });
        Assert.True(nextCalled);
    }
}
