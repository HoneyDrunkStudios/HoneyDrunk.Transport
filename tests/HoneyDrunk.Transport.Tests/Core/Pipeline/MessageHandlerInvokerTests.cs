using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.Pipeline;
using HoneyDrunk.Transport.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace HoneyDrunk.Transport.Tests.Core.Pipeline;

/// <summary>
/// Tests for MessageHandlerInvoker to verify handler resolution and invocation.
/// </summary>
public sealed class MessageHandlerInvokerTests
{
    /// <summary>
    /// Verifies that TryInvokeHandler returns false when no handler is registered.
    /// </summary>
    [Fact]
    public void TryInvokeHandler_WithNoHandlerRegistered_ReturnsFalse()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var provider = services.BuildServiceProvider();

        var invoker = new MessageHandlerInvoker(provider);
        var message = new SampleMessage { Value = "test" };
        var context = new MessageContext
        {
            Envelope = TestData.CreateEnvelope(message),
            Transaction = NoOpTransportTransaction.Instance,
            DeliveryCount = 1
        };

        // Act
        var found = invoker.TryInvokeHandler(message, typeof(SampleMessage), context, CancellationToken.None, out var task);

        // Assert
        Assert.False(found);
        Assert.Same(Task.CompletedTask, task);
    }

    /// <summary>
    /// Verifies that TryInvokeHandler invokes the registered handler successfully.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task TryInvokeHandler_WithRegisteredHandler_InvokesHandler()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var handlerCalled = false;
        services.AddSingleton<IMessageHandler<SampleMessage>>(
            new HoneyDrunk.Transport.DependencyInjection.DelegateMessageHandler<SampleMessage>((msg, ctx, ct) =>
            {
                handlerCalled = true;
                Assert.Equal("test", msg.Value);
                return Task.CompletedTask;
            }));

        var provider = services.BuildServiceProvider();

        var invoker = new MessageHandlerInvoker(provider);
        var message = new SampleMessage { Value = "test" };
        var context = new MessageContext
        {
            Envelope = TestData.CreateEnvelope(message),
            Transaction = NoOpTransportTransaction.Instance,
            DeliveryCount = 1
        };

        // Act
        var found = invoker.TryInvokeHandler(message, typeof(SampleMessage), context, CancellationToken.None, out var task);
        Assert.True(found);
        await task;

        // Assert
        Assert.True(handlerCalled);
    }

    /// <summary>
    /// Verifies that TryInvokeHandler caches the compiled delegate for performance.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task TryInvokeHandler_CalledMultipleTimes_UsesCachedDelegate()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var callCount = 0;
        services.AddSingleton<IMessageHandler<SampleMessage>>(
            new HoneyDrunk.Transport.DependencyInjection.DelegateMessageHandler<SampleMessage>((msg, ctx, ct) =>
            {
                callCount++;
                return Task.CompletedTask;
            }));

        var provider = services.BuildServiceProvider();
        var invoker = new MessageHandlerInvoker(provider);

        var message = new SampleMessage { Value = "test" };
        var context = new MessageContext
        {
            Envelope = TestData.CreateEnvelope(message),
            Transaction = NoOpTransportTransaction.Instance,
            DeliveryCount = 1
        };

        // Act - invoke multiple times
        Assert.True(invoker.TryInvokeHandler(message, typeof(SampleMessage), context, CancellationToken.None, out var task1));
        await task1;

        Assert.True(invoker.TryInvokeHandler(message, typeof(SampleMessage), context, CancellationToken.None, out var task2));
        await task2;

        Assert.True(invoker.TryInvokeHandler(message, typeof(SampleMessage), context, CancellationToken.None, out var task3));
        await task3;

        // Assert - handler was called 3 times (verifies caching didn't break invocation)
        Assert.Equal(3, callCount);
    }

    /// <summary>
    /// Verifies that TryInvokeHandler throws ArgumentNullException for null message.
    /// </summary>
    [Fact]
    public void TryInvokeHandler_WithNullMessage_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();
        var invoker = new MessageHandlerInvoker(provider);
        var context = new MessageContext
        {
            Envelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" }),
            Transaction = NoOpTransportTransaction.Instance,
            DeliveryCount = 1
        };

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            invoker.TryInvokeHandler(null!, typeof(SampleMessage), context, CancellationToken.None, out _));
    }

    /// <summary>
    /// Verifies that TryInvokeHandler throws ArgumentNullException for null message type.
    /// </summary>
    [Fact]
    public void TryInvokeHandler_WithNullMessageType_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();
        var invoker = new MessageHandlerInvoker(provider);
        var message = new SampleMessage { Value = "test" };
        var context = new MessageContext
        {
            Envelope = TestData.CreateEnvelope(message),
            Transaction = NoOpTransportTransaction.Instance,
            DeliveryCount = 1
        };

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            invoker.TryInvokeHandler(message, null!, context, CancellationToken.None, out _));
    }

    /// <summary>
    /// Verifies that TryInvokeHandler propagates handler exceptions.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task TryInvokeHandler_WhenHandlerThrows_PropagatesException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var expectedException = new InvalidOperationException("Handler failed");
        services.AddSingleton<IMessageHandler<SampleMessage>>(
            new HoneyDrunk.Transport.DependencyInjection.DelegateMessageHandler<SampleMessage>((msg, ctx, ct) =>
            {
                throw expectedException;
            }));

        var provider = services.BuildServiceProvider();
        var invoker = new MessageHandlerInvoker(provider);
        var message = new SampleMessage { Value = "test" };
        var context = new MessageContext
        {
            Envelope = TestData.CreateEnvelope(message),
            Transaction = NoOpTransportTransaction.Instance,
            DeliveryCount = 1
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            Assert.True(invoker.TryInvokeHandler(message, typeof(SampleMessage), context, CancellationToken.None, out var task));
            await task;
        });

        Assert.Same(expectedException, exception);
    }

    /// <summary>
    /// Verifies that TryInvokeHandler respects cancellation tokens.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task TryInvokeHandler_WithCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddSingleton<IMessageHandler<SampleMessage>>(
            new HoneyDrunk.Transport.DependencyInjection.DelegateMessageHandler<SampleMessage>(async (msg, ctx, ct) =>
            {
                await Task.Delay(100, ct);
            }));

        var provider = services.BuildServiceProvider();
        var invoker = new MessageHandlerInvoker(provider);
        var message = new SampleMessage { Value = "test" };
        var context = new MessageContext
        {
            Envelope = TestData.CreateEnvelope(message),
            Transaction = NoOpTransportTransaction.Instance,
            DeliveryCount = 1
        };

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        Assert.True(invoker.TryInvokeHandler(message, typeof(SampleMessage), context, cts.Token, out var task));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    /// <summary>
    /// Verifies that TryInvokeHandler works with different message types.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task TryInvokeHandler_WithDifferentMessageTypes_InvokesCorrectHandlers()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var sampleHandlerCalled = false;
        var otherHandlerCalled = false;

        services.AddSingleton<IMessageHandler<SampleMessage>>(
            new HoneyDrunk.Transport.DependencyInjection.DelegateMessageHandler<SampleMessage>((msg, ctx, ct) =>
            {
                sampleHandlerCalled = true;
                return Task.CompletedTask;
            }));

        services.AddSingleton<IMessageHandler<OtherMessage>>(
            new HoneyDrunk.Transport.DependencyInjection.DelegateMessageHandler<OtherMessage>((msg, ctx, ct) =>
            {
                otherHandlerCalled = true;
                return Task.CompletedTask;
            }));

        var provider = services.BuildServiceProvider();
        var invoker = new MessageHandlerInvoker(provider);

        var sampleMessage = new SampleMessage { Value = "test" };
        var otherMessage = new OtherMessage { Data = "other" };
        var context = new MessageContext
        {
            Envelope = TestData.CreateEnvelope(sampleMessage),
            Transaction = NoOpTransportTransaction.Instance,
            DeliveryCount = 1
        };

        // Act - invoke SampleMessage handler
        Assert.True(invoker.TryInvokeHandler(sampleMessage, typeof(SampleMessage), context, CancellationToken.None, out var task1));
        await task1;

        // Act - invoke OtherMessage handler
        Assert.True(invoker.TryInvokeHandler(otherMessage, typeof(OtherMessage), context, CancellationToken.None, out var task2));
        await task2;

        // Assert
        Assert.True(sampleHandlerCalled);
        Assert.True(otherHandlerCalled);
    }

    /// <summary>
    /// Verifies that TryInvokeHandler passes context correctly to handler.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task TryInvokeHandler_PassesContextCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        MessageContext? receivedContext = null;
        services.AddSingleton<IMessageHandler<SampleMessage>>(
            new HoneyDrunk.Transport.DependencyInjection.DelegateMessageHandler<SampleMessage>((msg, ctx, ct) =>
            {
                receivedContext = ctx;
                return Task.CompletedTask;
            }));

        var provider = services.BuildServiceProvider();
        var invoker = new MessageHandlerInvoker(provider);
        var message = new SampleMessage { Value = "test" };
        var expectedContext = new MessageContext
        {
            Envelope = TestData.CreateEnvelope(message),
            Transaction = NoOpTransportTransaction.Instance,
            DeliveryCount = 5
        };
        expectedContext.Properties["custom"] = "value";

        // Act
        Assert.True(invoker.TryInvokeHandler(message, typeof(SampleMessage), expectedContext, CancellationToken.None, out var task));
        await task;

        // Assert
        Assert.NotNull(receivedContext);
        Assert.Same(expectedContext, receivedContext);
        Assert.Equal(5, receivedContext.DeliveryCount);
        Assert.Equal("value", receivedContext.Properties["custom"]);
    }

    /// <summary>
    /// Test message type for multiple handler scenarios.
    /// </summary>
    private sealed class OtherMessage
    {
        public string Data { get; set; } = string.Empty;
    }
}
