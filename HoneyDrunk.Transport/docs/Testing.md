# 🧪 Testing - Test Patterns and Best Practices

[← Back to File Guide](FILE_GUIDE.md)

---

## Table of Contents

- [Overview](#overview)
- [Testing Philosophy](#testing-philosophy)
- [Unit Testing Patterns](#unit-testing-patterns)
  - [Testing Message Handlers](#testing-message-handlers)
  - [Testing Middleware](#testing-middleware)
- [Integration Testing Patterns](#integration-testing-patterns)
  - [Shared Test Fixture](#shared-test-fixture)
  - [Pipeline-Level Tests](#pipeline-level-tests)
  - [Runtime-Level Tests](#runtime-level-tests)
  - [Grid Context Propagation](#grid-context-propagation)
  - [Retry Behavior](#retry-behavior)
- [Outbox Testing](#outbox-testing)
- [Test Helpers](#test-helpers)
  - [TestEnvelopeFactory](#testenvelopefactory)
  - [TestGridContext](#testgridcontext)
  - [FakeTimeProvider](#faketimeprovider)
  - [TestMessageContext](#testmessagecontext)

---

## Overview

Testing patterns for Transport applications using InMemory transport, mocking strategies, and integration test patterns.

**Location:** `HoneyDrunk.Transport.Tests/`

**Key Concepts:**
- **Unit tests** are pure and independent—no DI container, no runtime
- **Integration tests** either drive `IMessagePipeline` directly or start `ITransportRuntime`
- **Never mutate a built `ServiceProvider`**—rebuild for each test or use a shared fixture as read-only

---

## Testing Philosophy

Transport testing follows two distinct patterns:

| Pattern | When to Use | What's Involved |
|---------|-------------|-----------------|
| **Unit Tests** | Testing handlers, middleware, serializers in isolation | Mocks, no DI, `NoOpTransportTransaction.Instance` |
| **Integration Tests** | Testing pipeline flow, Grid context propagation, retry behavior | Build `ServiceProvider` per test, or use shared fixture + `IMessagePipeline` / `ITransportRuntime` |

> **Critical Rule:** You cannot add services to an already-built `ServiceProvider`. It's a one-way door. Either:
> - Use a **shared fixture** as read-only (all wiring done in constructor)
> - **Build a fresh provider** for each test that needs custom wiring

[↑ Back to top](#table-of-contents)

---

## Unit Testing Patterns

Unit tests should be **pure**—no DI container, no runtime, no Kernel registration required.

### Testing Message Handlers

```csharp
public class OrderCreatedHandlerTests
{
    [Fact]
    public async Task HandleAsync_ValidMessage_ReturnsSuccess()
    {
        // Arrange - pure unit test, no DI
        var repository = new Mock<IOrderRepository>();
        var handler = new OrderCreatedHandler(repository.Object);
        
        var message = new OrderCreated(OrderId: 123, CustomerId: 456);
        var context = new MessageContext
        {
            Envelope = TestEnvelopeFactory.CreateEnvelope(message),
            Transaction = NoOpTransportTransaction.Instance,  // Singleton
            DeliveryCount = 1
        };
        
        // Act
        var result = await handler.HandleAsync(message, context, CancellationToken.None);
        
        // Assert
        Assert.Equal(MessageProcessingResult.Success, result);
        repository.Verify(r => r.ProcessOrderAsync(123, It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [Fact]
    public async Task HandleAsync_RepositoryThrows_ReturnsRetry()
    {
        // Arrange
        var repository = new Mock<IOrderRepository>();
        repository
            .Setup(r => r.ProcessOrderAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException());
        
        var handler = new OrderCreatedHandler(repository.Object);
        var message = new OrderCreated(123, 456);
        var context = TestMessageContext.Create(message);
        
        // Act
        var result = await handler.HandleAsync(message, context, CancellationToken.None);
        
        // Assert
        Assert.Equal(MessageProcessingResult.Retry, result);
    }
    
    [Fact]
    public async Task HandleAsync_ValidationFails_ReturnsDeadLetter()
    {
        // Arrange
        var repository = new Mock<IOrderRepository>();
        var handler = new OrderCreatedHandler(repository.Object);
        var invalidMessage = new OrderCreated(OrderId: -1, CustomerId: 456);
        var context = TestMessageContext.Create(invalidMessage);
        
        // Act
        var result = await handler.HandleAsync(invalidMessage, context, CancellationToken.None);
        
        // Assert
        Assert.Equal(MessageProcessingResult.DeadLetter, result);
        repository.Verify(r => r.ProcessOrderAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
```

> **Note:** Unit tests don't need `IGridContext` or Kernel. The `MessageContext.GridContext` property is nullable and populated by middleware during pipeline execution.

[↑ Back to top](#table-of-contents)

---

### Testing Middleware

```csharp
public class ValidationMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_ValidMessage_CallsNext()
    {
        // Arrange
        var validator = new Mock<IValidator>();
        validator.Setup(v => v.ValidateAsync(It.IsAny<object>())).ReturnsAsync(true);
        
        var middleware = new ValidationMiddleware(validator.Object);
        var envelope = TestEnvelopeFactory.CreateEnvelope(new OrderCreated(123, 456));
        var context = TestMessageContext.Create<OrderCreated>();
        var nextCalled = false;
        
        // Act
        await middleware.InvokeAsync(envelope, context, async () =>
        {
            nextCalled = true;
            await Task.CompletedTask;
        }, CancellationToken.None);
        
        // Assert
        Assert.True(nextCalled);
    }
    
    [Fact]
    public async Task InvokeAsync_InvalidMessage_ThrowsWithoutCallingNext()
    {
        // Arrange
        var validator = new Mock<IValidator>();
        validator.Setup(v => v.ValidateAsync(It.IsAny<object>())).ReturnsAsync(false);
        
        var middleware = new ValidationMiddleware(validator.Object);
        var envelope = TestEnvelopeFactory.CreateEnvelope(new OrderCreated(123, 456));
        var context = TestMessageContext.Create<OrderCreated>();
        var nextCalled = false;
        
        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(async () =>
            await middleware.InvokeAsync(
                envelope,
                context,
                async () => { nextCalled = true; await Task.CompletedTask; },
                CancellationToken.None));
        
        Assert.False(nextCalled);
    }
}
```

[↑ Back to top](#table-of-contents)

---

## Integration Testing Patterns

Integration tests validate that components work together through the Transport pipeline.

### Shared Test Fixture

A shared fixture provides a **ready-to-go** publisher, broker, and pipeline for integration tests. **Do not mutate it after construction.**

```csharp
public class TransportTestFixture : IAsyncLifetime
{
    public ServiceProvider Services { get; private set; } = null!;
    public ITransportPublisher Publisher { get; private set; } = null!;
    public IMessagePipeline Pipeline { get; private set; } = null!;
    public InMemoryBroker Broker { get; private set; } = null!;
    public EnvelopeFactory EnvelopeFactory { get; private set; } = null!;
    public IMessageSerializer Serializer { get; private set; } = null!;
    
    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();
        
        // Register Kernel
        services.AddHoneyDrunkCoreNode(new NodeDescriptor
        {
            NodeId = "test-node",
            Version = "1.0.0",
            Name = "Test Node"
        });
        
        // Register InMemory transport
        services.AddHoneyDrunkTransportCore(options =>
        {
            options.EnableTelemetry = false;
            options.EnableLogging = true;
            options.EnableCorrelation = true;
        })
        .AddHoneyDrunkInMemoryTransport();
        
        // Register handlers that are shared across all tests
        services.AddMessageHandler<OrderCreated, OrderCreatedHandler>();
        services.AddMessageHandler<OrderUpdated, OrderUpdatedHandler>();
        
        Services = services.BuildServiceProvider();
        Publisher = Services.GetRequiredService<ITransportPublisher>();
        Pipeline = Services.GetRequiredService<IMessagePipeline>();
        Broker = Services.GetRequiredService<InMemoryBroker>();
        EnvelopeFactory = Services.GetRequiredService<EnvelopeFactory>();
        Serializer = Services.GetRequiredService<IMessageSerializer>();
    }
    
    public async Task DisposeAsync()
    {
        await Services.DisposeAsync();
    }
}

// Use in tests - fixture is READ-ONLY
public class OrderTests : IClassFixture<TransportTestFixture>
{
    private readonly TransportTestFixture _fixture;
    
    public OrderTests(TransportTestFixture fixture)
    {
        _fixture = fixture;
    }
}
```

> **Key Point:** The fixture is built once and shared. If a test needs custom handler wiring, it should build its own `ServiceProvider`.

[↑ Back to top](#table-of-contents)

---

### Pipeline-Level Tests

For testing message processing through the full pipeline (middleware → handler), call `IMessagePipeline.ProcessAsync()` directly.

```csharp
public class PipelineIntegrationTests : IClassFixture<TransportTestFixture>
{
    private readonly TransportTestFixture _fixture;
    
    public PipelineIntegrationTests(TransportTestFixture fixture)
    {
        _fixture = fixture;
    }
    
    [Fact]
    public async Task ProcessAsync_ValidMessage_InvokesHandlerAndReturnsSuccess()
    {
        // Arrange
        var message = new OrderCreated(123, 456);
        var payload = _fixture.Serializer.Serialize(message);
        var envelope = _fixture.EnvelopeFactory.CreateEnvelope<OrderCreated>(payload);
        
        var context = new MessageContext
        {
            Envelope = envelope,
            Transaction = NoOpTransportTransaction.Instance,
            DeliveryCount = 1
        };
        
        // Act - drive pipeline directly
        var result = await _fixture.Pipeline.ProcessAsync(envelope, context, CancellationToken.None);
        
        // Assert
        Assert.Equal(MessageProcessingResult.Success, result);
    }
}
```

[↑ Back to top](#table-of-contents)

---

### Runtime-Level Tests

For true end-to-end tests with consumers running, start `ITransportRuntime`. This is useful for testing the full flow from publish → broker → consumer → pipeline → handler.

```csharp
public class RuntimeIntegrationTests
{
    [Fact]
    public async Task Publish_OrderCreated_InvokesHandlerThroughRuntime()
    {
        // Arrange - build a fresh provider for this test
        var handlerInvoked = new TaskCompletionSource<OrderCreated>();
        
        var services = new ServiceCollection();
        services.AddHoneyDrunkCoreNode(new NodeDescriptor
        {
            NodeId = "test-node",
            Version = "1.0.0"
        });
        
        services.AddHoneyDrunkTransportCore(options =>
        {
            options.EnableCorrelation = true;
        })
        .AddHoneyDrunkInMemoryTransport();
        
        // Register handler that signals completion
        services.AddMessageHandler<OrderCreated>(async (msg, ctx, ct) =>
        {
            handlerInvoked.SetResult(msg);
            return MessageProcessingResult.Success;
        });
        
        await using var provider = services.BuildServiceProvider();
        
        var runtime = provider.GetRequiredService<ITransportRuntime>();
        var publisher = provider.GetRequiredService<ITransportPublisher>();
        var factory = provider.GetRequiredService<EnvelopeFactory>();
        var serializer = provider.GetRequiredService<IMessageSerializer>();
        
        // Act - start runtime so consumers are active
        await runtime.StartAsync(CancellationToken.None);
        
        try
        {
            var message = new OrderCreated(123, 456);
            var payload = serializer.Serialize(message);
            var envelope = factory.CreateEnvelope<OrderCreated>(payload);
            
            await publisher.PublishAsync(
                envelope,
                EndpointAddress.Create("orders", "orders"),
                CancellationToken.None);
            
            // Assert - wait for handler to be invoked
            var receivedMessage = await handlerInvoked.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(123, receivedMessage.OrderId);
            Assert.Equal(456, receivedMessage.CustomerId);
        }
        finally
        {
            await runtime.StopAsync(CancellationToken.None);
        }
    }
}
```

> **Note:** Runtime tests are slower but test the complete flow including `InMemoryTransportConsumer` picking up messages from `InMemoryBroker`.

[↑ Back to top](#table-of-contents)

---

### Grid Context Propagation

Test that `GridContextPropagationMiddleware` correctly populates `MessageContext.GridContext` from envelope metadata.

```csharp
[Fact]
public async Task GridContext_PropagatesThroughPipeline()
{
    // Arrange - build fresh provider for custom handler
    IGridContext? receivedContext = null;
    
    var services = new ServiceCollection();
    services.AddHoneyDrunkCoreNode(new NodeDescriptor
    {
        NodeId = "test-node",
        Version = "1.0.0"
    });
    
    services.AddHoneyDrunkTransportCore(options =>
    {
        options.EnableCorrelation = true;
    })
    .AddHoneyDrunkInMemoryTransport();
    
    services.AddMessageHandler<OrderCreated>(async (msg, ctx, ct) =>
    {
        receivedContext = ctx.GridContext;
        return MessageProcessingResult.Success;
    });
    
    await using var provider = services.BuildServiceProvider();
    
    var pipeline = provider.GetRequiredService<IMessagePipeline>();
    var factory = provider.GetRequiredService<EnvelopeFactory>();
    var serializer = provider.GetRequiredService<IMessageSerializer>();
    
    // Create envelope with Grid context
    var gridContext = new TestGridContext
    {
        CorrelationId = "test-correlation-123",
        StudioId = "test-studio",
        NodeId = "source-node",
        TenantId = "tenant-abc",
        ProjectId = "project-xyz"
    };
    
    var payload = serializer.Serialize(new OrderCreated(123, 456));
    var envelope = factory.CreateEnvelopeWithGridContext<OrderCreated>(payload, gridContext);
    
    var context = new MessageContext
    {
        Envelope = envelope,
        Transaction = NoOpTransportTransaction.Instance,
        DeliveryCount = 1
    };
    
    // Act - process through pipeline
    var result = await pipeline.ProcessAsync(envelope, context, CancellationToken.None);
    
    // Assert
    Assert.Equal(MessageProcessingResult.Success, result);
    Assert.NotNull(receivedContext);
    Assert.Equal("test-correlation-123", receivedContext!.CorrelationId);
    Assert.Equal("test-studio", receivedContext.StudioId);
    Assert.Equal("tenant-abc", receivedContext.TenantId);
    Assert.Equal("project-xyz", receivedContext.ProjectId);
}
```

[↑ Back to top](#table-of-contents)

---

### Retry Behavior

Test retry logic by driving the pipeline directly and counting handler invocations. Use `FakeTimeProvider` to control time for backoff assertions.

```csharp
[Fact]
public async Task RetryMiddleware_TransientError_RetriesUpToMaxAttempts()
{
    // Arrange
    var attemptCount = 0;
    
    var services = new ServiceCollection();
    services.AddHoneyDrunkCoreNode(new NodeDescriptor { NodeId = "test" });
    
    services.AddHoneyDrunkTransportCore()
        .AddHoneyDrunkInMemoryTransport()
        .WithRetry(retry =>
        {
            retry.MaxAttempts = 3;
            retry.BackoffStrategy = BackoffStrategy.Fixed;
            retry.InitialDelay = TimeSpan.Zero;  // No delay for fast tests
        });
    
    services.AddMessageHandler<OrderCreated>(async (msg, ctx, ct) =>
    {
        attemptCount++;
        if (attemptCount < 3)
            return MessageProcessingResult.Retry;
        return MessageProcessingResult.Success;
    });
    
    await using var provider = services.BuildServiceProvider();
    var pipeline = provider.GetRequiredService<IMessagePipeline>();
    var factory = provider.GetRequiredService<EnvelopeFactory>();
    var serializer = provider.GetRequiredService<IMessageSerializer>();
    
    var payload = serializer.Serialize(new OrderCreated(123, 456));
    var envelope = factory.CreateEnvelope<OrderCreated>(payload);
    var context = new MessageContext
    {
        Envelope = envelope,
        Transaction = NoOpTransportTransaction.Instance,
        DeliveryCount = 1
    };
    
    // Act
    var result = await pipeline.ProcessAsync(envelope, context, CancellationToken.None);
    
    // Assert
    Assert.Equal(3, attemptCount);
    Assert.Equal(MessageProcessingResult.Success, result);
}

[Fact]
public async Task RetryMiddleware_ExceedsMaxAttempts_ReturnsDeadLetter()
{
    // Arrange
    var attemptCount = 0;
    
    var services = new ServiceCollection();
    services.AddHoneyDrunkCoreNode(new NodeDescriptor { NodeId = "test" });
    
    services.AddHoneyDrunkTransportCore()
        .AddHoneyDrunkInMemoryTransport()
        .WithRetry(retry =>
        {
            retry.MaxAttempts = 3;
            retry.InitialDelay = TimeSpan.Zero;
        });
    
    services.AddMessageHandler<OrderCreated>(async (msg, ctx, ct) =>
    {
        attemptCount++;
        return MessageProcessingResult.Retry;  // Always retry
    });
    
    await using var provider = services.BuildServiceProvider();
    var pipeline = provider.GetRequiredService<IMessagePipeline>();
    var factory = provider.GetRequiredService<EnvelopeFactory>();
    var serializer = provider.GetRequiredService<IMessageSerializer>();
    
    var payload = serializer.Serialize(new OrderCreated(123, 456));
    var envelope = factory.CreateEnvelope<OrderCreated>(payload);
    var context = new MessageContext
    {
        Envelope = envelope,
        Transaction = NoOpTransportTransaction.Instance,
        DeliveryCount = 1
    };
    
    // Act
    var result = await pipeline.ProcessAsync(envelope, context, CancellationToken.None);
    
    // Assert
    Assert.Equal(3, attemptCount);
    Assert.Equal(MessageProcessingResult.DeadLetter, result);
}
```

> **Tip:** For testing actual backoff timing, use `FakeTimeProvider` and inject it into the retry middleware. Avoid wall-clock timing assertions in tests.

[↑ Back to top](#table-of-contents)

---

## Outbox Testing

Test outbox behavior by verifying transaction semantics and dispatch logic.

```csharp
public class OutboxIntegrationTests
{
    [Fact]
    public async Task Outbox_TransactionRollback_MessageNotPersisted()
    {
        // Arrange
        using var db = CreateTestDatabase();
        var outbox = new EfCoreOutboxStore(db);
        
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Destination = EndpointAddress.Create("orders", "orders"),
            Envelope = TestEnvelopeFactory.CreateEnvelope(new OrderCreated(123, 456)),
            CreatedAt = DateTimeOffset.UtcNow
        };
        
        // Act - Rollback transaction
        using (var transaction = await db.Database.BeginTransactionAsync())
        {
            await outbox.SaveAsync(message.Destination, message.Envelope, CancellationToken.None);
            await transaction.RollbackAsync();
        }
        
        // Assert - Message not in outbox
        var pending = await outbox.LoadPendingAsync(10, CancellationToken.None);
        Assert.Empty(pending);
    }
    
    [Fact]
    public async Task Outbox_TransactionCommit_MessagePersisted()
    {
        // Arrange
        using var db = CreateTestDatabase();
        var outbox = new EfCoreOutboxStore(db);
        
        var destination = EndpointAddress.Create("orders", "orders");
        var envelope = TestEnvelopeFactory.CreateEnvelope(new OrderCreated(123, 456));
        
        // Act - Commit transaction
        using (var transaction = await db.Database.BeginTransactionAsync())
        {
            await outbox.SaveAsync(destination, envelope, CancellationToken.None);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        
        // Assert - Message in outbox
        var pending = await outbox.LoadPendingAsync(10, CancellationToken.None);
        Assert.Single(pending);
        Assert.Equal("orders", pending[0].Destination.Name);
    }
    
    [Fact]
    public async Task OutboxDispatcher_DispatchesPendingMessages()
    {
        // Arrange
        using var db = CreateTestDatabase();
        var outbox = new EfCoreOutboxStore(db);
        var publisher = new Mock<ITransportPublisher>();
        var logger = NullLogger<DefaultOutboxDispatcher>.Instance;
        var options = Options.Create(new OutboxDispatcherOptions
        {
            BatchSize = 10,
            DispatchInterval = TimeSpan.FromSeconds(1)
        });
        
        // Create dispatcher directly (not as hosted service)
        var dispatcher = new DefaultOutboxDispatcher(outbox, publisher.Object, options, logger);
        
        // Save a message
        var destination = EndpointAddress.Create("orders", "orders");
        var envelope = TestEnvelopeFactory.CreateEnvelope(new OrderCreated(123, 456));
        await outbox.SaveAsync(destination, envelope, CancellationToken.None);
        await db.SaveChangesAsync();
        
        // Act - dispatch pending
        await dispatcher.DispatchPendingAsync(CancellationToken.None);
        
        // Assert
        publisher.Verify(p => p.PublishAsync(
            It.IsAny<ITransportEnvelope>(),
            It.Is<IEndpointAddress>(d => d.Name == "orders"),
            It.IsAny<PublishOptions?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

> **Note:** `DefaultOutboxDispatcher` exposes `DispatchPendingAsync()` for testing. In production it runs as a `BackgroundService` via `ExecuteAsync()`.

[↑ Back to top](#table-of-contents)

---

## Test Helpers

Reusable helpers for Transport tests.

### TestEnvelopeFactory

Creates envelopes for unit tests without requiring a DI container.

```csharp
public static class TestEnvelopeFactory
{
    private static readonly IMessageSerializer Serializer = new JsonMessageSerializer();
    private static readonly EnvelopeFactory Factory = new(TimeProvider.System);
    
    /// <summary>
    /// Creates an envelope for unit tests. For integration tests, prefer
    /// resolving EnvelopeFactory and IMessageSerializer from DI.
    /// </summary>
    public static ITransportEnvelope CreateEnvelope<TMessage>(TMessage message)
        where TMessage : class
    {
        var payload = Serializer.Serialize(message);
        return Factory.CreateEnvelope<TMessage>(payload);
    }
    
    public static ITransportEnvelope CreateEnvelopeWithGridContext<TMessage>(
        TMessage message,
        IGridContext gridContext)
        where TMessage : class
    {
        var payload = Serializer.Serialize(message);
        return Factory.CreateEnvelopeWithGridContext<TMessage>(payload, gridContext);
    }
    
    public static ITransportEnvelope CreateEnvelopeWithTimestamp<TMessage>(
        TMessage message,
        DateTimeOffset timestamp)
        where TMessage : class
    {
        var timeProvider = new FakeTimeProvider(timestamp);
        var factory = new EnvelopeFactory(timeProvider);
        var payload = Serializer.Serialize(message);
        return factory.CreateEnvelope<TMessage>(payload);
    }
}
```

> **Integration Tests:** For integration tests, prefer resolving `IMessageSerializer` and `EnvelopeFactory` from DI to match production wiring.

[↑ Back to top](#table-of-contents)

---

### TestGridContext

Minimal `IGridContext` implementation for tests.

```csharp
/// <summary>
/// Minimal IGridContext fake for tests. CreateChildContext and WithBaggage
/// return `this` for simplicity - not representative of production behavior.
/// </summary>
public class TestGridContext : IGridContext
{
    public string CorrelationId { get; set; } = "test-correlation";
    public string? CausationId { get; set; }
    public string NodeId { get; set; } = "test-node";
    public string StudioId { get; set; } = "test-studio";
    public string? TenantId { get; set; }
    public string? ProjectId { get; set; }
    public string Environment { get; set; } = "test";
    public IReadOnlyDictionary<string, string> Baggage { get; set; } 
        = new Dictionary<string, string>();
    public CancellationToken Cancellation { get; set; } = CancellationToken.None;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    
    public IDisposable BeginScope() => new NoOpScope();
    
    // Simplified for tests - production creates actual child contexts
    public IGridContext CreateChildContext(string? nodeId = null) => this;
    public IGridContext WithBaggage(string key, string value) => this;
    
    private sealed class NoOpScope : IDisposable
    {
        public void Dispose() { }
    }
}
```

[↑ Back to top](#table-of-contents)

---

### FakeTimeProvider

Controllable `TimeProvider` for deterministic timestamp and backoff testing.

```csharp
public class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset _current;
    
    public FakeTimeProvider(DateTimeOffset start)
    {
        _current = start;
    }
    
    public override DateTimeOffset GetUtcNow() => _current;
    
    public void Advance(TimeSpan duration)
    {
        _current = _current.Add(duration);
    }
    
    public void SetTime(DateTimeOffset time)
    {
        _current = time;
    }
}

// Usage with EnvelopeFactory
[Fact]
public void EnvelopeFactory_UsesFakeTimeProvider()
{
    var timeProvider = new FakeTimeProvider(
        new DateTimeOffset(2025, 1, 15, 10, 0, 0, TimeSpan.Zero));
    var factory = new EnvelopeFactory(timeProvider);
    var serializer = new JsonMessageSerializer();
    
    var payload = serializer.Serialize(new OrderCreated(123, 456));
    var envelope1 = factory.CreateEnvelope<OrderCreated>(payload);
    
    Assert.Equal(
        new DateTimeOffset(2025, 1, 15, 10, 0, 0, TimeSpan.Zero),
        envelope1.Timestamp);
    
    timeProvider.Advance(TimeSpan.FromHours(1));
    var envelope2 = factory.CreateEnvelope<OrderCreated>(payload);
    
    Assert.Equal(
        new DateTimeOffset(2025, 1, 15, 11, 0, 0, TimeSpan.Zero),
        envelope2.Timestamp);
}
```

[↑ Back to top](#table-of-contents)

---

### TestMessageContext

Factory for creating `MessageContext` in tests.

```csharp
public static class TestMessageContext
{
    public static MessageContext Create<TMessage>(TMessage? message = null)
        where TMessage : class, new()
    {
        var actualMessage = message ?? new TMessage();
        return new MessageContext
        {
            Envelope = TestEnvelopeFactory.CreateEnvelope(actualMessage),
            Transaction = NoOpTransportTransaction.Instance,
            DeliveryCount = 1
        };
    }
    
    public static MessageContext CreateWithGridContext<TMessage>(
        TMessage message,
        IGridContext gridContext)
        where TMessage : class
    {
        return new MessageContext
        {
            Envelope = TestEnvelopeFactory.CreateEnvelopeWithGridContext(message, gridContext),
            Transaction = NoOpTransportTransaction.Instance,
            GridContext = gridContext,
            DeliveryCount = 1
        };
    }
    
    public static MessageContext CreateWithDeliveryCount<TMessage>(
        TMessage message,
        int deliveryCount)
        where TMessage : class
    {
        return new MessageContext
        {
            Envelope = TestEnvelopeFactory.CreateEnvelope(message),
            Transaction = NoOpTransportTransaction.Instance,
            DeliveryCount = deliveryCount
        };
    }
}
```

[↑ Back to top](#table-of-contents)

---

[← Back to File Guide](FILE_GUIDE.md) | [↑ Back to top](#table-of-contents)
