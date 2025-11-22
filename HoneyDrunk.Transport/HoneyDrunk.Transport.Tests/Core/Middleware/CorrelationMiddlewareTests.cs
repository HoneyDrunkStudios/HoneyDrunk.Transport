using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.Context;
using HoneyDrunk.Transport.Pipeline.Middleware;
using HoneyDrunk.Transport.Tests.Support;

namespace HoneyDrunk.Transport.Tests.Core.Middleware;

/// <summary>
/// Tests for deprecated correlation middleware (backward compatibility).
/// </summary>
public sealed class CorrelationMiddlewareTests
{
    /// <summary>
    /// Verifies middleware is marked as obsolete.
    /// </summary>
    [Fact]
    public void Class_IsMarkedObsolete()
    {
        // Arrange & Act
#pragma warning disable CS0618 // Type or member is obsolete
        var obsoleteAttribute = typeof(CorrelationMiddleware)
            .GetCustomAttributes(typeof(ObsoleteAttribute), false)
            .FirstOrDefault() as ObsoleteAttribute;
#pragma warning restore CS0618

        // Assert
        Assert.NotNull(obsoleteAttribute);
        Assert.Contains("GridContextPropagationMiddleware", obsoleteAttribute.Message);
    }

    /// <summary>
    /// Verifies middleware creates Grid context from envelope.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task InvokeAsync_CreatesGridContextFromEnvelope()
    {
        // Arrange
        var envelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" });
        envelope = new Primitives.TransportEnvelope
        {
            MessageId = envelope.MessageId,
            MessageType = envelope.MessageType,
            Payload = envelope.Payload,
            CorrelationId = "corr-123",
            CausationId = "cause-456",
            Timestamp = envelope.Timestamp
        };

        var context = new MessageContext
        {
            Envelope = envelope,
            Transaction = NoOpTransportTransaction.Instance,
            DeliveryCount = 1
        };

        var gridContextFactory = new GridContextFactory(TimeProvider.System);
#pragma warning disable CS0618 // Type or member is obsolete
        var middleware = new CorrelationMiddleware(gridContextFactory);
#pragma warning restore CS0618

        var nextCalled = false;

        // Act
        await middleware.InvokeAsync(
            envelope,
            context,
            () =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            CancellationToken.None);

        // Assert
        Assert.True(nextCalled);
        Assert.True(context.Properties.ContainsKey("GridContext"));
    }

    /// <summary>
    /// Verifies middleware stores legacy "KernelContext" property for backward compatibility.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task InvokeAsync_StoresLegacyKernelContextProperty()
    {
        // Arrange
        var envelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" });
        var context = new MessageContext
        {
            Envelope = envelope,
            Transaction = NoOpTransportTransaction.Instance,
            DeliveryCount = 1
        };

        var gridContextFactory = new GridContextFactory(TimeProvider.System);
#pragma warning disable CS0618 // Type or member is obsolete
        var middleware = new CorrelationMiddleware(gridContextFactory);
#pragma warning restore CS0618

        // Act
        await middleware.InvokeAsync(
            envelope,
            context,
            () => Task.CompletedTask,
            CancellationToken.None);

        // Assert - verify legacy property exists
        Assert.True(context.Properties.TryGetValue("KernelContext", out var kernelContext) && kernelContext != null);
    }

    /// <summary>
    /// Verifies middleware stores CorrelationId and CausationId properties.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task InvokeAsync_StoresCorrelationAndCausationIdProperties()
    {
        // Arrange
        var envelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" });
        envelope = new Primitives.TransportEnvelope
        {
            MessageId = envelope.MessageId,
            MessageType = envelope.MessageType,
            Payload = envelope.Payload,
            CorrelationId = "test-corr",
            CausationId = "test-cause",
            Timestamp = envelope.Timestamp
        };

        var context = new MessageContext
        {
            Envelope = envelope,
            Transaction = NoOpTransportTransaction.Instance,
            DeliveryCount = 1
        };

        var gridContextFactory = new GridContextFactory(TimeProvider.System);
#pragma warning disable CS0618
        var middleware = new CorrelationMiddleware(gridContextFactory);
#pragma warning restore CS0618

        // Act
        await middleware.InvokeAsync(
            envelope,
            context,
            () => Task.CompletedTask,
            CancellationToken.None);

        // Assert
        Assert.True(context.Properties.TryGetValue("CorrelationId", out var correlationId));
        Assert.True(context.Properties.TryGetValue("CausationId", out var causationId));
        Assert.Equal("test-corr", correlationId);
        Assert.Equal("test-cause", causationId);
    }

    /// <summary>
    /// Verifies middleware respects cancellation token.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task InvokeAsync_WithCancelledToken_ThrowsOperationCancelledException()
    {
        // Arrange
        var envelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" });
        var context = new MessageContext
        {
            Envelope = envelope,
            Transaction = NoOpTransportTransaction.Instance,
            DeliveryCount = 1
        };

        var gridContextFactory = new GridContextFactory(TimeProvider.System);
#pragma warning disable CS0618
        var middleware = new CorrelationMiddleware(gridContextFactory);
#pragma warning restore CS0618

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => middleware.InvokeAsync(
                envelope,
                context,
                () => Task.CompletedTask,
                cts.Token));
    }

    /// <summary>
    /// Verifies middleware propagates exceptions from next delegate.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task InvokeAsync_WhenNextThrows_PropagatesException()
    {
        // Arrange
        var envelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" });
        var context = new MessageContext
        {
            Envelope = envelope,
            Transaction = NoOpTransportTransaction.Instance,
            DeliveryCount = 1
        };

        var gridContextFactory = new GridContextFactory(TimeProvider.System);
#pragma warning disable CS0618
        var middleware = new CorrelationMiddleware(gridContextFactory);
#pragma warning restore CS0618

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => middleware.InvokeAsync(
                envelope,
                context,
                () => throw new InvalidOperationException("test"),
                CancellationToken.None));
    }

    /// <summary>
    /// Verifies middleware behavior matches GridContextPropagationMiddleware for backward compatibility.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task InvokeAsync_BehaviorMatchesGridContextPropagationMiddleware()
    {
        // Arrange
        var envelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" });
        envelope = new Primitives.TransportEnvelope
        {
            MessageId = envelope.MessageId,
            MessageType = envelope.MessageType,
            Payload = envelope.Payload,
            CorrelationId = "match-test",
            CausationId = "match-cause",
            Timestamp = envelope.Timestamp
        };

        var context1 = new MessageContext
        {
            Envelope = envelope,
            Transaction = NoOpTransportTransaction.Instance,
            DeliveryCount = 1
        };

        var context2 = new MessageContext
        {
            Envelope = envelope,
            Transaction = NoOpTransportTransaction.Instance,
            DeliveryCount = 1
        };

        var gridContextFactory = new GridContextFactory(TimeProvider.System);
#pragma warning disable CS0618
        var correlationMiddleware = new CorrelationMiddleware(gridContextFactory);
#pragma warning restore CS0618
        var gridPropagationMiddleware = new GridContextPropagationMiddleware(gridContextFactory);

        // Act
        await correlationMiddleware.InvokeAsync(envelope, context1, () => Task.CompletedTask, CancellationToken.None);
        await gridPropagationMiddleware.InvokeAsync(envelope, context2, () => Task.CompletedTask, CancellationToken.None);

        // Assert - both should populate GridContext property
        Assert.True(context1.Properties.ContainsKey("GridContext"));
        Assert.True(context2.Properties.ContainsKey("GridContext"));

        Assert.True(context1.Properties.TryGetValue("CorrelationId", out var corr1));
        Assert.True(context2.Properties.TryGetValue("CorrelationId", out var corr2));
        Assert.Equal(corr2, corr1);

        Assert.True(context1.Properties.TryGetValue("CausationId", out var cause1));
        Assert.True(context2.Properties.TryGetValue("CausationId", out var cause2));
        Assert.Equal(cause2, cause1);
    }

    /// <summary>
    /// Verifies middleware handles missing correlation/causation IDs with fallback.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task InvokeAsync_WithMissingIds_FallsBackToMessageId()
    {
        // Arrange - No CorrelationId or CausationId set
        var envelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" });

        var context = new MessageContext
        {
            Envelope = envelope,
            Transaction = NoOpTransportTransaction.Instance,
            DeliveryCount = 1
        };

        var gridContextFactory = new GridContextFactory(TimeProvider.System);
#pragma warning disable CS0618
        var middleware = new CorrelationMiddleware(gridContextFactory);
#pragma warning restore CS0618

        // Act
        await middleware.InvokeAsync(
            envelope,
            context,
            () => Task.CompletedTask,
            CancellationToken.None);

        // Assert - should fallback to messageId
        Assert.True(context.Properties.TryGetValue("CorrelationId", out var correlationId));
        Assert.True(context.Properties.TryGetValue("CausationId", out var causationId));
        Assert.Equal(envelope.MessageId, correlationId);
        Assert.Equal(envelope.MessageId, causationId);
    }
}
