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

    /// <summary>
    /// Ensures the middleware doesn't modify the envelope.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task InvokeAsync_DoesNotModifyEnvelope()
    {
        var envelope = new TransportEnvelope
        {
            MessageId = "test-id",
            MessageType = "TestType",
            Headers = new Dictionary<string, string> { ["key"] = "value" },
            Payload = new byte[] { 1, 2, 3 },
            Timestamp = DateTimeOffset.UtcNow
        };

        var context = new MessageContext
        {
            Envelope = envelope,
            Transaction = NoOpTransportTransaction.Instance,
            DeliveryCount = 1,
            Properties = []
        };

        ITransportEnvelope? receivedEnvelope = null;
        var middleware = new HoneyDrunk.Transport.DependencyInjection.NoOpMiddleware();

        await middleware.InvokeAsync(
            envelope,
            context,
            () =>
            {
                receivedEnvelope = envelope;
                return Task.CompletedTask;
            });

        Assert.Same(envelope, receivedEnvelope);
        Assert.Equal("test-id", envelope.MessageId);
        Assert.Equal("TestType", envelope.MessageType);
    }

    /// <summary>
    /// Ensures the middleware doesn't modify the context.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task InvokeAsync_DoesNotModifyContext()
    {
        var envelope = new TransportEnvelope
        {
            MessageId = "id",
            MessageType = "Type",
            Headers = new Dictionary<string, string>(),
            Payload = Array.Empty<byte>(),
            Timestamp = DateTimeOffset.UtcNow
        };

        var context = new MessageContext
        {
            Envelope = envelope,
            Transaction = NoOpTransportTransaction.Instance,
            DeliveryCount = 5,
            Properties = new Dictionary<string, object> { ["test"] = "value" }
        };

        MessageContext? receivedContext = null;
        var middleware = new HoneyDrunk.Transport.DependencyInjection.NoOpMiddleware();

        await middleware.InvokeAsync(
            envelope,
            context,
            () =>
            {
                receivedContext = context;
                return Task.CompletedTask;
            });

        Assert.Same(context, receivedContext);
        Assert.Equal(5, context.DeliveryCount);
        Assert.Equal("value", context.Properties["test"]);
    }

    /// <summary>
    /// Ensures the middleware respects cancellation token.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task InvokeAsync_RespectsCancellationToken()
    {
        var envelope = new TransportEnvelope
        {
            MessageId = "id",
            MessageType = "Type",
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

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var middleware = new HoneyDrunk.Transport.DependencyInjection.NoOpMiddleware();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            middleware.InvokeAsync(
                envelope,
                context,
                () => Task.Delay(100, cts.Token),
                cts.Token));
    }

    /// <summary>
    /// Ensures the middleware propagates exceptions from next.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task InvokeAsync_PropagatesExceptionsFromNext()
    {
        var envelope = new TransportEnvelope
        {
            MessageId = "id",
            MessageType = "Type",
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

        var expectedException = new InvalidOperationException("Next failed");
        var middleware = new HoneyDrunk.Transport.DependencyInjection.NoOpMiddleware();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            middleware.InvokeAsync(
                envelope,
                context,
                () => throw expectedException));

        Assert.Same(expectedException, exception);
    }

    /// <summary>
    /// Ensures the middleware works with default cancellation token.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task InvokeAsync_WorksWithDefaultCancellationToken()
    {
        var envelope = new TransportEnvelope
        {
            MessageId = "id",
            MessageType = "Type",
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
            },
            default);

        Assert.True(nextCalled);
    }

    /// <summary>
    /// Ensures the middleware can be called multiple times.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task InvokeAsync_CanBeCalledMultipleTimes()
    {
        var envelope = new TransportEnvelope
        {
            MessageId = "id",
            MessageType = "Type",
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

        int callCount = 0;
        var middleware = new HoneyDrunk.Transport.DependencyInjection.NoOpMiddleware();

        await middleware.InvokeAsync(
            envelope,
            context,
            () =>
            {
                callCount++;
                return Task.CompletedTask;
            });

        await middleware.InvokeAsync(
            envelope,
            context,
            () =>
            {
                callCount++;
                return Task.CompletedTask;
            });

        await middleware.InvokeAsync(
            envelope,
            context,
            () =>
            {
                callCount++;
                return Task.CompletedTask;
            });

        Assert.Equal(3, callCount);
    }

    /// <summary>
    /// Ensures the middleware is stateless.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task InvokeAsync_IsStateless()
    {
        var envelope1 = new TransportEnvelope
        {
            MessageId = "id1",
            MessageType = "Type",
            Headers = new Dictionary<string, string>(),
            Payload = Array.Empty<byte>(),
            Timestamp = DateTimeOffset.UtcNow
        };

        var envelope2 = new TransportEnvelope
        {
            MessageId = "id2",
            MessageType = "Type",
            Headers = new Dictionary<string, string>(),
            Payload = Array.Empty<byte>(),
            Timestamp = DateTimeOffset.UtcNow
        };

        var context1 = new MessageContext
        {
            Envelope = envelope1,
            Transaction = NoOpTransportTransaction.Instance,
            DeliveryCount = 1,
            Properties = []
        };

        var context2 = new MessageContext
        {
            Envelope = envelope2,
            Transaction = NoOpTransportTransaction.Instance,
            DeliveryCount = 2,
            Properties = []
        };

        var middleware = new HoneyDrunk.Transport.DependencyInjection.NoOpMiddleware();

        string? received1 = null;
        string? received2 = null;

        await middleware.InvokeAsync(
            envelope1,
            context1,
            () =>
            {
                received1 = envelope1.MessageId;
                return Task.CompletedTask;
            });

        await middleware.InvokeAsync(
            envelope2,
            context2,
            () =>
            {
                received2 = envelope2.MessageId;
                return Task.CompletedTask;
            });

        Assert.Equal("id1", received1);
        Assert.Equal("id2", received2);
    }
}
