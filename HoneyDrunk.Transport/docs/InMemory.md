# 🧪 InMemory - In-Memory Transport for Testing

[← Back to File Guide](FILE_GUIDE.md)

---

## Table of Contents

- [Overview](#overview)
- [InMemoryBroker.cs](#inmemorybrokercs)
- [InMemoryTransportPublisher.cs](#inmemorytransportpublishercs)
- [InMemoryTransportConsumer.cs](#inmemorytransportconsumercs)
- [Setup and Configuration](#setup-and-configuration)
- [Testing Patterns](#testing-patterns)
  - [End-to-End Message Flow](#end-to-end-message-flow)
  - [Verify Message Content](#verify-message-content)
  - [Testing Error Handling](#testing-error-handling)
  - [Testing Middleware Pipeline](#testing-middleware-pipeline)
  - [Performance Testing](#performance-testing)

---

## Overview

In-memory message broker for testing without external dependencies. Provides full pipeline execution with no infrastructure requirements.

**Location:** `HoneyDrunk.Transport.InMemory/`

---

## InMemoryBroker.cs

```csharp
public sealed class InMemoryBroker
{
    public void Subscribe(string address, Func<ITransportEnvelope, CancellationToken, Task> handler);
    public Task PublishAsync(ITransportEnvelope envelope, string address, CancellationToken ct);
    public Channel<ITransportEnvelope> GetQueue(string address);
}
```

### Usage Example

```csharp
// Access broker for testing
var broker = services.GetRequiredService<InMemoryBroker>();

// Publish test message
await broker.PublishAsync(testEnvelope, "test-queue", ct);

// Verify message received
var queue = broker.GetQueue("test-queue");
var received = await queue.Reader.ReadAsync(ct);
Assert.Equal(testEnvelope.MessageId, received.MessageId);
```

---

## InMemoryTransportPublisher.cs

```csharp
public sealed class InMemoryTransportPublisher(InMemoryBroker broker) 
    : ITransportPublisher
{
    public Task PublishAsync(
        ITransportEnvelope envelope,
        IEndpointAddress destination,
        CancellationToken ct)
    {
        return broker.PublishAsync(envelope, destination.Address, ct);
    }
}
```

### Usage Example

```csharp
// Inject and use like any transport
public class OrderService(ITransportPublisher publisher)
{
    public async Task PublishOrderAsync(Order order, CancellationToken ct)
    {
        var envelope = CreateEnvelope(order);
        await publisher.PublishAsync(envelope, new EndpointAddress("orders"), ct);
        // Published to in-memory broker
    }
}
```

---

## InMemoryTransportConsumer.cs

```csharp
public sealed class InMemoryTransportConsumer(
    InMemoryBroker broker,
    IMessagePipeline pipeline) 
    : ITransportConsumer
{
    public Task StartAsync(CancellationToken ct);
    public Task StopAsync(CancellationToken ct);
}
```

### Usage Example

```csharp
// Started automatically as BackgroundService
// Consumes from all subscribed addresses
services.AddHoneyDrunkInMemoryTransport();

// Consumer automatically processes messages through pipeline
```

---

## Setup and Configuration

```csharp
// Test setup
public class IntegrationTests
{
    private readonly ServiceProvider _services;
    
    public IntegrationTests()
    {
        var services = new ServiceCollection();
        
        // Register Kernel
        services.AddHoneyDrunkCoreNode(new NodeDescriptor
        {
            NodeId = "test-node",
            Version = "1.0.0"
        });
        
        // Register InMemory transport
        services.AddHoneyDrunkTransportCore()
            .AddHoneyDrunkInMemoryTransport();
        
        // Register handlers
        services.AddMessageHandler<OrderCreated, OrderCreatedHandler>();
        
        _services = services.BuildServiceProvider();
    }
}
```

---

## Testing Patterns

### End-to-End Message Flow

```csharp
[Fact]
public async Task ProcessesOrderCreatedMessage()
{
    // Arrange
    var publisher = _services.GetRequiredService<ITransportPublisher>();
    var broker = _services.GetRequiredService<InMemoryBroker>();
    var handlerCalled = false;
    
    broker.Subscribe("orders", async (envelope, ct) =>
    {
        handlerCalled = true;
        await Task.CompletedTask;
    });
    
    var message = new OrderCreated(123, 456);
    var envelope = CreateEnvelope(message);
    
    // Act
    await publisher.PublishAsync(envelope, new EndpointAddress("orders"), ct);
    await Task.Delay(100); // Give consumer time to process
    
    // Assert
    Assert.True(handlerCalled);
}
```

---

### Verify Message Content

```csharp
[Fact]
public async Task PublishesCorrectMessageContent()
{
    var broker = _services.GetRequiredService<InMemoryBroker>();
    ITransportEnvelope? received = null;
    
    broker.Subscribe("orders", async (envelope, ct) =>
    {
        received = envelope;
        await Task.CompletedTask;
    });
    
    var message = new OrderCreated(OrderId: 123, CustomerId: 456);
    var envelope = CreateEnvelope(message);
    
    await _publisher.PublishAsync(envelope, new EndpointAddress("orders"), ct);
    await Task.Delay(100);
    
    Assert.NotNull(received);
    Assert.Equal(envelope.MessageId, received.MessageId);
    Assert.Equal(typeof(OrderCreated).FullName, received.MessageType);
    
    var deserializedMessage = _serializer.Deserialize<OrderCreated>(received.Payload);
    Assert.Equal(123, deserializedMessage.OrderId);
}
```

---

### Testing Error Handling

```csharp
[Fact]
public async Task RetriesOnTransientError()
{
    var attemptCount = 0;
    
    services.AddMessageHandler<OrderCreated>(async (message, context, ct) =>
    {
        attemptCount++;
        
        if (attemptCount < 3)
            return MessageProcessingResult.Retry;
        
        return MessageProcessingResult.Success;
    });
    
    await _publisher.PublishAsync(envelope, destination, ct);
    await Task.Delay(500); // Allow retries
    
    Assert.Equal(3, attemptCount);
}
```

---

### Testing Middleware Pipeline

```csharp
[Fact]
public async Task MiddlewareExecutesInOrder()
{
    var executionOrder = new List<string>();
    
    services.AddMessageMiddleware(async (envelope, context, next, ct) =>
    {
        executionOrder.Add("Middleware1-Before");
        await next();
        executionOrder.Add("Middleware1-After");
    });
    
    services.AddMessageMiddleware(async (envelope, context, next, ct) =>
    {
        executionOrder.Add("Middleware2-Before");
        await next();
        executionOrder.Add("Middleware2-After");
    });
    
    services.AddMessageHandler<OrderCreated>((msg, ctx, ct) =>
    {
        executionOrder.Add("Handler");
        return Task.FromResult(MessageProcessingResult.Success);
    });
    
    await _publisher.PublishAsync(envelope, destination, ct);
    await Task.Delay(100);
    
    Assert.Equal(new[]
    {
        "Middleware1-Before",
        "Middleware2-Before",
        "Handler",
        "Middleware2-After",
        "Middleware1-After"
    }, executionOrder);
}
```

---

### Performance Testing

```csharp
[Fact]
public async Task HandlesHighVolumeMessages()
{
    var messageCount = 10000;
    var processedCount = 0;
    
    services.AddMessageHandler<OrderCreated>((msg, ctx, ct) =>
    {
        Interlocked.Increment(ref processedCount);
        return Task.FromResult(MessageProcessingResult.Success);
    });
    
    var tasks = Enumerable.Range(0, messageCount)
        .Select(i => _publisher.PublishAsync(
            CreateEnvelope(new OrderCreated(i, i)),
            destination,
            ct));
    
    await Task.WhenAll(tasks);
    await Task.Delay(5000); // Allow processing
    
    Assert.Equal(messageCount, processedCount);
}
```

---

[← Back to File Guide](FILE_GUIDE.md) | [↑ Back to top](#table-of-contents)
