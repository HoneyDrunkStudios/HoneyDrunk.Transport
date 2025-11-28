using HoneyDrunk.Kernel.Abstractions.Context;
using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.DependencyInjection;
using HoneyDrunk.Transport.Primitives;
using HoneyDrunk.Transport.Publishers;
using HoneyDrunk.Transport.Tests.Support;
using NSubstitute;

namespace HoneyDrunk.Transport.Tests.Core.Publishers;

/// <summary>
/// Tests for <see cref="MessagePublisher"/> covering single/batch publishing,
/// Grid context propagation, null validation, and integration with transport layer.
/// </summary>
public sealed class MessagePublisherTests
{
    private static readonly DateTimeOffset FixedTime = new(2025, 1, 28, 19, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Verifies single message publishing correctly serializes, creates envelope with Grid context,
    /// and delegates to transport publisher.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task PublishAsync_SingleMessage_SerializesAndCreatesEnvelopeWithGridContext()
    {
        // Arrange
        var transportPublisher = Substitute.For<ITransportPublisher>();
        var serializer = new JsonMessageSerializer();
        var timeProvider = new TestTimeProvider(FixedTime);
        var factory = new EnvelopeFactory(timeProvider);
        var publisher = new MessagePublisher(transportPublisher, serializer, factory);

        var message = new SampleMessage { Value = "test-message" };
        var gridContext = CreateTestGridContext();
        var destination = "orders.created";

        // Act
        await publisher.PublishAsync(destination, message, gridContext, CancellationToken.None);

        // Assert
        await transportPublisher.Received(1).PublishAsync(
            Arg.Is<ITransportEnvelope>(env =>
                env.MessageType == typeof(SampleMessage).FullName &&
                env.CorrelationId == gridContext.CorrelationId &&
                env.CausationId == gridContext.CausationId &&
                env.NodeId == gridContext.NodeId &&
                env.StudioId == gridContext.StudioId &&
                env.TenantId == gridContext.TenantId &&
                env.ProjectId == gridContext.ProjectId &&
                env.Environment == gridContext.Environment &&
                env.Timestamp == FixedTime),
            Arg.Is<IEndpointAddress>(addr =>
                addr.Name == destination &&
                addr.Address == destination),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Verifies Grid context fields (TenantId, ProjectId, etc.) are correctly propagated
    /// through to the envelope.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task PublishAsync_PropagatesGridContextFieldsToEnvelope()
    {
        // Arrange
        var transportPublisher = Substitute.For<ITransportPublisher>();
        var serializer = new JsonMessageSerializer();
        var timeProvider = new TestTimeProvider(FixedTime);
        var factory = new EnvelopeFactory(timeProvider);
        var publisher = new MessagePublisher(transportPublisher, serializer, factory);

        var message = new SampleMessage { Value = "context-test" };
        var gridContext = CreateTestGridContext(
            correlationId: "corr-123",
            causationId: "cause-456",
            nodeId: "node-789",
            studioId: "studio-abc",
            tenantId: "tenant-def",
            projectId: "project-ghi",
            environment: "production");

        // Act
        await publisher.PublishAsync("test-queue", message, gridContext, CancellationToken.None);

        // Assert
        await transportPublisher.Received(1).PublishAsync(
            Arg.Is<ITransportEnvelope>(env =>
                env.CorrelationId == "corr-123" &&
                env.CausationId == "cause-456" &&
                env.NodeId == "node-789" &&
                env.StudioId == "studio-abc" &&
                env.TenantId == "tenant-def" &&
                env.ProjectId == "project-ghi" &&
                env.Environment == "production"),
            Arg.Any<IEndpointAddress>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Verifies message payload is correctly serialized and included in envelope.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task PublishAsync_SerializesMessagePayloadCorrectly()
    {
        // Arrange
        var transportPublisher = Substitute.For<ITransportPublisher>();
        var serializer = new JsonMessageSerializer();
        var timeProvider = new TestTimeProvider(FixedTime);
        var factory = new EnvelopeFactory(timeProvider);
        var publisher = new MessagePublisher(transportPublisher, serializer, factory);

        var message = new SampleMessage { Value = "payload-test" };
        var gridContext = CreateTestGridContext();

        // Act
        await publisher.PublishAsync("test-queue", message, gridContext, CancellationToken.None);

        // Assert
        await transportPublisher.Received(1).PublishAsync(
            Arg.Is<ITransportEnvelope>(env =>
                env.Payload.Length > 0 &&
                DeserializePayload(env.Payload, serializer).Value == "payload-test"),
            Arg.Any<IEndpointAddress>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Verifies EndpointAddress is correctly constructed from destination string.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task PublishAsync_ConstructsEndpointAddressFromDestination()
    {
        // Arrange
        var transportPublisher = Substitute.For<ITransportPublisher>();
        var serializer = new JsonMessageSerializer();
        var timeProvider = new TestTimeProvider(FixedTime);
        var factory = new EnvelopeFactory(timeProvider);
        var publisher = new MessagePublisher(transportPublisher, serializer, factory);

        var message = new SampleMessage { Value = "address-test" };
        var gridContext = CreateTestGridContext();
        var destination = "orders.events.created";

        // Act
        await publisher.PublishAsync(destination, message, gridContext, CancellationToken.None);

        // Assert
        await transportPublisher.Received(1).PublishAsync(
            Arg.Any<ITransportEnvelope>(),
            Arg.Is<IEndpointAddress>(addr =>
                addr.Name == destination &&
                addr.Address == destination),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Verifies null destination throws ArgumentNullException.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task PublishAsync_NullDestination_ThrowsArgumentNullException()
    {
        // Arrange
        var transportPublisher = Substitute.For<ITransportPublisher>();
        var serializer = new JsonMessageSerializer();
        var timeProvider = new TestTimeProvider(FixedTime);
        var factory = new EnvelopeFactory(timeProvider);
        var publisher = new MessagePublisher(transportPublisher, serializer, factory);

        var message = new SampleMessage { Value = "test" };
        var gridContext = CreateTestGridContext();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await publisher.PublishAsync(null!, message, gridContext, CancellationToken.None));
    }

    /// <summary>
    /// Verifies null message throws ArgumentNullException.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task PublishAsync_NullMessage_ThrowsArgumentNullException()
    {
        // Arrange
        var transportPublisher = Substitute.For<ITransportPublisher>();
        var serializer = new JsonMessageSerializer();
        var timeProvider = new TestTimeProvider(FixedTime);
        var factory = new EnvelopeFactory(timeProvider);
        var publisher = new MessagePublisher(transportPublisher, serializer, factory);

        var gridContext = CreateTestGridContext();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await publisher.PublishAsync<SampleMessage>("test-queue", null!, gridContext, CancellationToken.None));
    }

    /// <summary>
    /// Verifies null Grid context throws ArgumentNullException.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task PublishAsync_NullGridContext_ThrowsArgumentNullException()
    {
        // Arrange
        var transportPublisher = Substitute.For<ITransportPublisher>();
        var serializer = new JsonMessageSerializer();
        var timeProvider = new TestTimeProvider(FixedTime);
        var factory = new EnvelopeFactory(timeProvider);
        var publisher = new MessagePublisher(transportPublisher, serializer, factory);

        var message = new SampleMessage { Value = "test" };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await publisher.PublishAsync("test-queue", message, null!, CancellationToken.None));
    }

    /// <summary>
    /// Verifies batch publishing handles multiple messages correctly.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task PublishBatchAsync_MultipleMessages_CreatesEnvelopesForAll()
    {
        // Arrange
        var transportPublisher = Substitute.For<ITransportPublisher>();
        var serializer = new JsonMessageSerializer();
        var timeProvider = new TestTimeProvider(FixedTime);
        var factory = new EnvelopeFactory(timeProvider);
        var publisher = new MessagePublisher(transportPublisher, serializer, factory);

        var messages = new[]
        {
            new SampleMessage { Value = "message-1" },
            new SampleMessage { Value = "message-2" },
            new SampleMessage { Value = "message-3" }
        };
        var gridContext = CreateTestGridContext();

        // Act
        await publisher.PublishBatchAsync("test-queue", messages, gridContext, CancellationToken.None);

        // Assert
        await transportPublisher.Received(1).PublishBatchAsync(
            Arg.Is<IEnumerable<ITransportEnvelope>>(envelopes =>
                envelopes.Count() == 3 &&
                envelopes.All(env => env.MessageType == typeof(SampleMessage).FullName)),
            Arg.Any<IEndpointAddress>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Verifies batch publishing with empty collection returns early without calling transport.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task PublishBatchAsync_EmptyCollection_ReturnsEarlyWithoutPublishing()
    {
        // Arrange
        var transportPublisher = Substitute.For<ITransportPublisher>();
        var serializer = new JsonMessageSerializer();
        var timeProvider = new TestTimeProvider(FixedTime);
        var factory = new EnvelopeFactory(timeProvider);
        var publisher = new MessagePublisher(transportPublisher, serializer, factory);

        var messages = Array.Empty<SampleMessage>();
        var gridContext = CreateTestGridContext();

        // Act
        await publisher.PublishBatchAsync("test-queue", messages, gridContext, CancellationToken.None);

        // Assert
        await transportPublisher.DidNotReceive().PublishBatchAsync(
            Arg.Any<IEnumerable<ITransportEnvelope>>(),
            Arg.Any<IEndpointAddress>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Verifies batch publishing shares same Grid context across all envelopes.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task PublishBatchAsync_SharesGridContextAcrossAllEnvelopes()
    {
        // Arrange
        var transportPublisher = Substitute.For<ITransportPublisher>();
        var serializer = new JsonMessageSerializer();
        var timeProvider = new TestTimeProvider(FixedTime);
        var factory = new EnvelopeFactory(timeProvider);
        var publisher = new MessagePublisher(transportPublisher, serializer, factory);

        var messages = new[]
        {
            new SampleMessage { Value = "msg-1" },
            new SampleMessage { Value = "msg-2" }
        };
        var gridContext = CreateTestGridContext(
            correlationId: "shared-corr",
            tenantId: "shared-tenant");

        // Act
        await publisher.PublishBatchAsync("test-queue", messages, gridContext, CancellationToken.None);

        // Assert
        await transportPublisher.Received(1).PublishBatchAsync(
            Arg.Is<IEnumerable<ITransportEnvelope>>(envelopes =>
                envelopes.All(env =>
                    env.CorrelationId == "shared-corr" &&
                    env.TenantId == "shared-tenant")),
            Arg.Any<IEndpointAddress>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Verifies each message in batch gets unique MessageId.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task PublishBatchAsync_AssignsUniqueMessageIdToEachEnvelope()
    {
        // Arrange
        var transportPublisher = Substitute.For<ITransportPublisher>();
        var serializer = new JsonMessageSerializer();
        var timeProvider = new TestTimeProvider(FixedTime);
        var factory = new EnvelopeFactory(timeProvider);
        var publisher = new MessagePublisher(transportPublisher, serializer, factory);

        var messages = new[]
        {
            new SampleMessage { Value = "msg-1" },
            new SampleMessage { Value = "msg-2" },
            new SampleMessage { Value = "msg-3" }
        };
        var gridContext = CreateTestGridContext();

        // Act
        await publisher.PublishBatchAsync("test-queue", messages, gridContext, CancellationToken.None);

        // Assert
        await transportPublisher.Received(1).PublishBatchAsync(
            Arg.Is<IEnumerable<ITransportEnvelope>>(envelopes =>
                envelopes.Select(env => env.MessageId).Distinct().Count() == 3),
            Arg.Any<IEndpointAddress>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Verifies batch publishing with null destination throws ArgumentNullException.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task PublishBatchAsync_NullDestination_ThrowsArgumentNullException()
    {
        // Arrange
        var transportPublisher = Substitute.For<ITransportPublisher>();
        var serializer = new JsonMessageSerializer();
        var timeProvider = new TestTimeProvider(FixedTime);
        var factory = new EnvelopeFactory(timeProvider);
        var publisher = new MessagePublisher(transportPublisher, serializer, factory);

        var messages = new[] { new SampleMessage { Value = "test" } };
        var gridContext = CreateTestGridContext();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await publisher.PublishBatchAsync(null!, messages, gridContext, CancellationToken.None));
    }

    /// <summary>
    /// Verifies batch publishing with null messages collection throws ArgumentNullException.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task PublishBatchAsync_NullMessages_ThrowsArgumentNullException()
    {
        // Arrange
        var transportPublisher = Substitute.For<ITransportPublisher>();
        var serializer = new JsonMessageSerializer();
        var timeProvider = new TestTimeProvider(FixedTime);
        var factory = new EnvelopeFactory(timeProvider);
        var publisher = new MessagePublisher(transportPublisher, serializer, factory);

        var gridContext = CreateTestGridContext();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await publisher.PublishBatchAsync<SampleMessage>("test-queue", null!, gridContext, CancellationToken.None));
    }

    /// <summary>
    /// Verifies batch publishing with null Grid context throws ArgumentNullException.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task PublishBatchAsync_NullGridContext_ThrowsArgumentNullException()
    {
        // Arrange
        var transportPublisher = Substitute.For<ITransportPublisher>();
        var serializer = new JsonMessageSerializer();
        var timeProvider = new TestTimeProvider(FixedTime);
        var factory = new EnvelopeFactory(timeProvider);
        var publisher = new MessagePublisher(transportPublisher, serializer, factory);

        var messages = new[] { new SampleMessage { Value = "test" } };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await publisher.PublishBatchAsync("test-queue", messages, null!, CancellationToken.None));
    }

    /// <summary>
    /// Verifies cancellation token is passed through to transport publisher.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task PublishAsync_PassesCancellationTokenToTransport()
    {
        // Arrange
        var transportPublisher = Substitute.For<ITransportPublisher>();
        var serializer = new JsonMessageSerializer();
        var timeProvider = new TestTimeProvider(FixedTime);
        var factory = new EnvelopeFactory(timeProvider);
        var publisher = new MessagePublisher(transportPublisher, serializer, factory);

        var message = new SampleMessage { Value = "test" };
        var gridContext = CreateTestGridContext();
        using var cts = new CancellationTokenSource();

        // Act
        await publisher.PublishAsync("test-queue", message, gridContext, cts.Token);

        // Assert
        await transportPublisher.Received(1).PublishAsync(
            Arg.Any<ITransportEnvelope>(),
            Arg.Any<IEndpointAddress>(),
            cts.Token);
    }

    /// <summary>
    /// Verifies cancellation token is passed through to transport publisher in batch mode.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task PublishBatchAsync_PassesCancellationTokenToTransport()
    {
        // Arrange
        var transportPublisher = Substitute.For<ITransportPublisher>();
        var serializer = new JsonMessageSerializer();
        var timeProvider = new TestTimeProvider(FixedTime);
        var factory = new EnvelopeFactory(timeProvider);
        var publisher = new MessagePublisher(transportPublisher, serializer, factory);

        var messages = new[] { new SampleMessage { Value = "test" } };
        var gridContext = CreateTestGridContext();
        using var cts = new CancellationTokenSource();

        // Act
        await publisher.PublishBatchAsync("test-queue", messages, gridContext, cts.Token);

        // Assert
        await transportPublisher.Received(1).PublishBatchAsync(
            Arg.Any<IEnumerable<ITransportEnvelope>>(),
            Arg.Any<IEndpointAddress>(),
            cts.Token);
    }

    /// <summary>
    /// Creates a test Grid context with default or custom values.
    /// </summary>
    private static IGridContext CreateTestGridContext(
        string correlationId = "test-correlation",
        string? causationId = "test-causation",
        string nodeId = "test-node",
        string studioId = "test-studio",
        string? tenantId = "test-tenant",
        string? projectId = "test-project",
        string environment = "test")
    {
        var context = Substitute.For<IGridContext>();
        context.CorrelationId.Returns(correlationId);
        context.CausationId.Returns(causationId);
        context.NodeId.Returns(nodeId);
        context.StudioId.Returns(studioId);
        context.TenantId.Returns(tenantId);
        context.ProjectId.Returns(projectId);
        context.Environment.Returns(environment);
        context.CreatedAtUtc.Returns(FixedTime);
        context.Baggage.Returns(new Dictionary<string, string>());
        return context;
    }

    /// <summary>
    /// Deserializes a payload back to SampleMessage for assertion.
    /// </summary>
    private static SampleMessage DeserializePayload(ReadOnlyMemory<byte> payload, JsonMessageSerializer serializer)
    {
        return serializer.Deserialize<SampleMessage>(payload);
    }

    /// <summary>
    /// Test time provider that returns a fixed time.
    /// </summary>
    private sealed class TestTimeProvider(DateTimeOffset fixedTime) : TimeProvider
    {
        private readonly DateTimeOffset _fixedTime = fixedTime;

        public override DateTimeOffset GetUtcNow() => _fixedTime;
    }
}
