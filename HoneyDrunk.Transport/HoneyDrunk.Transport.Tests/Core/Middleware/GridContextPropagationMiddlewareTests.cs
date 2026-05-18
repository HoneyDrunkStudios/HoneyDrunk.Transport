using HoneyDrunk.Kernel.Abstractions.Context;
using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.Pipeline.Middleware;
using HoneyDrunk.Transport.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

using TransportGridContextFactory = HoneyDrunk.Transport.Context.GridContextFactory;

namespace HoneyDrunk.Transport.Tests.Core.Middleware;

/// <summary>
/// Tests for Grid context propagation middleware.
/// </summary>
/// <remarks>
/// These tests verify the abstractions-only pattern where the middleware creates an initialized GridContextSnapshot.
/// </remarks>
public sealed class GridContextPropagationMiddlewareTests
{
    private const string TestNodeId = "test-node";
    private const string TestStudioId = "test-studio";
    private const string TestEnvironment = "test-env";

    /// <summary>
    /// Verifies middleware initializes Grid context from envelope and populates MessageContext.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task InvokeAsync_WithValidEnvelope_InitializesAndPopulatesGridContext()
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

        var serviceProvider = CreateServiceProvider();
        var context = new MessageContext
        {
            Envelope = envelope,
            Transaction = NoOpTransportTransaction.Instance,
            DeliveryCount = 1,
            ServiceProvider = serviceProvider
        };

        var gridContextFactory = new TransportGridContextFactory();
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
            DeliveryCount = 1,
            ServiceProvider = CreateServiceProvider()
        };

        var gridContextFactory = new TransportGridContextFactory();
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

        Assert.Equal("corr-abc", correlationId);
        Assert.Equal("cause-xyz", causationId);
    }

    /// <summary>
    /// Verifies middleware handles envelopes with missing optional fields.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task InvokeAsync_WithMissingOptionalFields_InitializesGridContextWithDefaults()
    {
        // Arrange - Envelope has no CorrelationId, CausationId
        var envelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" });

        var context = new MessageContext
        {
            Envelope = envelope,
            Transaction = NoOpTransportTransaction.Instance,
            DeliveryCount = 1,
            ServiceProvider = CreateServiceProvider()
        };

        var gridContextFactory = new TransportGridContextFactory();
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
        Assert.True(context.GridContext.IsInitialized);
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
            DeliveryCount = 1,
            ServiceProvider = CreateServiceProvider()
        };

        var gridContextFactory = new TransportGridContextFactory();
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
            DeliveryCount = 1,
            ServiceProvider = CreateServiceProvider()
        };

        var gridContextFactory = new TransportGridContextFactory();
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
            DeliveryCount = 1,
            ServiceProvider = CreateServiceProvider()
        };

        var gridContextFactory = new TransportGridContextFactory();
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

    /// <summary>
    /// Creates a service provider with a scoped GridContext.
    /// </summary>
    /// <remarks>
    /// The scope is intentionally created and not disposed during tests.
    /// Each test method creates its own scope for isolation.
    /// </remarks>
#pragma warning disable CA2000 // Dispose objects before losing scope - intentional for test isolation
    private static IServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddScoped<IGridContext>(_ => new GridContextSnapshot(TestNodeId, TestStudioId, TestEnvironment));
        return services.BuildServiceProvider().CreateScope().ServiceProvider;
    }
#pragma warning restore CA2000
}
