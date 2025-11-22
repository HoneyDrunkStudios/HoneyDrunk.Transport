using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.Context;
using HoneyDrunk.Transport.Pipeline.Middleware;
using HoneyDrunk.Transport.Tests.Support;

namespace HoneyDrunk.Transport.Tests.Core.Middleware;

/// <summary>
/// Tests for Grid context propagation middleware.
/// </summary>
public sealed class GridContextPropagationMiddlewareTests
{
    /// <summary>
    /// Verifies middleware creates Grid context from envelope and populates MessageContext.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task InvokeAsync_WithValidEnvelope_CreatesAndPopulatesGridContext()
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
            NodeId = "node-1",
            StudioId = "studio-1",
            Environment = "production",
            Headers = new Dictionary<string, string> { ["key1"] = "value1" },
            Timestamp = envelope.Timestamp
        };

        var context = new MessageContext
        {
            Envelope = envelope,
            Transaction = NoOpTransportTransaction.Instance,
            DeliveryCount = 1
        };

        var gridContextFactory = new GridContextFactory(TimeProvider.System);
        var middleware = new GridContextPropagationMiddleware(gridContextFactory);

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
        Assert.NotNull(context.GridContext);
        Assert.Equal("corr-123", context.GridContext!.CorrelationId);
        Assert.Equal("cause-456", context.GridContext.CausationId);
        Assert.Equal("node-1", context.GridContext.NodeId);
        Assert.Equal("studio-1", context.GridContext.StudioId);
        Assert.Equal("production", context.GridContext.Environment);
    }

    /// <summary>
    /// Verifies middleware populates backward compatibility properties in context.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task InvokeAsync_PopulatesBackwardCompatibilityProperties()
    {
        // Arrange
        var envelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" });
        envelope = new Primitives.TransportEnvelope
        {
            MessageId = envelope.MessageId,
            MessageType = envelope.MessageType,
            Payload = envelope.Payload,
            CorrelationId = "corr-abc",
            CausationId = "cause-xyz",
            NodeId = "node-2",
            StudioId = "studio-2",
            Environment = "staging",
            Timestamp = envelope.Timestamp
        };

        var context = new MessageContext
        {
            Envelope = envelope,
            Transaction = NoOpTransportTransaction.Instance,
            DeliveryCount = 1
        };

        var gridContextFactory = new GridContextFactory(TimeProvider.System);
        var middleware = new GridContextPropagationMiddleware(gridContextFactory);

        // Act
        await middleware.InvokeAsync(
            envelope,
            context,
            () => Task.CompletedTask,
            CancellationToken.None);

        // Assert - verify properties dictionary is populated
        Assert.True(context.Properties.ContainsKey("GridContext"));
        Assert.True(context.Properties.TryGetValue("CorrelationId", out var correlationId));
        Assert.True(context.Properties.TryGetValue("CausationId", out var causationId));
        Assert.True(context.Properties.TryGetValue("NodeId", out var nodeId));
        Assert.True(context.Properties.TryGetValue("StudioId", out var studioId));
        Assert.True(context.Properties.TryGetValue("Environment", out var environment));

        Assert.Equal("corr-abc", correlationId);
        Assert.Equal("cause-xyz", causationId);
        Assert.Equal("node-2", nodeId);
        Assert.Equal("studio-2", studioId);
        Assert.Equal("staging", environment);
    }

    /// <summary>
    /// Verifies middleware handles envelopes with missing optional fields.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task InvokeAsync_WithMissingOptionalFields_CreatesGridContextWithDefaults()
    {
        // Arrange - Envelope has no CorrelationId, CausationId, NodeId, StudioId, Environment
        var envelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" });

        var context = new MessageContext
        {
            Envelope = envelope,
            Transaction = NoOpTransportTransaction.Instance,
            DeliveryCount = 1
        };

        var gridContextFactory = new GridContextFactory(TimeProvider.System);
        var middleware = new GridContextPropagationMiddleware(gridContextFactory);

        // Act
        await middleware.InvokeAsync(
            envelope,
            context,
            () => Task.CompletedTask,
            CancellationToken.None);

        // Assert - should use messageId as fallback for correlation
        Assert.NotNull(context.GridContext);
        Assert.Equal(envelope.MessageId, context.GridContext!.CorrelationId);
        Assert.Equal(envelope.MessageId, context.GridContext.CausationId);
        Assert.Equal(string.Empty, context.GridContext.NodeId);
        Assert.Equal(string.Empty, context.GridContext.StudioId);
        Assert.Equal(string.Empty, context.GridContext.Environment);
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
        var middleware = new GridContextPropagationMiddleware(gridContextFactory);

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
        var middleware = new GridContextPropagationMiddleware(gridContextFactory);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => middleware.InvokeAsync(
                envelope,
                context,
                () => throw new InvalidOperationException("test error"),
                CancellationToken.None));
    }

    /// <summary>
    /// Verifies middleware propagates baggage from envelope headers.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task InvokeAsync_WithEnvelopeHeaders_PropagatesBaggage()
    {
        // Arrange
        var envelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" });
        envelope = new Primitives.TransportEnvelope
        {
            MessageId = envelope.MessageId,
            MessageType = envelope.MessageType,
            Payload = envelope.Payload,
            CorrelationId = "corr-123",
            Headers = new Dictionary<string, string>
            {
                ["baggage-key1"] = "value1",
                ["baggage-key2"] = "value2"
            },
            Timestamp = envelope.Timestamp
        };

        var context = new MessageContext
        {
            Envelope = envelope,
            Transaction = NoOpTransportTransaction.Instance,
            DeliveryCount = 1
        };

        var gridContextFactory = new GridContextFactory(TimeProvider.System);
        var middleware = new GridContextPropagationMiddleware(gridContextFactory);

        // Act
        await middleware.InvokeAsync(
            envelope,
            context,
            () => Task.CompletedTask,
            CancellationToken.None);

        // Assert
        Assert.NotNull(context.GridContext);
        Assert.Equal(2, context.GridContext!.Baggage.Count);
        Assert.Equal("value1", context.GridContext.Baggage["baggage-key1"]);
        Assert.Equal("value2", context.GridContext.Baggage["baggage-key2"]);
    }
}
