using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.DependencyInjection;
using HoneyDrunk.Transport.Tests.Support;

namespace HoneyDrunk.Transport.Tests.Core.DependencyInjection;

/// <summary>
/// Tests for DelegateMessageHandler adapter.
/// </summary>
public sealed class DelegateMessageHandlerTests
{
    /// <summary>
    /// Verifies that HandleAsync invokes the delegate.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task HandleAsync_InvokesDelegate()
    {
        // Arrange
        var invoked = false;
        SampleMessage? receivedMessage = null;
        MessageContext? receivedContext = null;
        CancellationToken receivedToken = default;

        Task Handler(SampleMessage msg, MessageContext ctx, CancellationToken ct)
        {
            invoked = true;
            receivedMessage = msg;
            receivedContext = ctx;
            receivedToken = ct;
            return Task.CompletedTask;
        }

        var delegateHandler = new DelegateMessageHandler<SampleMessage>(Handler);
        var message = new SampleMessage { Value = "test" };
        var context = new MessageContext
        {
            Envelope = TestData.CreateEnvelope(message),
            Transaction = NoOpTransportTransaction.Instance,
            DeliveryCount = 1
        };

        using var cts = new CancellationTokenSource();

        // Act
        await delegateHandler.HandleAsync(message, context, cts.Token);

        // Assert
        Assert.True(invoked);
        Assert.Same(message, receivedMessage);
        Assert.Same(context, receivedContext);
        Assert.Equal(cts.Token, receivedToken);
    }

    /// <summary>
    /// Verifies that HandleAsync propagates exceptions from delegate.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task HandleAsync_DelegateThrows_PropagatesException()
    {
        // Arrange
        var expectedException = new InvalidOperationException("Delegate error");
        Task Handler(SampleMessage msg, MessageContext ctx, CancellationToken ct)
        {
            throw expectedException;
        }

        var delegateHandler = new DelegateMessageHandler<SampleMessage>(Handler);
        var message = new SampleMessage { Value = "test" };
        var context = new MessageContext
        {
            Envelope = TestData.CreateEnvelope(message),
            Transaction = NoOpTransportTransaction.Instance,
            DeliveryCount = 1
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => delegateHandler.HandleAsync(message, context, CancellationToken.None));
        Assert.Same(expectedException, exception);
    }

    /// <summary>
    /// Verifies that HandleAsync respects cancellation token.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task HandleAsync_WithCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        static async Task Handler(SampleMessage msg, MessageContext ctx, CancellationToken ct)
        {
            await Task.Delay(100, ct);
        }

        var delegateHandler = new DelegateMessageHandler<SampleMessage>(Handler);
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
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => delegateHandler.HandleAsync(message, context, cts.Token));
    }

    /// <summary>
    /// Verifies that HandleAsync completes task from delegate.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task HandleAsync_CompletesTask()
    {
        // Arrange
        var completed = false;
        Task Handler(SampleMessage msg, MessageContext ctx, CancellationToken ct)
        {
            completed = true;
            return Task.CompletedTask;
        }

        var delegateHandler = new DelegateMessageHandler<SampleMessage>(Handler);
        var message = new SampleMessage { Value = "test" };
        var context = new MessageContext
        {
            Envelope = TestData.CreateEnvelope(message),
            Transaction = NoOpTransportTransaction.Instance,
            DeliveryCount = 1
        };

        // Act
        await delegateHandler.HandleAsync(message, context, CancellationToken.None);

        // Assert
        Assert.True(completed);
    }

    /// <summary>
    /// Verifies that HandleAsync works with async delegates.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task HandleAsync_WithAsyncDelegate_CompletesSuccessfully()
    {
        // Arrange
        var completed = false;
        async Task Handler(SampleMessage msg, MessageContext ctx, CancellationToken ct)
        {
            await Task.Delay(10, ct);
            completed = true;
        }

        var delegateHandler = new DelegateMessageHandler<SampleMessage>(Handler);
        var message = new SampleMessage { Value = "test" };
        var context = new MessageContext
        {
            Envelope = TestData.CreateEnvelope(message),
            Transaction = NoOpTransportTransaction.Instance,
            DeliveryCount = 1
        };

        // Act
        await delegateHandler.HandleAsync(message, context, CancellationToken.None);

        // Assert
        Assert.True(completed);
    }

    /// <summary>
    /// Verifies that HandleAsync passes message properties correctly.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task HandleAsync_PassesMessagePropertiesCorrectly()
    {
        // Arrange
        string? receivedValue = null;
        Task Handler(SampleMessage msg, MessageContext ctx, CancellationToken ct)
        {
            receivedValue = msg.Value;
            return Task.CompletedTask;
        }

        var delegateHandler = new DelegateMessageHandler<SampleMessage>(Handler);
        var message = new SampleMessage { Value = "expected-value" };
        var context = new MessageContext
        {
            Envelope = TestData.CreateEnvelope(message),
            Transaction = NoOpTransportTransaction.Instance,
            DeliveryCount = 1
        };

        // Act
        await delegateHandler.HandleAsync(message, context, CancellationToken.None);

        // Assert
        Assert.Equal("expected-value", receivedValue);
    }

    /// <summary>
    /// Verifies that HandleAsync passes context properties correctly.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task HandleAsync_PassesContextPropertiesCorrectly()
    {
        // Arrange
        int receivedDeliveryCount = 0;
        object? receivedProperty = null;
        Task Handler(SampleMessage msg, MessageContext ctx, CancellationToken ct)
        {
            receivedDeliveryCount = ctx.DeliveryCount;
            ctx.Properties.TryGetValue("custom", out receivedProperty);
            return Task.CompletedTask;
        }

        var delegateHandler = new DelegateMessageHandler<SampleMessage>(Handler);
        var message = new SampleMessage { Value = "test" };
        var context = new MessageContext
        {
            Envelope = TestData.CreateEnvelope(message),
            Transaction = NoOpTransportTransaction.Instance,
            DeliveryCount = 7
        };
        context.Properties["custom"] = "custom-value";

        // Act
        await delegateHandler.HandleAsync(message, context, CancellationToken.None);

        // Assert
        Assert.Equal(7, receivedDeliveryCount);
        Assert.Equal("custom-value", receivedProperty);
    }

    /// <summary>
    /// Verifies that HandleAsync can be called multiple times.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task HandleAsync_CalledMultipleTimes_WorksCorrectly()
    {
        // Arrange
        var callCount = 0;
        Task Handler(SampleMessage msg, MessageContext ctx, CancellationToken ct)
        {
            callCount++;
            return Task.CompletedTask;
        }

        var delegateHandler = new DelegateMessageHandler<SampleMessage>(Handler);
        var message = new SampleMessage { Value = "test" };
        var context = new MessageContext
        {
            Envelope = TestData.CreateEnvelope(message),
            Transaction = NoOpTransportTransaction.Instance,
            DeliveryCount = 1
        };

        // Act
        await delegateHandler.HandleAsync(message, context, CancellationToken.None);
        await delegateHandler.HandleAsync(message, context, CancellationToken.None);
        await delegateHandler.HandleAsync(message, context, CancellationToken.None);

        // Assert
        Assert.Equal(3, callCount);
    }
}
