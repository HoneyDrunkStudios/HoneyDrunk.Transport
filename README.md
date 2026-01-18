# HoneyDrunk.Transport

[![Validate PR](https://github.com/HoneyDrunkStudios/HoneyDrunk.Transport/actions/workflows/validate-pr.yml/badge.svg)](https://github.com/HoneyDrunkStudios/HoneyDrunk.Transport/actions/workflows/validate-pr.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/10.0)

> **Reliable messaging and outbox infrastructure for the Hive** - Transport unifies brokers, queues, and event buses under one contract ensuring delivery, order, and idempotence. It powers communication between Nodes—Data, Pulse, Vault, and beyond—so every message finds its way.

## 📦 What Is This?

HoneyDrunk.Transport is the **messaging backbone** of HoneyDrunk.OS ("the Hive"). It provides a transport-agnostic abstraction layer over different message brokers with built-in resilience, observability, and exactly-once semantics.

### What This Package Provides

- **Transport Abstraction** - Unified `ITransportPublisher` and `ITransportConsumer` over Azure Service Bus, Azure Storage Queue, and InMemory
- **Middleware Pipeline** - Onion-style message processing with logging, telemetry, correlation, and retry
- **Envelope Pattern** - Immutable `ITransportEnvelope` with correlation/causation tracking and Grid context propagation
- **Grid Context Integration** - Uses `IGridContext` from HoneyDrunk.Kernel for distributed context propagation
- **Transactional Outbox** - `IOutboxStore` and `IOutboxDispatcher` contracts for exactly-once processing
- **Health Contributors** - `ITransportHealthContributor` for Kubernetes probe integration
- **Observability** - OpenTelemetry spans via `ITransportMetrics` and built-in telemetry middleware
- **Blob Fallback** - Persist failed Service Bus publishes to Azure Blob Storage for later replay

### What This Package Does Not Provide

- **Automatic message routing** — Application responsibility; no built-in routing conventions
- **Message schema registry** — No schema validation or evolution support in v0.1.0
- **Distributed transactions** — Outbox provides eventual consistency, not two-phase commit
- **Non-Azure provider implementations** — RabbitMQ and Kafka are planned but not available

---

## ⚠️ v0.1.0 Limitations

The following features exist as **contracts only** or have limitations in v0.1.0:

| Feature | Contract | v0.1.0 Status |
|---------|----------|---------------|
| Transport publishing | `ITransportPublisher` | ✅ Implemented for Service Bus, Storage Queue, InMemory |
| Transport consuming | `ITransportConsumer` | ✅ Implemented for Service Bus, Storage Queue, InMemory |
| Transactional outbox | `IOutboxStore` | ⚠️ Contract only — application must implement against their database |
| Outbox dispatching | `IOutboxDispatcher` | ✅ `DefaultOutboxDispatcher` provided |
| Health aggregation | `ITransportHealthContributor` | ⚠️ Contributors exist — application wires into health system |
| Message serialization | `IMessageSerializer` | ✅ `JsonMessageSerializer` provided as default |

**Bottom line:** v0.1.0 provides **complete transport abstraction** with Azure providers. Applications must implement:
- `IOutboxStore` if using transactional outbox pattern (database-specific)
- Health endpoint wiring for contributor aggregation
- Custom `IMessageSerializer` if JSON is not suitable

---

## 🚀 Quick Start

### Installation

```sh
# Azure Service Bus transport
dotnet add package HoneyDrunk.Transport.AzureServiceBus

# Or Azure Storage Queue transport
dotnet add package HoneyDrunk.Transport.StorageQueue

# Or InMemory transport (for testing)
dotnet add package HoneyDrunk.Transport.InMemory

# Or just the core abstractions (contracts only)
dotnet add package HoneyDrunk.Transport
```

### Web API Setup

This example shows a web application with Kernel and Azure Service Bus. Simpler setups are possible—see package-specific documentation.

> **Registration order matters.** Kernel must be registered before Transport. See [DependencyInjection.md](HoneyDrunk.Transport/docs/DependencyInjection.md) for details.

```csharp
using HoneyDrunk.Kernel.DependencyInjection;
using HoneyDrunk.Transport.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// 1. Kernel (required for Grid context)
builder.Services.AddHoneyDrunkCoreNode(nodeDescriptor);

// 2. Transport core (middleware pipeline, envelope factory)
builder.Services.AddHoneyDrunkTransportCore(options =>
{
    options.EnableTelemetry = true;
    options.EnableLogging = true;
});

// 3. Azure Service Bus provider
builder.Services.AddHoneyDrunkServiceBusTransport(options =>
{
    options.FullyQualifiedNamespace = "mynamespace.servicebus.windows.net";
    options.Address = "orders";
    options.EntityType = ServiceBusEntityType.Topic;
    options.SubscriptionName = "order-processor";
    options.MaxConcurrency = 10;
});

// 4. Register message handlers
builder.Services.AddMessageHandler<OrderCreatedEvent, OrderCreatedHandler>();

var app = builder.Build();
app.Run();
```

### Abstractions-Only Usage

For libraries that only need contracts without runtime dependencies:

```csharp
// Reference only HoneyDrunk.Transport
// No Kernel runtime, no broker SDK dependencies

public class OrderService
{
    private readonly ITransportPublisher _publisher;
    private readonly EnvelopeFactory _envelopeFactory;
    private readonly IMessageSerializer _serializer;

    public async Task PublishOrderCreatedAsync(Order order, CancellationToken ct)
    {
        var @event = new OrderCreatedEvent { OrderId = order.Id };
        var payload = _serializer.Serialize(@event);
        var envelope = _envelopeFactory.CreateEnvelope<OrderCreatedEvent>(payload);
        
        await _publisher.PublishAsync(
            envelope,
            EndpointAddress.Create("orders", "orders-topic"),
            ct);
    }
}
```

---

## 🎯 Key Features (v0.1.0)

### 📨 Transport Envelope Pattern

All messages are wrapped in immutable `ITransportEnvelope` for distributed tracing:

```csharp
public interface ITransportEnvelope
{
    string MessageId { get; }
    string? CorrelationId { get; }
    string? CausationId { get; }
    string MessageType { get; }
    ReadOnlyMemory<byte> Payload { get; }
    IReadOnlyDictionary<string, string> Headers { get; }
    DateTimeOffset Timestamp { get; }
}

// Create envelopes via factory (integrates with TimeProvider and Grid context)
var envelope = envelopeFactory.CreateEnvelopeWithGridContext<OrderCreatedEvent>(
    payload, gridContext);
```

**Note:** Always use `EnvelopeFactory` to create envelopes. It integrates with `TimeProvider` for deterministic timestamps and `IGridContext` for distributed context propagation.

### 🔗 Grid Context Integration

Transport is fully integrated with Kernel's `IGridContext` for distributed context propagation:

```csharp
public class OrderCreatedHandler : IMessageHandler<OrderCreatedEvent>
{
    public async Task<MessageProcessingResult> HandleAsync(
        OrderCreatedEvent message,
        MessageContext context,
        CancellationToken ct)
    {
        // Access Grid context directly from MessageContext
        var grid = context.GridContext;
        
        _logger.LogInformation(
            "Processing order {OrderId} with CorrelationId {CorrelationId} on Node {NodeId}",
            message.OrderId,
            grid?.CorrelationId,
            grid?.NodeId);
        
        return MessageProcessingResult.Success;
    }
}
```

**Note:** Grid context is extracted from envelope headers by `GridContextPropagationMiddleware` and populated in `MessageContext` automatically.

### 🧅 Middleware Pipeline

Message processing follows an onion-style middleware pattern:

```csharp
// Built-in middleware (executed in order)
// 1. GridContextPropagationMiddleware - Extracts IGridContext from envelope
// 2. TelemetryMiddleware - Distributed tracing via OpenTelemetry
// 3. LoggingMiddleware - Structured logging of message processing

// Custom middleware registration
builder.Services.AddHoneyDrunkTransportCore()
    .AddMiddleware<CustomRetryMiddleware>()
    .AddMiddleware<CustomValidationMiddleware>();
```

**Note:** Middleware order matters. GridContextPropagation must run before telemetry to ensure correlation IDs are available for tracing.

### 📤 Transactional Outbox

For exactly-once processing with database transactions:

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
        
        // Save order to database
        var order = new Order { /* ... */ };
        await dbContext.Orders.AddAsync(order, ct);
        
        // Save message to outbox (same transaction)
        var payload = serializer.Serialize(new OrderCreatedEvent { OrderId = order.Id });
        var envelope = factory.CreateEnvelope<OrderCreatedEvent>(payload);
        
        await outboxStore.SaveAsync(
            EndpointAddress.Create("orders", "orders-topic"),
            envelope, ct);
        
        await dbContext.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        
        // DefaultOutboxDispatcher publishes from outbox in background
    }
}
```

**Note:** `IOutboxStore` is a contract—application must implement against their database. `DefaultOutboxDispatcher` polls the store and publishes pending messages.

### 🏥 Health Contributors

Transport providers include health monitoring for Kubernetes probes:

```csharp
public interface ITransportHealthContributor
{
    string Name { get; }
    ValueTask<TransportHealthResult> CheckHealthAsync(CancellationToken ct);
}

// Each transport registers its own contributor
// - ServiceBusHealthContributor
// - StorageQueueHealthContributor
// - InMemoryHealthContributor
```

**Note:** Health contributors are passive—invoked by host health system on demand. Applications wire contributors into their health check infrastructure.

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

## 📖 Documentation

### Package Documentation
- **[Architecture](HoneyDrunk.Transport/docs/Architecture.md)** - High-level architecture and design principles
- **[Abstractions](HoneyDrunk.Transport/docs/Abstractions.md)** - Core contracts: `ITransportEnvelope`, `IMessageHandler`, `MessageContext`
- **[Pipeline](HoneyDrunk.Transport/docs/Pipeline.md)** - Middleware pipeline and built-in middleware
- **[Configuration](HoneyDrunk.Transport/docs/Configuration.md)** - All options: `TransportCoreOptions`, `RetryOptions`, error strategies
- **[Context](HoneyDrunk.Transport/docs/Context.md)** - Grid context propagation and `IGridContextFactory`
- **[Primitives](HoneyDrunk.Transport/docs/Primitives.md)** - `EnvelopeFactory`, `TransportEnvelope`, serialization
- **[Outbox](HoneyDrunk.Transport/docs/Outbox.md)** - Transactional outbox pattern

### Transport Providers
- **[AzureServiceBus](HoneyDrunk.Transport/docs/AzureServiceBus.md)** - Service Bus transport: sessions, topics, blob fallback
- **[StorageQueue](HoneyDrunk.Transport/docs/StorageQueue.md)** - Storage Queue transport: concurrency model, poison queues
- **[InMemory](HoneyDrunk.Transport/docs/InMemory.md)** - InMemory transport for testing

### Runtime & Observability
- **[Runtime](HoneyDrunk.Transport/docs/Runtime.md)** - `ITransportRuntime` and consumer lifecycle
- **[Health](HoneyDrunk.Transport/docs/Health.md)** - Health monitoring with `ITransportHealthContributor`
- **[Metrics](HoneyDrunk.Transport/docs/Metrics.md)** - `ITransportMetrics` and OpenTelemetry integration
- **[Testing](HoneyDrunk.Transport/docs/Testing.md)** - Test patterns and helpers

---

## 🏗️ Project Structure

```
HoneyDrunk.Transport/
├── HoneyDrunk.Transport/                    # Core abstractions & pipeline
│   ├── Abstractions/                        # Publisher, consumer, handler contracts
│   ├── Pipeline/                            # Middleware execution engine
│   ├── Configuration/                       # TransportCoreOptions, RetryOptions
│   ├── Context/                             # Grid context factory and propagation
│   ├── Primitives/                          # EnvelopeFactory, TransportEnvelope
│   ├── Outbox/                              # IOutboxStore, DefaultOutboxDispatcher
│   ├── Runtime/                             # ITransportRuntime host
│   ├── Health/                              # ITransportHealthContributor
│   ├── Metrics/                             # ITransportMetrics
│   ├── Telemetry/                           # OpenTelemetry integration
│   └── DependencyInjection/                 # AddHoneyDrunkTransportCore()
│
├── HoneyDrunk.Transport.AzureServiceBus/    # Azure Service Bus provider
│   ├── Publishing/                          # ServiceBusTransportPublisher
│   ├── Consuming/                           # ServiceBusTransportConsumer
│   ├── BlobFallback/                        # Blob storage for failed publishes
│   ├── Health/                              # ServiceBusHealthContributor
│   └── DependencyInjection/                 # AddHoneyDrunkServiceBusTransport()
│
├── HoneyDrunk.Transport.StorageQueue/       # Azure Storage Queue provider
│   ├── Publishing/                          # StorageQueueTransportPublisher
│   ├── Consuming/                           # StorageQueueTransportConsumer
│   ├── Health/                              # StorageQueueHealthContributor
│   └── DependencyInjection/                 # AddHoneyDrunkTransportStorageQueue()
│
├── HoneyDrunk.Transport.InMemory/           # In-memory provider (testing)
│   ├── InMemoryBroker                       # Thread-safe in-memory message store
│   ├── Health/                              # InMemoryHealthContributor
│   └── DependencyInjection/                 # AddHoneyDrunkInMemoryTransport()
│
└── HoneyDrunk.Transport.Tests/              # xUnit test suite
```

---

## 🆕 What's New in v0.1.0

### Core Transport
- `ITransportPublisher` and `ITransportConsumer` transport abstraction
- `ITransportEnvelope` immutable message wrapper with Grid context
- `EnvelopeFactory` integrating `TimeProvider` and `IGridContext`
- `IMessagePipeline` with onion-style middleware execution
- `IMessageHandler<T>` and `MessageProcessingResult` for handler contracts
- `GridContextPropagationMiddleware`, `TelemetryMiddleware`, `LoggingMiddleware`
- `IOutboxStore` and `DefaultOutboxDispatcher` for transactional outbox
- `ITransportHealthContributor` for health check participation
- `ITransportMetrics` for OpenTelemetry integration

### Azure Service Bus Provider
- `ServiceBusTransportPublisher` with topic and queue support
- `ServiceBusTransportConsumer` with session and subscription support
- `BlobFallbackPublisher` for failed publish persistence
- `ServiceBusHealthContributor` for connectivity health checks
- `AzureServiceBusOptions` with retry and prefetch configuration

### Azure Storage Queue Provider
- `StorageQueueTransportPublisher` with base64 encoding
- `StorageQueueTransportConsumer` with concurrent polling
- `StorageQueueHealthContributor` for connectivity health checks
- `StorageQueueOptions` with dequeue count and visibility timeout

### InMemory Provider
- `InMemoryBroker` thread-safe message store for testing
- `InMemoryTransportPublisher` and `InMemoryTransportConsumer`
- `InMemoryHealthContributor` always-healthy contributor

---

## 🔗 Related Projects

| Project | Relationship |
|---------|--------------|
| **[HoneyDrunk.Kernel](https://github.com/HoneyDrunkStudios/HoneyDrunk.Kernel)** | Transport depends on Kernel for `IGridContext` and `TimeProvider` |
| **[HoneyDrunk.Standards](https://github.com/HoneyDrunkStudios/HoneyDrunk.Standards)** | Analyzers and coding conventions |
| **HoneyDrunk.Data** | Data access and persistence *(in development)* |
| **HoneyDrunk.Auth** | Authentication and authorization *(in development)* |

**Note:** `HoneyDrunk.Transport` depends only on `HoneyDrunk.Kernel.Abstractions` (contracts, no runtime). Transport providers depend on their respective Azure SDKs.

---

## 📄 License

This project is licensed under the [MIT License](LICENSE).

---

<div align="center">

**Built with 🍯 by HoneyDrunk Studios**

[GitHub](https://github.com/HoneyDrunkStudios/HoneyDrunk.Transport) • [Issues](https://github.com/HoneyDrunkStudios/HoneyDrunk.Transport/issues)

</div>
