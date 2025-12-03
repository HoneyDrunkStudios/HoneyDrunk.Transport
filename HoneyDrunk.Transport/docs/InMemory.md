# 🧪 InMemory - In-Memory Transport for Testing

[← Back to File Guide](FILE_GUIDE.md)

---

## Table of Contents

- [Overview](#overview)
- [InMemoryBroker.cs](#inmemorybrokercs)
- [InMemoryTransportPublisher.cs](#inmemorytransportpublishercs)
- [InMemoryTransportConsumer.cs](#inmemorytransportconsumercs)
- [InMemoryTopology.cs](#inmemorytopologycs)
- [Setup and Configuration](#setup-and-configuration)
- [Testing Patterns](#testing-patterns)
  - [Transport-Level Tests (Recommended)](#transport-level-tests-recommended)
  - [Broker-Level Tests (Advanced)](#broker-level-tests-advanced)
  - [Testing Middleware Pipeline](#testing-middleware-pipeline)
  - [Testing Error Handling](#testing-error-handling)
  - [Performance Testing](#performance-testing)

---

## Overview

In-memory transport adapter for development and testing.

- Provides a lightweight in-process broker for message routing
- Executes the full HoneyDrunk.Transport pipeline (middleware, handlers, runtime) entirely in-process
- Requires no external infrastructure
- **Not intended for production use**

**Location:** `HoneyDrunk.Transport.InMemory/`

> ⚠️ **Not for Production:** The InMemory transport is intended only for development and tests. It does not provide durability, scaling, or fault tolerance guarantees.

---

## InMemoryBroker.cs

The in-memory message broker that wires publisher → consumer → pipeline.

```csharp
public sealed class InMemoryBroker(ILogger<InMemoryBroker> logger)
{
    public Task PublishAsync(
        string address,
        ITransportEnvelope envelope,
        CancellationToken cancellationToken = default);
    
    public void Subscribe(
        string address,
        Func<ITransportEnvelope, CancellationToken, Task> handler);
    
    public Task ConsumeAsync(
        string address,
        Func<ITransportEnvelope, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default);
    
    public int GetMessageCount(string address);
    
    public void ClearQueue(string address);
}
```

### Subscriber Behavior

Multiple subscribers to the same address are **all invoked** (fan-out). Each subscriber receives the message independently via `Task.Run`.

### Methods

| Method | Purpose |
|--------|---------|
| `PublishAsync` | Writes envelope to the channel and notifies all subscribers |
| `Subscribe` | Registers a handler that is invoked on every publish (fan-out) |
| `ConsumeAsync` | Reads from the channel in a loop, invoking handler for each message |
| `GetMessageCount` | Returns pending message count for diagnostics |
| `ClearQueue` | Drains all pending messages without completing the channel |

> **Note:** `GetMessageCount` and `ClearQueue` are intended for diagnostic and advanced test scenarios, not typical application code.

### Usage Example

```csharp
// Direct broker access for low-level tests
var broker = provider.GetRequiredService<InMemoryBroker>();

// Check queue depth
var pending = broker.GetMessageCount("orders");

// Clear queue between tests
broker.ClearQueue("orders");
```

[↑ Back to top](#table-of-contents)

---

## InMemoryTransportPublisher.cs

Standard `ITransportPublisher` implementation that delegates to `InMemoryBroker`.

```csharp
public sealed class InMemoryTransportPublisher(InMemoryBroker broker) 
    : ITransportPublisher
{
    public Task PublishAsync(
        ITransportEnvelope envelope,
        IEndpointAddress destination,
        CancellationToken cancellationToken = default);
    
    public Task PublishAsync(
        IEnumerable<ITransportEnvelope> envelopes,
        IEndpointAddress destination,
        CancellationToken cancellationToken = default);
}
```

### Usage Example

```csharp
// Use like any transport - same code works with ServiceBus, StorageQueue, etc.
public class OrderService(ITransportPublisher publisher, EnvelopeFactory factory)
{
    public async Task PublishOrderAsync(OrderCreated order, CancellationToken ct)
    {
        var envelope = factory.CreateEnvelope<OrderCreated>(
            _serializer.Serialize(order));
        
        await publisher.PublishAsync(
            envelope,
            EndpointAddress.Create("orders", "orders"),
            ct);
    }
}
```

[↑ Back to top](#table-of-contents)

---

## InMemoryTransportConsumer.cs

Standard `ITransportConsumer` implementation that reads from `InMemoryBroker` and forwards to `IMessagePipeline`.

```csharp
public sealed class InMemoryTransportConsumer(
    InMemoryBroker broker,
    IMessagePipeline pipeline,
    IOptions<TransportOptions> options,
    ILogger<InMemoryTransportConsumer> logger) 
    : ITransportConsumer, IAsyncDisposable
{
    public Task StartAsync(CancellationToken cancellationToken = default);
    public Task StopAsync(CancellationToken cancellationToken = default);
    public ValueTask DisposeAsync();
}
```

### What StartAsync Does

On `StartAsync`, the consumer:

1. Creates concurrent consumer tasks based on `TransportOptions.MaxConcurrency`
2. Each consumer calls `broker.ConsumeAsync()` to read from the configured address
3. Each message is forwarded to `IMessagePipeline.ProcessAsync()` for full pipeline execution
4. Pipeline results are logged; `Retry` results re-publish to the same queue

The consumer is registered as an `ITransportConsumer`, so `TransportRuntimeHost` will start and stop it with the rest of the node.

### Usage Example

```csharp
// Registered automatically - no manual interaction needed
services.AddHoneyDrunkTransportCore()
    .AddHoneyDrunkInMemoryTransport();
// Registers: InMemoryBroker, InMemoryTransportPublisher, InMemoryTransportConsumer, InMemoryTopology
```

[↑ Back to top](#table-of-contents)

---

## InMemoryTopology.cs

Implements `ITransportTopology` to declare InMemory transport capabilities.

```csharp
internal sealed class InMemoryTopology : ITransportTopology
{
    public string Name => "InMemory";
    public bool SupportsTopics => true;
    public bool SupportsSubscriptions => true;
    public bool SupportsSessions => false;
    public bool SupportsOrdering => true;  // Single-threaded broker guarantees ordering
    public bool SupportsScheduledMessages => false;
    public bool SupportsBatching => true;
    public bool SupportsTransactions => false;
    public bool SupportsDeadLetterQueue => false;
    public bool SupportsPriority => false;
    public long? MaxMessageSize => null;  // No limit for in-memory
}
```

> See [Architecture → Topology Capabilities](Architecture.md) for how topology is used across transport adapters.

[↑ Back to top](#table-of-contents)

---

## Setup and Configuration

```csharp
public class IntegrationTests : IAsyncLifetime
{
    private ServiceProvider _provider = null!;
    private ITransportPublisher _publisher = null!;
    private ITransportRuntime _runtime = null!;
    
    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();
        
        // Register Kernel
        services.AddLogging();
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
        
        _provider = services.BuildServiceProvider();
        _publisher = _provider.GetRequiredService<ITransportPublisher>();
        _runtime = _provider.GetRequiredService<ITransportRuntime>();
        
        // Start the consumer
        await _runtime.StartAsync();
    }
    
    public async Task DisposeAsync()
    {
        await _runtime.StopAsync();
        await _provider.DisposeAsync();
    }
}
```

[↑ Back to top](#table-of-contents)

---

## Testing Patterns

### Transport-Level Tests (Recommended)

These tests use `ITransportPublisher` and `IMessageHandler<T>`, letting the consumer and pipeline do their work. This is the recommended approach for integration tests.

```csharp
[Fact]
public async Task Processes_OrderCreated_Through_Pipeline()
{
    // Arrange
    var handler = _provider.GetRequiredService<OrderCreatedHandler>();
    handler.ProcessedCount.Should().Be(0);
    
    var envelope = _factory.CreateEnvelope<OrderCreated>(
        _serializer.Serialize(new OrderCreated(123, 456)));
    
    // Act
    await _publisher.PublishAsync(
        envelope,
        EndpointAddress.Create("orders", "orders"),
        CancellationToken.None);
    
    // Allow consumer time to process
    await Task.Delay(100);
    
    // Assert
    handler.ProcessedCount.Should().Be(1);
}

[Fact]
public async Task Handler_Receives_Correct_Message_Content()
{
    // Arrange
    var handler = _provider.GetRequiredService<OrderCreatedHandler>();
    var order = new OrderCreated(OrderId: 999, CustomerId: 888);
    
    var envelope = _factory.CreateEnvelope<OrderCreated>(
        _serializer.Serialize(order));
    
    // Act
    await _publisher.PublishAsync(
        envelope,
        EndpointAddress.Create("orders", "orders"),
        CancellationToken.None);
    
    await Task.Delay(100);
    
    // Assert
    handler.LastOrder.Should().NotBeNull();
    handler.LastOrder!.OrderId.Should().Be(999);
    handler.LastOrder.CustomerId.Should().Be(888);
}
```

[↑ Back to top](#table-of-contents)

---

### Broker-Level Tests (Advanced)

These tests access `InMemoryBroker` directly to test low-level broker behavior. This bypasses handlers and middleware, so use sparingly.

```csharp
[Fact]
public async Task Broker_FansOut_To_Multiple_Subscribers()
{
    // Arrange
    var broker = _provider.GetRequiredService<InMemoryBroker>();
    var subscriber1Called = false;
    var subscriber2Called = false;
    
    broker.Subscribe("test-address", async (envelope, ct) =>
    {
        subscriber1Called = true;
        await Task.CompletedTask;
    });
    
    broker.Subscribe("test-address", async (envelope, ct) =>
    {
        subscriber2Called = true;
        await Task.CompletedTask;
    });
    
    var envelope = _factory.CreateEnvelope<TestMessage>(
        _serializer.Serialize(new TestMessage("hello")));
    
    // Act
    await broker.PublishAsync("test-address", envelope, CancellationToken.None);
    await Task.Delay(50);
    
    // Assert - both subscribers are invoked
    subscriber1Called.Should().BeTrue();
    subscriber2Called.Should().BeTrue();
}

[Fact]
public async Task Broker_Clears_Queue_Between_Tests()
{
    // Arrange
    var broker = _provider.GetRequiredService<InMemoryBroker>();
    var envelope = _factory.CreateEnvelope<TestMessage>(
        _serializer.Serialize(new TestMessage("hello")));
    
    await broker.PublishAsync("cleanup-test", envelope, CancellationToken.None);
    broker.GetMessageCount("cleanup-test").Should().Be(1);
    
    // Act
    broker.ClearQueue("cleanup-test");
    
    // Assert
    broker.GetMessageCount("cleanup-test").Should().Be(0);
}
```

[↑ Back to top](#table-of-contents)

---

### Testing Middleware Pipeline

```csharp
[Fact]
public async Task Middleware_Executes_In_Onion_Order()
{
    // Arrange
    var executionOrder = new List<string>();
    
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddHoneyDrunkCoreNode(TestNodeDescriptor);
    services.AddHoneyDrunkTransportCore()
        .AddHoneyDrunkInMemoryTransport();
    
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
    
    var provider = services.BuildServiceProvider();
    var runtime = provider.GetRequiredService<ITransportRuntime>();
    var publisher = provider.GetRequiredService<ITransportPublisher>();
    
    await runtime.StartAsync();
    
    // Act
    var envelope = CreateEnvelope(new OrderCreated(1, 2));
    await publisher.PublishAsync(envelope, EndpointAddress.Create("orders", "orders"), ct);
    await Task.Delay(100);
    
    // Assert
    executionOrder.Should().Equal(
        "Middleware1-Before",
        "Middleware2-Before",
        "Handler",
        "Middleware2-After",
        "Middleware1-After");
    
    await runtime.StopAsync();
}
```

[↑ Back to top](#table-of-contents)

---

### Testing Error Handling

```csharp
[Fact]
public async Task Handler_Can_Signal_Retry()
{
    // Arrange
    var attemptCount = 0;
    
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddHoneyDrunkCoreNode(TestNodeDescriptor);
    services.AddHoneyDrunkTransportCore()
        .AddHoneyDrunkInMemoryTransport();
    
    services.AddMessageHandler<OrderCreated>(async (message, context, ct) =>
    {
        attemptCount++;
        
        if (attemptCount < 3)
            return MessageProcessingResult.Retry;
        
        return MessageProcessingResult.Success;
    });
    
    var provider = services.BuildServiceProvider();
    var runtime = provider.GetRequiredService<ITransportRuntime>();
    var publisher = provider.GetRequiredService<ITransportPublisher>();
    
    await runtime.StartAsync();
    
    // Act
    var envelope = CreateEnvelope(new OrderCreated(1, 2));
    await publisher.PublishAsync(envelope, EndpointAddress.Create("orders", "orders"), ct);
    await Task.Delay(500); // Allow retries
    
    // Assert
    attemptCount.Should().Be(3);
    
    await runtime.StopAsync();
}
```

[↑ Back to top](#table-of-contents)

---

### Performance Testing

```csharp
[Fact]
public async Task Handles_High_Volume_Messages()
{
    // Arrange
    const int messageCount = 10_000;
    var processedCount = 0;
    
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddHoneyDrunkCoreNode(TestNodeDescriptor);
    services.AddHoneyDrunkTransportCore()
        .AddHoneyDrunkInMemoryTransport();
    
    services.AddMessageHandler<OrderCreated>((msg, ctx, ct) =>
    {
        Interlocked.Increment(ref processedCount);
        return Task.FromResult(MessageProcessingResult.Success);
    });
    
    var provider = services.BuildServiceProvider();
    var runtime = provider.GetRequiredService<ITransportRuntime>();
    var publisher = provider.GetRequiredService<ITransportPublisher>();
    
    await runtime.StartAsync();
    
    // Act
    var destination = EndpointAddress.Create("orders", "orders");
    var tasks = Enumerable.Range(0, messageCount)
        .Select(i => publisher.PublishAsync(
            CreateEnvelope(new OrderCreated(i, i)),
            destination,
            CancellationToken.None));
    
    await Task.WhenAll(tasks);
    await Task.Delay(5000); // Allow processing
    
    // Assert
    processedCount.Should().Be(messageCount);
    
    await runtime.StopAsync();
}
```

> **Note:** This measures handler and pipeline behavior in-process. It does not simulate broker throughput limits, network latency, or lock timeouts. Do not use InMemory performance numbers for production capacity planning.

[↑ Back to top](#table-of-contents)

---

[← Back to File Guide](FILE_GUIDE.md) | [↑ Back to top](#table-of-contents)
