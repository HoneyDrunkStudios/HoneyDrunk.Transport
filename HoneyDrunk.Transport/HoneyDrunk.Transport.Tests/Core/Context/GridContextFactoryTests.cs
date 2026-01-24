using HoneyDrunk.Kernel.Context;
using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.Primitives;

using TransportGridContextFactory = HoneyDrunk.Transport.Context.GridContextFactory;

namespace HoneyDrunk.Transport.Tests.Core.Context;

/// <summary>
/// Tests for Grid context factory.
/// </summary>
/// <remarks>
/// These tests verify the Kernel vNext pattern where the factory INITIALIZES
/// an existing DI-scoped GridContext rather than creating a new one.
/// </remarks>
public sealed class GridContextFactoryTests
{
    private const string TestNodeId = "test-node";
    private const string TestStudioId = "test-studio";
    private const string TestEnvironment = "test-env";

    /// <summary>
    /// Verifies factory initializes Grid context from envelope with all fields populated.
    /// </summary>
    [Fact]
    public void InitializeFromEnvelope_WithAllFields_InitializesGridContext()
    {
        // Arrange
        var factory = new TransportGridContextFactory();
        var gridContext = new GridContext(TestNodeId, TestStudioId, TestEnvironment);

        var envelope = new TransportEnvelope
        {
            MessageId = "msg-123",
            CorrelationId = "corr-456",
            CausationId = "cause-789",
            NodeId = "node-1",
            StudioId = "studio-1",
            TenantId = "tenant-1",
            ProjectId = "project-1",
            Environment = "production",
            Headers = new Dictionary<string, string>
            {
                ["key1"] = "value1",
                ["key2"] = "value2"
            },
            MessageType = "TestMessage",
            Payload = ReadOnlyMemory<byte>.Empty,
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        factory.InitializeFromEnvelope(gridContext, envelope, CancellationToken.None);

        // Assert
        Assert.True(gridContext.IsInitialized);
        Assert.Equal("corr-456", gridContext.CorrelationId);
        Assert.Equal("cause-789", gridContext.CausationId);
        Assert.Equal("tenant-1", gridContext.TenantId);
        Assert.Equal("project-1", gridContext.ProjectId);
        Assert.Equal(2, gridContext.Baggage.Count);
        Assert.Equal("value1", gridContext.Baggage["key1"]);
        Assert.Equal("value2", gridContext.Baggage["key2"]);
    }

    /// <summary>
    /// Verifies factory falls back to messageId when correlationId is missing.
    /// </summary>
    [Fact]
    public void InitializeFromEnvelope_WithMissingCorrelationId_FallsBackToMessageId()
    {
        // Arrange
        var factory = new TransportGridContextFactory();
        var gridContext = new GridContext(TestNodeId, TestStudioId, TestEnvironment);

        var envelope = new TransportEnvelope
        {
            MessageId = "msg-abc",
            CorrelationId = null, // Missing
            MessageType = "TestMessage",
            Payload = ReadOnlyMemory<byte>.Empty,
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        factory.InitializeFromEnvelope(gridContext, envelope, CancellationToken.None);

        // Assert
        Assert.Equal("msg-abc", gridContext.CorrelationId);
    }

    /// <summary>
    /// Verifies factory falls back to messageId when causationId is missing.
    /// </summary>
    [Fact]
    public void InitializeFromEnvelope_WithMissingCausationId_FallsBackToMessageId()
    {
        // Arrange
        var factory = new TransportGridContextFactory();
        var gridContext = new GridContext(TestNodeId, TestStudioId, TestEnvironment);

        var envelope = new TransportEnvelope
        {
            MessageId = "msg-xyz",
            CausationId = null, // Missing
            MessageType = "TestMessage",
            Payload = ReadOnlyMemory<byte>.Empty,
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        factory.InitializeFromEnvelope(gridContext, envelope, CancellationToken.None);

        // Assert
        Assert.Equal("msg-xyz", gridContext.CausationId);
    }

    /// <summary>
    /// Verifies factory creates empty baggage when headers are null.
    /// </summary>
    [Fact]
    public void InitializeFromEnvelope_WithNullHeaders_CreatesEmptyBaggage()
    {
        // Arrange
        var factory = new TransportGridContextFactory();
        var gridContext = new GridContext(TestNodeId, TestStudioId, TestEnvironment);

        IReadOnlyDictionary<string, string>? nullHeaders = null;
        var envelope = new TransportEnvelope
        {
            MessageId = "msg-123",
            CorrelationId = "corr-456",
            Headers = nullHeaders!, // No headers
            MessageType = "TestMessage",
            Payload = ReadOnlyMemory<byte>.Empty,
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        factory.InitializeFromEnvelope(gridContext, envelope, CancellationToken.None);

        // Assert
        Assert.NotNull(gridContext.Baggage);
        Assert.Empty(gridContext.Baggage);
    }

    /// <summary>
    /// Verifies factory propagates cancellation token to Grid context.
    /// </summary>
    [Fact]
    public void InitializeFromEnvelope_WithCancellationToken_PropagatesToken()
    {
        // Arrange
        var factory = new TransportGridContextFactory();
        var gridContext = new GridContext(TestNodeId, TestStudioId, TestEnvironment);
        using var cts = new CancellationTokenSource();

        var envelope = new TransportEnvelope
        {
            MessageId = "msg-123",
            MessageType = "TestMessage",
            Payload = ReadOnlyMemory<byte>.Empty,
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        factory.InitializeFromEnvelope(gridContext, envelope, cts.Token);

        // Assert
        Assert.Equal(cts.Token, gridContext.Cancellation);
    }

    /// <summary>
    /// Verifies factory throws when envelope is null.
    /// </summary>
    [Fact]
    public void InitializeFromEnvelope_WithNullEnvelope_ThrowsArgumentNullException()
    {
        // Arrange
        var factory = new TransportGridContextFactory();
        var gridContext = new GridContext(TestNodeId, TestStudioId, TestEnvironment);
        ITransportEnvelope? nullEnvelope = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            factory.InitializeFromEnvelope(gridContext, nullEnvelope!, CancellationToken.None));
    }

    /// <summary>
    /// Verifies factory throws when gridContext is null.
    /// </summary>
    [Fact]
    public void InitializeFromEnvelope_WithNullGridContext_ThrowsArgumentNullException()
    {
        // Arrange
        var factory = new TransportGridContextFactory();

        var envelope = new TransportEnvelope
        {
            MessageId = "msg-123",
            MessageType = "TestMessage",
            Payload = ReadOnlyMemory<byte>.Empty,
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            factory.InitializeFromEnvelope(null!, envelope, CancellationToken.None));
    }

    /// <summary>
    /// Verifies factory handles optional tenant and project IDs.
    /// </summary>
    [Fact]
    public void InitializeFromEnvelope_WithTenantAndProjectIds_SetsMultiTenantProperties()
    {
        // Arrange
        var factory = new TransportGridContextFactory();
        var gridContext = new GridContext(TestNodeId, TestStudioId, TestEnvironment);

        var envelope = new TransportEnvelope
        {
            MessageId = "msg-123",
            TenantId = "tenant-abc",
            ProjectId = "project-xyz",
            MessageType = "TestMessage",
            Payload = ReadOnlyMemory<byte>.Empty,
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        factory.InitializeFromEnvelope(gridContext, envelope, CancellationToken.None);

        // Assert
        Assert.Equal("tenant-abc", gridContext.TenantId);
        Assert.Equal("project-xyz", gridContext.ProjectId);
    }

    /// <summary>
    /// Verifies factory handles null tenant and project IDs gracefully.
    /// </summary>
    [Fact]
    public void InitializeFromEnvelope_WithNullTenantAndProject_SetsNullProperties()
    {
        // Arrange
        var factory = new TransportGridContextFactory();
        var gridContext = new GridContext(TestNodeId, TestStudioId, TestEnvironment);

        var envelope = new TransportEnvelope
        {
            MessageId = "msg-123",
            TenantId = null,
            ProjectId = null,
            MessageType = "TestMessage",
            Payload = ReadOnlyMemory<byte>.Empty,
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        factory.InitializeFromEnvelope(gridContext, envelope, CancellationToken.None);

        // Assert
        Assert.Null(gridContext.TenantId);
        Assert.Null(gridContext.ProjectId);
    }
}
