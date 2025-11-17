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
                return Task.FromResult(MessageProcessingResult.Success);
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
                return Task.FromResult(MessageProcessingResult.Success);
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
}
