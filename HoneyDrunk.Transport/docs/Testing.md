# 🧪 Testing - Test Patterns and Best Practices

[← Back to File Guide](FILE_GUIDE.md)

---

## Overview

Testing patterns for Transport applications using InMemory transport, mocking strategies, and integration test patterns.

**Location:** `HoneyDrunk.Transport.Tests/`

---

## Test Setup

### Basic Test Configuration

```csharp
public class TransportTestFixture : IDisposable
{
    public ServiceProvider Services { get; }
    public ITransportPublisher Publisher { get; }
    public InMemoryBroker Broker { get; }
    
    public TransportTestFixture()
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
        })
        .AddHoneyDrunkInMemoryTransport();
        
        // Register test handlers
        services.AddMessageHandler<OrderCreated, OrderCreatedHandler>();
        
        Services = services.BuildServiceProvider();
        Publisher = Services.GetRequiredService<ITransportPublisher>();
        Broker = Services.GetRequiredService<InMemoryBroker>();
    }
    
    public void Dispose()
    {
        Services?.Dispose();
    }
}

// Use in tests
public class OrderTests : IClassFixture<TransportTestFixture>
{
    private readonly TransportTestFixture _fixture;
    
    public OrderTests(TransportTestFixture fixture)
    {
        _fixture = fixture;
    }
}
```

---

## Unit Testing Patterns

### Testing Message Handlers

```csharp
public class OrderCreatedHandlerTests
{
    [Fact]
    public async Task HandleAsync_ValidMessage_ReturnsSuccess()
    {
        // Arrange
        var repository = new Mock<IOrderRepository>();
        var handler = new OrderCreatedHandler(repository.Object);
        
        var message = new OrderCreated(OrderId: 123, CustomerId: 456);
        var context = new MessageContext
        {
            Envelope = CreateTestEnvelope(message),
            Transaction = new NoOpTransportTransaction(),
            Properties = new Dictionary<string, object>()
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
        var context = CreateTestContext(message);
        
        // Act
        var result = await handler.HandleAsync(message, context, CancellationToken.None);
        
        // Assert
        Assert.Equal(MessageProcessingResult.Retry, result);
    }
}
```

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
        var envelope = CreateTestEnvelope();
        var context = CreateTestContext();
        var nextCalled = false;
        
        // Act
        await middleware.InvokeAsync(envelope, context, () =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, CancellationToken.None);
        
        // Assert
        Assert.True(nextCalled);
    }
    
    [Fact]
    public async Task InvokeAsync_InvalidMessage_ThrowsException()
    {
        // Arrange
        var validator = new Mock<IValidator>();
        validator.Setup(v => v.ValidateAsync(It.IsAny<object>())).ReturnsAsync(false);
        
        var middleware = new ValidationMiddleware(validator.Object);
        
        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(async () =>
            await middleware.InvokeAsync(
                CreateTestEnvelope(),
                CreateTestContext(),
                () => Task.CompletedTask,
                CancellationToken.None));
    }
}
```

---

## Integration Testing Patterns

### End-to-End Message Flow

```csharp
public class OrderIntegrationTests : IClassFixture<TransportTestFixture>
{
    private readonly TransportTestFixture _fixture;
    
    [Fact]
    public async Task PublishAndConsume_OrderCreated_ProcessesSuccessfully()
    {
        // Arrange
        var processed = new TaskCompletionSource<bool>();
        
        _fixture.Broker.Subscribe("orders", async (envelope, ct) =>
        {
            processed.SetResult(true);
            await Task.CompletedTask;
        });
        
        var message = new OrderCreated(123, 456);
        var envelope = CreateEnvelope(message);
        
        // Act
        await _fixture.Publisher.PublishAsync(
            envelope,
            new EndpointAddress("orders"),
            CancellationToken.None);
        
        // Assert
        var result = await processed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(result);
    }
}
```

---

### Testing Grid Context Propagation

```csharp
[Fact]
public async Task GridContext_FlowsAcrossNodes()
{
    // Arrange
    var gridContext = new TestGridContext
    {
        CorrelationId = "test-correlation",
        NodeId = "node-a",
        StudioId = "test-studio"
    };
    
    IGridContext? receivedContext = null;
    
    _fixture.Services.AddMessageHandler<OrderCreated>((msg, ctx, ct) =>
    {
        receivedContext = ctx.GridContext;
        return Task.FromResult(MessageProcessingResult.Success);
    });
    
    // Act
    var message = new OrderCreated(123, 456);
    var envelope = _fixture.Factory.CreateEnvelopeWithGridContext<OrderCreated>(
        _serializer.Serialize(message),
        gridContext);
    
    await _fixture.Publisher.PublishAsync(envelope, destination, ct);
    await Task.Delay(100);
    
    // Assert
    Assert.NotNull(receivedContext);
    Assert.Equal("test-correlation", receivedContext.CorrelationId);
    Assert.Equal("test-studio", receivedContext.StudioId);
}
```

---

### Testing Retry Behavior

```csharp
[Fact]
public async Task RetryMiddleware_TransientError_RetriesWithBackoff()
{
    // Arrange
    var attemptCount = 0;
    var attemptTimes = new List<DateTimeOffset>();
    
    _fixture.Services.AddMessageHandler<OrderCreated>((msg, ctx, ct) =>
    {
        attemptCount++;
        attemptTimes.Add(DateTimeOffset.UtcNow);
        
        if (attemptCount < 3)
            return Task.FromResult(MessageProcessingResult.Retry);
        
        return Task.FromResult(MessageProcessingResult.Success);
    });
    
    // Act
    await _fixture.Publisher.PublishAsync(envelope, destination, ct);
    await Task.Delay(5000); // Allow retries
    
    // Assert
    Assert.Equal(3, attemptCount);
    
    // Verify exponential backoff
    var delay1 = attemptTimes[1] - attemptTimes[0];
    var delay2 = attemptTimes[2] - attemptTimes[1];
    Assert.True(delay2 > delay1, "Second retry should have longer delay");
}
```

---

### Testing Outbox Pattern

```csharp
public class OutboxIntegrationTests
{
    [Fact]
    public async Task Outbox_TransactionRollback_MessageNotDispatched()
    {
        // Arrange
        using var db = CreateTestDatabase();
        var outbox = new EfCoreOutboxStore(db);
        
        // Act - Rollback transaction
        using (var transaction = await db.Database.BeginTransactionAsync())
        {
            await outbox.SaveAsync(CreateTestMessage(), ct);
            await transaction.RollbackAsync();
        }
        
        // Assert - Message not in outbox
        var pending = await outbox.LoadPendingAsync(10, ct);
        Assert.Empty(pending);
    }
    
    [Fact]
    public async Task Outbox_TransactionCommit_MessageDispatched()
    {
        // Arrange
        using var db = CreateTestDatabase();
        var outbox = new EfCoreOutboxStore(db);
        var publisher = new Mock<ITransportPublisher>();
        var dispatcher = new OutboxDispatcherService(outbox, publisher.Object, logger);
        
        // Act - Commit transaction
        using (var transaction = await db.Database.BeginTransactionAsync())
        {
            await outbox.SaveAsync(CreateTestMessage(), ct);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        
        // Dispatch
        await dispatcher.DispatchPendingAsync(ct);
        
        // Assert
        publisher.Verify(p => p.PublishAsync(
            It.IsAny<ITransportEnvelope>(),
            It.IsAny<IEndpointAddress>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

---

## Test Helpers

### Envelope Creation

```csharp
public static class TestEnvelopeFactory
{
    public static ITransportEnvelope CreateEnvelope<TMessage>(
        TMessage message,
        IGridContext? gridContext = null)
        where TMessage : class
    {
        var serializer = new JsonMessageSerializer();
        var payload = serializer.Serialize(message);
        var factory = new EnvelopeFactory(TimeProvider.System);
        
        return gridContext != null
            ? factory.CreateEnvelopeWithGridContext<TMessage>(payload, gridContext)
            : factory.CreateEnvelope<TMessage>(payload);
    }
}
```

---

### Mock Grid Context

```csharp
public class TestGridContext : IGridContext
{
    public string CorrelationId { get; set; } = "test-correlation";
    public string? CausationId { get; set; }
    public string NodeId { get; set; } = "test-node";
    public string StudioId { get; set; } = "test-studio";
    public string Environment { get; set; } = "test";
    public IReadOnlyDictionary<string, string> Baggage { get; set; } 
        = new Dictionary<string, string>();
    public CancellationToken Cancellation { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    
    public IDisposable BeginScope() => new NoOpScope();
    public IGridContext CreateChildContext(string? nodeId = null) => this;
    public IGridContext WithBaggage(string key, string value) => this;
}
```

---

### Time Provider Fakes

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
}

// Usage
var timeProvider = new FakeTimeProvider(new DateTimeOffset(2025, 1, 15, 10, 0, 0, TimeSpan.Zero));
var factory = new EnvelopeFactory(timeProvider);

var envelope1 = factory.CreateEnvelope<OrderCreated>(payload);
Assert.Equal(new DateTimeOffset(2025, 1, 15, 10, 0, 0, TimeSpan.Zero), envelope1.Timestamp);

timeProvider.Advance(TimeSpan.FromHours(1));
var envelope2 = factory.CreateEnvelope<OrderCreated>(payload);
Assert.Equal(new DateTimeOffset(2025, 1, 15, 11, 0, 0, TimeSpan.Zero), envelope2.Timestamp);
```

---

[← Back to File Guide](FILE_GUIDE.md)
