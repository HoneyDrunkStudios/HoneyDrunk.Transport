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
    /// Verifies that InvokeHandlerAsync returns null when no handler is registered.
    /// </summary>
    [Fact]
    public void InvokeHandlerAsync_WithNoHandlerRegistered_ReturnsNull()
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
        var result = invoker.InvokeHandlerAsync(message, typeof(SampleMessage), context, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    /// <summary>
    /// Verifies that InvokeHandlerAsync invokes the registered handler successfully.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task InvokeHandlerAsync_WithRegisteredHandler_InvokesHandler()
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
        var task = invoker.InvokeHandlerAsync(message, typeof(SampleMessage), context, CancellationToken.None);
        Assert.NotNull(task);
        await task;

        // Assert
        Assert.True(handlerCalled);
    }

    /// <summary>
    /// Verifies that InvokeHandlerAsync caches the compiled delegate for performance.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task InvokeHandlerAsync_CalledMultipleTimes_UsesCachedDelegate()
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
        var task1 = invoker.InvokeHandlerAsync(message, typeof(SampleMessage), context, CancellationToken.None);
        Assert.NotNull(task1);
        await task1;

        var task2 = invoker.InvokeHandlerAsync(message, typeof(SampleMessage), context, CancellationToken.None);
        Assert.NotNull(task2);
        await task2;

        var task3 = invoker.InvokeHandlerAsync(message, typeof(SampleMessage), context, CancellationToken.None);
        Assert.NotNull(task3);
        await task3;

        // Assert - handler was called 3 times (verifies caching didn't break invocation)
        Assert.Equal(3, callCount);
    }

    /// <summary>
    /// Verifies that InvokeHandlerAsync throws ArgumentNullException for null message.
    /// </summary>
    [Fact]
    public void InvokeHandlerAsync_WithNullMessage_ThrowsArgumentNullException()
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

        // Act & Assert - ArgumentNullException.ThrowIfNull is synchronous guard
        try
        {
            invoker.InvokeHandlerAsync(null!, typeof(SampleMessage), context, CancellationToken.None);
            Assert.Fail("Expected ArgumentNullException");
        }
        catch (ArgumentNullException ex)
        {
            Assert.NotNull(ex);
        }
    }

    /// <summary>
    /// Verifies that InvokeHandlerAsync throws ArgumentNullException for null message type.
    /// </summary>
    [Fact]
    public void InvokeHandlerAsync_WithNullMessageType_ThrowsArgumentNullException()
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

        // Act & Assert - ArgumentNullException.ThrowIfNull is synchronous guard
        try
        {
            invoker.InvokeHandlerAsync(message, null!, context, CancellationToken.None);
            Assert.Fail("Expected ArgumentNullException");
        }
        catch (ArgumentNullException ex)
        {
            Assert.NotNull(ex);
        }
    }

    /// <summary>
    /// Verifies that InvokeHandlerAsync propagates handler exceptions.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task InvokeHandlerAsync_WhenHandlerThrows_PropagatesException()
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

        // Act & Assert - Pass lambda that invokes and returns the task
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            var task = invoker.InvokeHandlerAsync(message, typeof(SampleMessage), context, CancellationToken.None);
            Assert.NotNull(task);
            await task;
        });

        Assert.Same(expectedException, exception);
    }

    /// <summary>
    /// Verifies that InvokeHandlerAsync respects cancellation tokens.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task InvokeHandlerAsync_WithCancelledToken_ThrowsOperationCanceledException()
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
        var task = invoker.InvokeHandlerAsync(message, typeof(SampleMessage), context, cts.Token);
        Assert.NotNull(task);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    /// <summary>
    /// Verifies that InvokeHandlerAsync works with different message types.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task InvokeHandlerAsync_WithDifferentMessageTypes_InvokesCorrectHandlers()
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
        var task1 = invoker.InvokeHandlerAsync(sampleMessage, typeof(SampleMessage), context, CancellationToken.None);
        Assert.NotNull(task1);
        await task1;

        // Act - invoke OtherMessage handler
        var task2 = invoker.InvokeHandlerAsync(otherMessage, typeof(OtherMessage), context, CancellationToken.None);
        Assert.NotNull(task2);
        await task2;

        // Assert
        Assert.True(sampleHandlerCalled);
        Assert.True(otherHandlerCalled);
    }

    /// <summary>
    /// Verifies that InvokeHandlerAsync passes context correctly to handler.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task InvokeHandlerAsync_PassesContextCorrectly()
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
        var task = invoker.InvokeHandlerAsync(message, typeof(SampleMessage), expectedContext, CancellationToken.None);
        Assert.NotNull(task);
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
