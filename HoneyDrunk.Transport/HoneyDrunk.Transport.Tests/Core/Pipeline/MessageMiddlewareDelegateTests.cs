using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.Tests.Support;

namespace HoneyDrunk.Transport.Tests.Core.Pipeline;

/// <summary>
/// Tests for MessageMiddleware delegate pattern.
/// </summary>
public sealed class MessageMiddlewareDelegateTests
{
    /// <summary>
    /// Verifies delegate middleware is invoked in pipeline.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task MessageMiddleware_Delegate_IsInvoked()
    {
        // Arrange
        var invoked = false;
        async Task Middleware(ITransportEnvelope envelope, MessageContext context, Func<Task> next, CancellationToken ct)
        {
            invoked = true;
            await next();
        }

        var envelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" });
        var context = new MessageContext
        {
            Envelope = envelope,
            Transaction = NoOpTransportTransaction.Instance,
            DeliveryCount = 1
        };

        var nextCalled = false;
        Task Next()
        {
            nextCalled = true;
            return Task.CompletedTask;
        }

        // Act
        await Middleware(envelope, context, Next, CancellationToken.None);

        // Assert
        Assert.True(invoked);
        Assert.True(nextCalled);
    }

    /// <summary>
    /// Verifies delegate middleware receives envelope correctly.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task MessageMiddleware_ReceivesEnvelope()
    {
        // Arrange
        ITransportEnvelope? receivedEnvelope = null;
        async Task Middleware(ITransportEnvelope envelope, MessageContext context, Func<Task> next, CancellationToken ct)
        {
            receivedEnvelope = envelope;
            await next();
        }

        var originalEnvelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" });
        var context = new MessageContext
        {
            Envelope = originalEnvelope,
            Transaction = NoOpTransportTransaction.Instance,
            DeliveryCount = 1
        };

        Task Next() => Task.CompletedTask;

        // Act
        await Middleware(originalEnvelope, context, Next, CancellationToken.None);

        // Assert
        Assert.Same(originalEnvelope, receivedEnvelope);
    }

    /// <summary>
    /// Verifies delegate middleware receives context correctly.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task MessageMiddleware_ReceivesContext()
    {
        // Arrange
        MessageContext? receivedContext = null;
        async Task Middleware(ITransportEnvelope envelope, MessageContext context, Func<Task> next, CancellationToken ct)
        {
            receivedContext = context;
            await next();
        }

        var envelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" });
        var originalContext = new MessageContext
        {
            Envelope = envelope,
            Transaction = NoOpTransportTransaction.Instance,
            DeliveryCount = 5
        };
        originalContext.Properties["custom"] = "value";

        Task Next() => Task.CompletedTask;

        // Act
        await Middleware(envelope, originalContext, Next, CancellationToken.None);

        // Assert
        Assert.Same(originalContext, receivedContext);
        Assert.Equal(5, receivedContext!.DeliveryCount);
        Assert.Equal("value", receivedContext.Properties["custom"]);
    }

    /// <summary>
    /// Verifies delegate middleware respects cancellation token.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task MessageMiddleware_RespectsCancellationToken()
    {
        // Arrange
        CancellationToken receivedToken = default;
        async Task Middleware(ITransportEnvelope envelope, MessageContext context, Func<Task> next, CancellationToken ct)
        {
            receivedToken = ct;
            ct.ThrowIfCancellationRequested();
            await next();
        }

        var envelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" });
        var context = new MessageContext
        {
            Envelope = envelope,
            Transaction = NoOpTransportTransaction.Instance,
            DeliveryCount = 1
        };

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Task Next() => Task.CompletedTask;

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Middleware(envelope, context, Next, cts.Token));
        Assert.Equal(cts.Token, receivedToken);
    }

    /// <summary>
    /// Verifies delegate middleware can modify context.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task MessageMiddleware_CanModifyContext()
    {
        // Arrange
        static async Task Middleware(ITransportEnvelope envelope, MessageContext context, Func<Task> next, CancellationToken ct)
        {
            context.Properties["middleware-added"] = "test-value";
            await next();
        }

        var envelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" });
        var context = new MessageContext
        {
            Envelope = envelope,
            Transaction = NoOpTransportTransaction.Instance,
            DeliveryCount = 1
        };

        var nextSawModifiedContext = false;
        Task Next()
        {
            if (context.Properties.ContainsKey("middleware-added"))
            {
                nextSawModifiedContext = true;
            }

            return Task.CompletedTask;
        }

        // Act
        await Middleware(envelope, context, Next, CancellationToken.None);

        // Assert
        Assert.True(nextSawModifiedContext);
        Assert.Equal("test-value", context.Properties["middleware-added"]);
    }

    /// <summary>
    /// Verifies delegate middleware can short-circuit pipeline.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task MessageMiddleware_CanShortCircuit()
    {
        // Arrange
        var nextCalled = false;
        static Task Middleware(ITransportEnvelope envelope, MessageContext context, Func<Task> next, CancellationToken ct)
        {
            // Don't call next() - short-circuit
            return Task.CompletedTask;
        }

        var envelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" });
        var context = new MessageContext
        {
            Envelope = envelope,
            Transaction = NoOpTransportTransaction.Instance,
            DeliveryCount = 1
        };

        Task Next()
        {
            nextCalled = true;
            return Task.CompletedTask;
        }

        // Act
        await Middleware(envelope, context, Next, CancellationToken.None);

        // Assert
        Assert.False(nextCalled);
    }

    /// <summary>
    /// Verifies delegate middleware can throw exceptions.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task MessageMiddleware_CanThrowException()
    {
        // Arrange
        var expectedException = new InvalidOperationException("Middleware error");
        Task Middleware(ITransportEnvelope envelope, MessageContext context, Func<Task> next, CancellationToken ct)
        {
            throw expectedException;
        }

        var envelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" });
        var context = new MessageContext
        {
            Envelope = envelope,
            Transaction = NoOpTransportTransaction.Instance,
            DeliveryCount = 1
        };

        Task Next() => Task.CompletedTask;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Middleware(envelope, context, Next, CancellationToken.None));
        Assert.Same(expectedException, exception);
    }

    /// <summary>
    /// Verifies delegate middleware executes before next in chain.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task MessageMiddleware_ExecutesBeforeNext()
    {
        // Arrange
        var executionOrder = new List<string>();
        async Task Middleware(ITransportEnvelope envelope, MessageContext context, Func<Task> next, CancellationToken ct)
        {
            executionOrder.Add("before-next");
            await next();
            executionOrder.Add("after-next");
        }

        var envelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" });
        var context = new MessageContext
        {
            Envelope = envelope,
            Transaction = NoOpTransportTransaction.Instance,
            DeliveryCount = 1
        };

        Task Next()
        {
            executionOrder.Add("next");
            return Task.CompletedTask;
        }

        // Act
        await Middleware(envelope, context, Next, CancellationToken.None);

        // Assert
        Assert.Equal(3, executionOrder.Count);
        Assert.Equal("before-next", executionOrder[0]);
        Assert.Equal("next", executionOrder[1]);
        Assert.Equal("after-next", executionOrder[2]);
    }

    /// <summary>
    /// Verifies delegate middleware can chain multiple handlers.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task MessageMiddleware_CanChainMultipleMiddlewares()
    {
        // Arrange
        var executionOrder = new List<string>();

        async Task Middleware(ITransportEnvelope envelope, MessageContext context, Func<Task> next, CancellationToken ct)
        {
            executionOrder.Add("middleware1-before");
            await next();
            executionOrder.Add("middleware1-after");
        }

        async Task Middleware2(ITransportEnvelope envelope, MessageContext context, Func<Task> next, CancellationToken ct)
        {
            executionOrder.Add("middleware2-before");
            await next();
            executionOrder.Add("middleware2-after");
        }

        var envelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" });
        var context = new MessageContext
        {
            Envelope = envelope,
            Transaction = NoOpTransportTransaction.Instance,
            DeliveryCount = 1
        };

        Task FinalHandler()
        {
            executionOrder.Add("handler");
            return Task.CompletedTask;
        }

        // Act - middleware1 -> middleware2 -> handler
        await Middleware(
            envelope,
            context,
            () => Middleware2(envelope, context, FinalHandler, CancellationToken.None),
            CancellationToken.None);

        // Assert - onion pattern execution
        Assert.Collection(
            executionOrder,
            item => Assert.Equal("middleware1-before", item),
            item => Assert.Equal("middleware2-before", item),
            item => Assert.Equal("handler", item),
            item => Assert.Equal("middleware2-after", item),
            item => Assert.Equal("middleware1-after", item));
    }

    /// <summary>
    /// Verifies delegate middleware works with async operations.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task MessageMiddleware_WorksWithAsyncOperations()
    {
        // Arrange
        var completed = false;
        async Task Middleware(ITransportEnvelope envelope, MessageContext context, Func<Task> next, CancellationToken ct)
        {
            await Task.Delay(10, ct);
            await next();
            await Task.Delay(10, ct);
            completed = true;
        }

        var envelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" });
        var context = new MessageContext
        {
            Envelope = envelope,
            Transaction = NoOpTransportTransaction.Instance,
            DeliveryCount = 1
        };

        Task Next() => Task.CompletedTask;

        // Act
        await Middleware(envelope, context, Next, CancellationToken.None);

        // Assert
        Assert.True(completed);
    }

    /// <summary>
    /// Verifies delegate middleware receives default cancellation token correctly.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task MessageMiddleware_WithDefaultCancellationToken_WorksCorrectly()
    {
        // Arrange
        CancellationToken receivedToken = new(true); // Set to non-default initially
        async Task Middleware(ITransportEnvelope envelope, MessageContext context, Func<Task> next, CancellationToken ct)
        {
            receivedToken = ct;
            await next();
        }

        var envelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" });
        var context = new MessageContext
        {
            Envelope = envelope,
            Transaction = NoOpTransportTransaction.Instance,
            DeliveryCount = 1
        };

        Task Next() => Task.CompletedTask;

        // Act
        await Middleware(envelope, context, Next, default);

        // Assert
        Assert.Equal(default, receivedToken);
    }
}
