using HoneyDrunk.Kernel.Abstractions.Identity;
using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.Primitives;
using Microsoft.Extensions.Logging;

using TransportGridContextFactory = HoneyDrunk.Transport.Context.GridContextFactory;

namespace HoneyDrunk.Transport.Tests.Core.Context;

/// <summary>
/// Tests for Grid context factory.
/// </summary>
/// <remarks>
/// These tests verify that the factory creates initialized Kernel Abstractions context snapshots without referencing Kernel runtime types.
/// </remarks>
public sealed class GridContextFactoryTests
{
    /// <summary>
    /// Verifies factory initializes Grid context from envelope with all fields populated.
    /// </summary>
    [Fact]
    public void CreateFromEnvelope_WithAllFields_CreatesGridContext()
    {
        // Arrange
        var factory = new TransportGridContextFactory();
        var envelope = new TransportEnvelope
        {
            MessageId = "msg-123",
            CorrelationId = "corr-456",
            CausationId = "cause-789",
            NodeId = "node-1",
            StudioId = "studio-1",
            TenantId = "01ARZ3NDEKTSV4RRFFQ69G5FAV",
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
        var gridContext = factory.CreateFromEnvelope(envelope, CancellationToken.None);

        // Assert
        Assert.True(gridContext.IsInitialized);
        Assert.Equal("corr-456", gridContext.CorrelationId);
        Assert.Equal("cause-789", gridContext.CausationId);
        Assert.Equal("01ARZ3NDEKTSV4RRFFQ69G5FAV", gridContext.TenantId.ToString());
        Assert.Equal("project-1", gridContext.ProjectId);
        Assert.Equal(2, gridContext.Baggage.Count);
        Assert.Equal("value1", gridContext.Baggage["key1"]);
        Assert.Equal("value2", gridContext.Baggage["key2"]);
    }

    /// <summary>
    /// Verifies factory falls back to messageId when correlationId is missing.
    /// </summary>
    [Fact]
    public void CreateFromEnvelope_WithMissingCorrelationId_FallsBackToMessageId()
    {
        // Arrange
        var factory = new TransportGridContextFactory();
        var envelope = new TransportEnvelope
        {
            MessageId = "msg-abc",
            CorrelationId = null, // Missing
            MessageType = "TestMessage",
            Payload = ReadOnlyMemory<byte>.Empty,
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        var gridContext = factory.CreateFromEnvelope(envelope, CancellationToken.None);

        // Assert
        Assert.Equal("msg-abc", gridContext.CorrelationId);
    }

    /// <summary>
    /// Verifies factory falls back to messageId when causationId is missing.
    /// </summary>
    [Fact]
    public void CreateFromEnvelope_WithMissingCausationId_FallsBackToMessageId()
    {
        // Arrange
        var factory = new TransportGridContextFactory();
        var envelope = new TransportEnvelope
        {
            MessageId = "msg-xyz",
            CausationId = null, // Missing
            MessageType = "TestMessage",
            Payload = ReadOnlyMemory<byte>.Empty,
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        var gridContext = factory.CreateFromEnvelope(envelope, CancellationToken.None);

        // Assert
        Assert.Equal("msg-xyz", gridContext.CausationId);
    }

    /// <summary>
    /// Verifies factory creates empty baggage when headers are null.
    /// </summary>
    [Fact]
    public void CreateFromEnvelope_WithNullHeaders_CreatesEmptyBaggage()
    {
        // Arrange
        var factory = new TransportGridContextFactory();
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
        var gridContext = factory.CreateFromEnvelope(envelope, CancellationToken.None);

        // Assert
        Assert.NotNull(gridContext.Baggage);
        Assert.Empty(gridContext.Baggage);
    }

    /// <summary>
    /// Verifies factory propagates cancellation token to Grid context.
    /// </summary>
    [Fact]
    public void CreateFromEnvelope_WithCancellationToken_PropagatesToken()
    {
        // Arrange
        var factory = new TransportGridContextFactory();
        using var cts = new CancellationTokenSource();

        var envelope = new TransportEnvelope
        {
            MessageId = "msg-123",
            MessageType = "TestMessage",
            Payload = ReadOnlyMemory<byte>.Empty,
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        var gridContext = factory.CreateFromEnvelope(envelope, cts.Token);

        // Assert
        Assert.Equal(cts.Token, gridContext.Cancellation);
    }

    /// <summary>
    /// Verifies factory throws when envelope is null.
    /// </summary>
    [Fact]
    public void CreateFromEnvelope_WithNullEnvelope_ThrowsArgumentNullException()
    {
        // Arrange
        var factory = new TransportGridContextFactory();
        ITransportEnvelope? nullEnvelope = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            factory.CreateFromEnvelope(nullEnvelope!, CancellationToken.None));
    }

    /// <summary>
    /// Verifies factory handles optional tenant and project IDs.
    /// </summary>
    [Fact]
    public void CreateFromEnvelope_WithTenantAndProjectIds_SetsMultiTenantProperties()
    {
        // Arrange
        var factory = new TransportGridContextFactory();
        var envelope = new TransportEnvelope
        {
            MessageId = "msg-123",
            TenantId = "01BX5ZZKBKACTAV9WEVGEMMVRZ",
            ProjectId = "project-xyz",
            MessageType = "TestMessage",
            Payload = ReadOnlyMemory<byte>.Empty,
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        var gridContext = factory.CreateFromEnvelope(envelope, CancellationToken.None);

        // Assert
        Assert.Equal("01BX5ZZKBKACTAV9WEVGEMMVRZ", gridContext.TenantId.ToString());
        Assert.Equal("project-xyz", gridContext.ProjectId);
    }

    /// <summary>
    /// Verifies factory handles null tenant and project IDs gracefully.
    /// </summary>
    [Fact]
    public void CreateFromEnvelope_WithNullTenantAndProject_UsesInternalTenant()
    {
        // Arrange
        var factory = new TransportGridContextFactory();
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
        var gridContext = factory.CreateFromEnvelope(envelope, CancellationToken.None);

        // Assert
        Assert.Equal(TenantId.Internal, gridContext.TenantId);
        Assert.Null(gridContext.ProjectId);
    }

    /// <summary>
    /// Verifies factory parses valid ULID tenant IDs.
    /// </summary>
    [Fact]
    public void CreateFromEnvelope_WithValidUlidTenant_ParsesTenantId()
    {
        // Arrange
        var factory = new TransportGridContextFactory();
        var tenantId = TenantId.NewId();

        var envelope = new TransportEnvelope
        {
            MessageId = "msg-valid-tenant",
            TenantId = tenantId.ToString(),
            MessageType = "TestMessage",
            Payload = ReadOnlyMemory<byte>.Empty,
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        var gridContext = factory.CreateFromEnvelope(envelope, CancellationToken.None);

        // Assert
        Assert.Equal(tenantId, gridContext.TenantId);
    }

    /// <summary>
    /// Verifies malformed tenant IDs fall back to Internal and log without leaking the raw value in the template.
    /// </summary>
    [Fact]
    public void CreateFromEnvelope_WithMalformedTenant_UsesInternalTenantAndLogsWarning()
    {
        // Arrange
        var logger = new CapturingLogger();
        var factory = new TransportGridContextFactory(logger);
        const string malformedTenant = "not-a-tenant-ulid";

        var envelope = new TransportEnvelope
        {
            MessageId = "msg-bad-tenant",
            NodeId = "node-1",
            StudioId = "studio-1",
            Environment = "production",
            TenantId = malformedTenant,
            MessageType = "TestMessage",
            Payload = ReadOnlyMemory<byte>.Empty,
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        var gridContext = factory.CreateFromEnvelope(envelope, CancellationToken.None);

        // Assert
        Assert.Equal(TenantId.Internal, gridContext.TenantId);
        var warning = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, warning.Level);
        Assert.Contains("{MessageId}", warning.Template, StringComparison.Ordinal);
        Assert.DoesNotContain(malformedTenant, warning.Template, StringComparison.Ordinal);
        Assert.DoesNotContain(malformedTenant, warning.Message, StringComparison.Ordinal);
        Assert.Equal("msg-bad-tenant", warning.Properties["MessageId"]);
    }

    /// <summary>
    /// Verifies the serialized Internal sentinel maps back to Internal.
    /// </summary>
    [Fact]
    public void CreateFromEnvelope_WithInternalTenantString_UsesInternalTenant()
    {
        // Arrange
        var factory = new TransportGridContextFactory();
        var envelope = new TransportEnvelope
        {
            MessageId = "msg-internal-tenant",
            TenantId = TenantId.Internal.ToString(),
            MessageType = "TestMessage",
            Payload = ReadOnlyMemory<byte>.Empty,
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        var gridContext = factory.CreateFromEnvelope(envelope, CancellationToken.None);

        // Assert
        Assert.Equal(TenantId.Internal, gridContext.TenantId);
    }

    private sealed class CapturingLogger : ILogger<TransportGridContextFactory>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var properties = state as IReadOnlyList<KeyValuePair<string, object?>>;
            var template = properties?.FirstOrDefault(p => p.Key == "{OriginalFormat}").Value?.ToString()
                ?? formatter(state, exception);

            var logProperties = properties?.Where(p => p.Key != "{OriginalFormat}")
                .ToDictionary(p => p.Key, p => p.Value) ?? [];

            Entries.Add(new LogEntry(
                logLevel,
                template,
                formatter(state, exception),
                logProperties));
        }
    }

    private sealed record LogEntry(
        LogLevel Level,
        string Template,
        string Message,
        IReadOnlyDictionary<string, object?> Properties);
}
