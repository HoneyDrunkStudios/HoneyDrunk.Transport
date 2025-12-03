# HoneyDrunk.Transport

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/10.0)

> **Reliable messaging and outbox infrastructure for the Hive** - Transport unifies brokers, queues, and event buses under one contract ensuring delivery, order, and idempotence. It powers communication between Nodes—Data, Pulse, Vault, and beyond—so every message finds its way.

**Signal Quote:** *"Every message finds its way."*

---

## 📦 What Is This?

HoneyDrunk.Transport is the **messaging backbone** of HoneyDrunk.OS ("the Hive"). It provides a transport-agnostic abstraction layer over different message brokers with built-in resilience, observability, and exactly-once semantics:

- ✅ **Transport Abstraction** - Unified `ITransportPublisher` and `ITransportConsumer` over Azure Service Bus, Azure Storage Queue, and InMemory
- ✅ **Middleware Pipeline** - Onion-style processing with logging, telemetry, correlation, and retry
- ✅ **Envelope Pattern** - Immutable `ITransportEnvelope` with correlation/causation tracking
- ✅ **Transactional Outbox** - Exactly-once processing with database transactions
- ✅ **Kernel Integration** - Uses `TimeProvider` and `IGridContext` from HoneyDrunk.Kernel for deterministic timestamps and distributed context
- ✅ **Observability** - OpenTelemetry spans and pluggable `ITransportMetrics`
- ✅ **Blob Fallback for Service Bus** - Persist failed publishes to Azure Blob Storage for later replay

---

## 🚀 Quick Start

### Installation

```xml
<ItemGroup>
  <PackageReference Include="HoneyDrunk.Transport" Version="0.1.0" />
  <PackageReference Include="HoneyDrunk.Transport.AzureServiceBus" Version="0.1.0" />
  <PackageReference Include="HoneyDrunk.Transport.StorageQueue" Version="0.1.0" />
  <PackageReference Include="HoneyDrunk.Transport.InMemory" Version="0.1.0" />
</ItemGroup>
```

### Configure in Program.cs

```csharp
using HoneyDrunk.Kernel.DependencyInjection;
using HoneyDrunk.Transport.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// 1. Register Kernel node
builder.Services.AddHoneyDrunkCoreNode(nodeDescriptor);

// 2. Register Transport core
builder.Services.AddHoneyDrunkTransportCore(options =>
{
    options.EnableTelemetry = true;
    options.EnableLogging = true;
    options.EnableCorrelation = true;
});

// 3. Choose a transport

// Azure Service Bus
builder.Services.AddHoneyDrunkServiceBusTransport(options =>
{
    options.FullyQualifiedNamespace = "mynamespace.servicebus.windows.net";
    options.Address = "orders";
    options.EntityType = ServiceBusEntityType.Topic;
    options.SubscriptionName = "order-processor";
    options.MaxConcurrency = 10;
    options.PrefetchCount = 20;

    options.ServiceBusRetry.Mode = ServiceBusRetryMode.Exponential;
    options.ServiceBusRetry.MaxRetries = 3;
});

// OR Azure Storage Queue
builder.Services
    .AddHoneyDrunkTransportStorageQueue(
        builder.Configuration["StorageQueue:ConnectionString"]!,
        "orders")
    .WithMaxDequeueCount(5)
    .WithConcurrency(10);

// 4. Register message handlers
builder.Services.AddMessageHandler<OrderCreatedEvent, OrderCreatedHandler>();

var app = builder.Build();
app.Run();
```

---

## 📖 Usage Examples

### Publishing Messages

```csharp
public class OrderService(
    ITransportPublisher publisher,
    EnvelopeFactory envelopeFactory,
    IMessageSerializer serializer,
    IGridContext gridContext)
{
    public async Task CreateOrderAsync(CreateOrderCommand command, CancellationToken ct)
    {
        // Create order...
        
        // Publish event
        var @event = new OrderCreatedEvent { OrderId = orderId, Total = total };
        var payload = serializer.Serialize(@event);
        var envelope = envelopeFactory.CreateEnvelopeWithGridContext<OrderCreatedEvent>(
            payload, gridContext);
        
        await publisher.PublishAsync(
            envelope,
            EndpointAddress.Create("orders", "orders-topic"),
            ct);
    }
}
```

### Handling Messages

```csharp
public class OrderCreatedHandler : IMessageHandler<OrderCreatedEvent>
{
    private readonly ILogger<OrderCreatedHandler> _logger;
    
    public OrderCreatedHandler(ILogger<OrderCreatedHandler> logger)
    {
        _logger = logger;
    }
    
    public async Task<MessageProcessingResult> HandleAsync(
        OrderCreatedEvent message,
        MessageContext context,
        CancellationToken cancellationToken)
    {
        var grid = context.GridContext;
        
        _logger.LogInformation(
            "Processing order {OrderId} with CorrelationId {CorrelationId} on Node {NodeId}",
            message.OrderId,
            grid?.CorrelationId,
            grid?.NodeId);
        
        await SendConfirmationEmailAsync(message.OrderId, cancellationToken);
        return MessageProcessingResult.Success;
    }
}
```

### Transactional Outbox

```csharp
public class OrderService(
    IOutboxStore outboxStore,
    EnvelopeFactory factory,
    IMessageSerializer serializer,
    IDbContext dbContext)
{
    public async Task CreateOrderAsync(CreateOrderCommand command, CancellationToken ct)
    {
        await using var transaction = await dbContext.BeginTransactionAsync(ct);
        
        try
        {
            // Save order to database
            var order = new Order { /* ... */ };
            await dbContext.Orders.AddAsync(order, ct);
            
            // Save message to outbox (same transaction)
            var payload = serializer.Serialize(new OrderCreatedEvent { OrderId = order.Id });
            var envelope = factory.CreateEnvelope<OrderCreatedEvent>(payload);
            var destination = EndpointAddress.Create("orders", "orders-topic");
            
            await outboxStore.SaveAsync(destination, envelope, ct);
            
            await dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            
            // DefaultOutboxDispatcher publishes from outbox in background
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }
}
```

---

## 🎯 Features

### 🔍 Core Components

| Component | Purpose | Key Types |
|-----------|---------|-----------|
| **Transport Abstraction** | Unified publisher/consumer interface | `ITransportPublisher`, `ITransportConsumer` |
| **Message Pipeline** | Middleware execution engine | `IMessagePipeline`, `IMessageMiddleware` |
| **Envelope System** | Immutable message wrapping | `ITransportEnvelope`, `EnvelopeFactory` |
| **Grid Context** | Correlation/causation tracking | `IGridContext`, `IGridContextFactory` |
| **Serialization** | Pluggable message serialization | `IMessageSerializer`, `JsonMessageSerializer` |
| **Outbox Pattern** | Transactional outbox support | `IOutboxStore`, `DefaultOutboxDispatcher` |

### 🔗 Kernel Integration

HoneyDrunk.Transport **extends** HoneyDrunk.Kernel with messaging primitives:

| Kernel Service | How Transport Uses It |
|----------------|----------------------|
| `TimeProvider` | Deterministic message timestamps via `EnvelopeFactory` |
| `IGridContext` | Correlation, causation, Node/Studio/Tenant propagation |
| `IGridContextFactory` | Creates Grid context for outbound messages |
| `ILogger<T>` | Structured logging throughout pipeline |
| `IMeterFactory` | OpenTelemetry metrics via `ITransportMetrics` |

### 🚀 Available Transports

| Transport | Package | Status |
|-----------|---------|--------|
| **Azure Service Bus** | `HoneyDrunk.Transport.AzureServiceBus` | ✅ Available |
| **Azure Storage Queue** | `HoneyDrunk.Transport.StorageQueue` | ✅ Available |
| **In-Memory** | `HoneyDrunk.Transport.InMemory` | ✅ Available (Testing) |
| **RabbitMQ** | `HoneyDrunk.Transport.RabbitMQ` | 🚧 Planned |
| **Kafka** | `HoneyDrunk.Transport.Kafka` | 🚧 Planned |

---

## 🧪 Testing

Use InMemory transport and DI for tests:

```csharp
var services = new ServiceCollection();
services.AddHoneyDrunkCoreNode(TestNodeDescriptor);
services.AddHoneyDrunkTransportCore()
    .AddHoneyDrunkInMemoryTransport();

services.AddMessageHandler<OrderCreatedEvent>((msg, ctx, ct) =>
{
    // Assert in handler
    return Task.FromResult(MessageProcessingResult.Success);
});

await using var provider = services.BuildServiceProvider();

var broker = provider.GetRequiredService<InMemoryBroker>();
var publisher = provider.GetRequiredService<ITransportPublisher>();
var pipeline = provider.GetRequiredService<IMessagePipeline>();

// Use broker for broker-level tests, pipeline for pipeline-level tests
```

See [Testing.md](HoneyDrunk.Transport/docs/Testing.md) for complete patterns including unit tests, integration tests, and test helpers.

---

## 📚 Documentation

| Document | Description |
|----------|-------------|
| [Architecture.md](HoneyDrunk.Transport/docs/Architecture.md) | High-level architecture and design principles |
| [Abstractions.md](HoneyDrunk.Transport/docs/Abstractions.md) | Core contracts: `ITransportEnvelope`, `IMessageHandler`, `MessageContext` |
| [Pipeline.md](HoneyDrunk.Transport/docs/Pipeline.md) | Middleware pipeline and built-in middleware |
| [Configuration.md](HoneyDrunk.Transport/docs/Configuration.md) | All options: `TransportCoreOptions`, `RetryOptions`, error strategies |
| [Context.md](HoneyDrunk.Transport/docs/Context.md) | Grid context propagation and `IGridContextFactory` |
| [Primitives.md](HoneyDrunk.Transport/docs/Primitives.md) | `EnvelopeFactory`, `TransportEnvelope`, serialization |
| [AzureServiceBus.md](HoneyDrunk.Transport/docs/AzureServiceBus.md) | Service Bus transport: sessions, topics, blob fallback |
| [StorageQueue.md](HoneyDrunk.Transport/docs/StorageQueue.md) | Storage Queue transport: concurrency model, poison queues |
| [InMemory.md](HoneyDrunk.Transport/docs/InMemory.md) | InMemory transport for testing |
| [Outbox.md](HoneyDrunk.Transport/docs/Outbox.md) | Transactional outbox pattern |
| [Runtime.md](HoneyDrunk.Transport/docs/Runtime.md) | `ITransportRuntime` and consumer lifecycle |
| [Health.md](HoneyDrunk.Transport/docs/Health.md) | Health monitoring with `ITransportHealthContributor` |
| [Metrics.md](HoneyDrunk.Transport/docs/Metrics.md) | `ITransportMetrics` and OpenTelemetry integration |
| [Testing.md](HoneyDrunk.Transport/docs/Testing.md) | Test patterns and helpers |

---

## 🛠️ Repository Layout

```
HoneyDrunk.Transport/
├── HoneyDrunk.Transport/                    # Core abstractions & pipeline
│   ├── Abstractions/                        # Contracts & interfaces
│   ├── Pipeline/                            # Middleware execution engine
│   ├── Configuration/                       # Options & settings
│   ├── Context/                             # Grid context integration
│   ├── Primitives/                          # Envelope & factory
│   ├── Outbox/                              # Transactional outbox
│   ├── Runtime/                             # ITransportRuntime host
│   ├── Health/                              # Health contributors
│   ├── Metrics/                             # ITransportMetrics
│   ├── Telemetry/                           # OpenTelemetry integration
│   └── DependencyInjection/                 # DI registration
├── HoneyDrunk.Transport.AzureServiceBus/    # Azure Service Bus provider
├── HoneyDrunk.Transport.StorageQueue/       # Azure Storage Queue provider
├── HoneyDrunk.Transport.InMemory/           # In-memory provider
├── HoneyDrunk.Transport.Tests/              # Test project
└── docs/                                    # Documentation
```

---

## ⚖️ Storage Queue vs Service Bus

| Scenario | Storage Queue | Service Bus |
|----------|---------------|-------------|
| **Cost optimization** | ✅ $0.0004/10K ops | ❌ Higher cost |
| **High volume (millions/day)** | ✅ Excellent | ✅ Good |
| **Simple queue semantics** | ✅ Yes | ✅ Yes |
| **Message size < 64KB** | ✅ Yes | ✅ Up to 100MB |
| **Topics/subscriptions (fan-out)** | ❌ No | ✅ Yes |
| **Sessions (ordered processing)** | ❌ No | ✅ Yes |
| **Transactions** | ❌ No | ✅ Yes |
| **Duplicate detection** | ❌ No | ✅ Yes |

**Choose Storage Queue** for cost-effective, high-volume, simple queue scenarios.  
**Choose Service Bus** for enterprise messaging with topics, sessions, or transactions.

---

## 📄 License

[MIT](LICENSE)
