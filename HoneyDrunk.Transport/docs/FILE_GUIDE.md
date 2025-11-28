# 📦 HoneyDrunk.Transport - Complete File Guide

## Overview

**Think of this library as a postal service for your application**

Just like how the postal service lets you send letters without worrying about trucks, planes, or delivery routes, this library lets applications send messages without worrying about the underlying messaging technology. It provides a unified abstraction layer over different message brokers (Azure Service Bus, Storage Queue, InMemory) with middleware pipeline pattern, retry strategies, and Grid-aware context propagation.

**Key Concepts:**
- **Envelope**: The wrapper around every message (like a postal envelope with address, tracking number, contents)
- **Publisher**: Sends messages to destinations (like the post office counter)
- **Consumer**: Receives and processes messages (like your mailbox)
- **Middleware**: Processing pipeline stations (like quality control on an assembly line)
- **Grid Context**: Distributed context that flows across Node boundaries (correlation, causation, Node/Studio/Environment)

---

## 📚 Documentation Structure

This guide is organized into focused documents by domain:

### 🏛️ Architecture

| Document | Description |
|----------|-------------|
| [Architecture.md](Architecture.md) | **Dependency flow, layer responsibilities, and integration patterns** |

### 🔷 HoneyDrunk.Transport (Core)

| Domain | Document | Description |
|--------|----------|-------------|
| 📋 **Abstractions** | [Abstractions.md](Abstractions.md) | Core contracts (ITransportEnvelope, ITransportPublisher, ITransportConsumer, IMessageHandler) |
| 🔧 **Primitives** | [Primitives.md](Primitives.md) | Building blocks (TransportEnvelope, EnvelopeFactory, message serialization) |
| ⚙️ **Configuration** | [Configuration.md](Configuration.md) | Settings (RetryOptions, BackoffStrategy, error handling) |
| 🔄 **Pipeline** | [Pipeline.md](Pipeline.md) | Message processing chain (middleware, handlers, execution flow) |
| 🌐 **Context** | [Context.md](Context.md) | Grid context integration (IGridContext, GridContextFactory, propagation) |
| ❤️ **Health** | [Health.md](Health.md) | Health monitoring (ITransportHealthContributor, health checks) |
| 📈 **Metrics** | [Metrics.md](Metrics.md) | Metrics collection (ITransportMetrics, telemetry integration) |
| 📤 **Outbox** | [Outbox.md](Outbox.md) | Transactional outbox pattern (IOutboxStore, reliable messaging) |
| 🔌 **DI** | [DependencyInjection.md](DependencyInjection.md) | Service registration (fluent builders, extensions) |

### 🔸 Transport Implementations

| Document | Description |
|----------|-------------|
| [InMemory.md](InMemory.md) | In-memory transport for testing (no external dependencies) |
| [StorageQueue.md](StorageQueue.md) | Azure Storage Queue transport (cost-effective, high-volume) |
| [AzureServiceBus.md](AzureServiceBus.md) | Azure Service Bus transport (topics, sessions, advanced features) |

### 🧪 Testing

| Document | Description |
|----------|-------------|
| [Testing.md](Testing.md) | Test patterns, InMemory transport usage, test helpers |

---

## 🔷 Quick Start

### Basic Concepts

**Message Flow:**
```
Service A                    Transport                    Service B
   ↓                            ↓                            ↓
Create Message → Wrap in Envelope → Publish → Queue/Topic → Consume → Unwrap → Handle
   ↓                            ↓                            ↓
OrderCreated      TransportEnvelope    Azure Service Bus    Pipeline    OrderCreatedHandler
```

**Grid Context Propagation:**
```
Node A (OrderService)
  ├─ GridContext: CorrelationId=abc-123, NodeId=order-node
  └─ Publishes OrderCreated → Envelope includes Grid context
       ↓
    Queue/Topic
       ↓
Node B (PaymentService)
  ├─ Consumes OrderCreated → Extracts Grid context from envelope
  └─ GridContext: CorrelationId=abc-123, NodeId=payment-node, CausationId=abc-123
```

**Middleware Pipeline:**
```
GridContextPropagation → Telemetry → Logging → CustomMiddleware → Handler
      ↓                      ↓          ↓              ↓              ↓
Extract IGridContext   Start span   Log received   Validate      Process message
```

### Installation

```bash
# Core abstractions and pipeline
dotnet add package HoneyDrunk.Transport

# Choose transport implementation
dotnet add package HoneyDrunk.Transport.AzureServiceBus
# OR
dotnet add package HoneyDrunk.Transport.StorageQueue
# OR (for testing)
dotnet add package HoneyDrunk.Transport.InMemory
```

### Basic Usage

```csharp
// Program.cs - Setup
var builder = WebApplication.CreateBuilder(args);

// Step 1: Register Kernel (required)
var nodeDescriptor = new NodeDescriptor
{
    NodeId = "order-service",
    Version = "1.0.0",
    Name = "Order Processing Service"
};
builder.Services.AddHoneyDrunkCoreNode(nodeDescriptor);

// Step 2: Register Transport
builder.Services.AddHoneyDrunkTransportCore(options =>
{
    options.EnableTelemetry = true;
    options.EnableLogging = true;
})
.AddHoneyDrunkServiceBusTransport(options =>
{
    options.ConnectionString = builder.Configuration["ServiceBus:ConnectionString"];
    options.TopicName = "orders";
})
.WithRetry(retry =>
{
    retry.MaxAttempts = 5;
    retry.BackoffStrategy = BackoffStrategy.Exponential;
});

// Step 3: Register message handlers
builder.Services.AddMessageHandler<OrderCreated, OrderCreatedHandler>();
builder.Services.AddMessageHandler<OrderCancelled, OrderCancelledHandler>();

var app = builder.Build();
app.Run();
```

```csharp
// Publishing Messages
public class OrderService(
    ITransportPublisher publisher,
    EnvelopeFactory factory,
    IGridContext gridContext,
    IMessageSerializer serializer)
{
    public async Task CreateOrderAsync(Order order, CancellationToken ct)
    {
        // Save to database
        await _repository.SaveAsync(order, ct);
        
        // Publish event with Grid context
        var message = new OrderCreated(order.Id, order.CustomerId);
        var payload = serializer.Serialize(message);
        var envelope = factory.CreateEnvelopeWithGridContext<OrderCreated>(
            payload,
            gridContext);
        
        await publisher.PublishAsync(envelope, new EndpointAddress("orders"), ct);
    }
}
```

```csharp
// Handling Messages
public class OrderCreatedHandler(ILogger<OrderCreatedHandler> logger) 
    : IMessageHandler<OrderCreated>
{
    public async Task<MessageProcessingResult> HandleAsync(
        OrderCreated message,
        MessageContext context,
        CancellationToken ct)
    {
        // Access Grid context
        var gridContext = context.GridContext;
        logger.LogInformation(
            "Processing order {OrderId} in Node {NodeId} with correlation {CorrelationId}",
            message.OrderId,
            gridContext?.NodeId,
            gridContext?.CorrelationId);
        
        try
        {
            await ProcessOrderAsync(message, ct);
            return MessageProcessingResult.Success;
        }
        catch (TransientException)
        {
            return MessageProcessingResult.Retry;  // Retry with backoff
        }
        catch (PermanentException)
        {
            return MessageProcessingResult.DeadLetter;  // Move to DLQ
        }
    }
}
```

---

## 🔷 Design Philosophy

### Core Principles

1. **Transport-agnostic** - Swap brokers without changing business logic
2. **Grid-aware** - First-class integration with Kernel for distributed context
3. **Middleware pipeline** - Cross-cutting concerns without handler pollution
4. **Immutable envelopes** - Thread-safe, testable message wrappers
5. **Abstractions-only** - Depend on `HoneyDrunk.Kernel.Abstractions`, not runtime

### Why These Patterns?

**Envelope Pattern:**
- Separates metadata (tracking, routing) from payload (business data)
- Enables distributed tracing via correlation/causation IDs
- Grid context propagation (NodeId, StudioId, Environment)
- Immutable design prevents accidental mutations

**Middleware Pipeline:**
- DRY principle - implement cross-cutting logic once
- Onion pattern - each layer wraps the next
- Built-in middleware (correlation, telemetry, logging)
- Extensible via custom middleware

**Grid Context Integration:**
- Automatic context propagation across Node boundaries
- Correlation tracking for distributed operations
- Causation chains for cause-effect relationships
- Baggage for custom metadata propagation

**Transactional Outbox:**
- Exactly-once message delivery semantics
- Database transaction consistency
- Eventual delivery guarantees
- Poison message handling

---

## 📦 Project Structure

```
HoneyDrunk.Transport/
├── HoneyDrunk.Transport/              # Core abstractions and pipeline
│   ├── Abstractions/                  # Contracts and interfaces
│   ├── Primitives/                    # Building blocks
│   ├── Configuration/                 # Settings and options
│   ├── Pipeline/                      # Middleware execution
│   ├── Context/                       # Grid context integration
│   ├── Health/                        # Health monitoring
│   ├── Metrics/                       # Metrics collection
│   ├── Outbox/                        # Transactional outbox
│   ├── Telemetry/                     # OpenTelemetry integration
│   └── DependencyInjection/          # Service registration
│
├── HoneyDrunk.Transport.InMemory/    # In-memory transport
│   ├── InMemoryBroker.cs             # In-process message broker
│   ├── InMemoryTransportPublisher.cs # Publisher implementation
│   └── InMemoryTransportConsumer.cs  # Consumer implementation
│
├── HoneyDrunk.Transport.StorageQueue/ # Azure Storage Queue transport
│   ├── StorageQueueOptions.cs        # Configuration
│   ├── StorageQueueSender.cs         # Publisher implementation
│   └── StorageQueueProcessor.cs      # Consumer implementation
│
├── HoneyDrunk.Transport.AzureServiceBus/ # Azure Service Bus transport
│   ├── AzureServiceBusOptions.cs     # Configuration
│   ├── ServiceBusTransportPublisher.cs # Publisher implementation
│   └── ServiceBusTransportConsumer.cs  # Consumer implementation
│
└── HoneyDrunk.Transport.Tests/       # Unit and integration tests
    ├── Core/                          # Core functionality tests
    ├── InMemory/                      # InMemory transport tests
    └── Support/                       # Test helpers
```

---

## 🔗 Relationships

### Upstream Dependencies

- **HoneyDrunk.Kernel.Abstractions** - Core abstractions (IGridContext, CorrelationId, TimeProvider)
- **Microsoft.Extensions.*** - DI, Hosting, Configuration abstractions
- **System.Text.Json** - Default message serialization

### Downstream Consumers

Applications using HoneyDrunk.Transport:

- **Order Processing Services** - Event-driven order workflows
- **Payment Services** - Reliable payment processing
- **Notification Services** - Email/SMS dispatching
- **Integration Services** - System-to-system messaging

---

## 📖 Additional Resources

### Official Documentation
- [README.md](../README.md) - Project overview and quick start
- [CHANGELOG.md](../HoneyDrunk.Transport/CHANGELOG.md) - Version history and migration guides
- [.github/copilot-instructions.md](../.github/copilot-instructions.md) - Coding standards

### Related Projects
- [HoneyDrunk.Kernel](https://github.com/HoneyDrunkStudios/HoneyDrunk.Kernel) - Core Grid primitives
- [HoneyDrunk.Standards](https://github.com/HoneyDrunkStudios/HoneyDrunk.Standards) - Analyzers and conventions

### External References
- [Azure Service Bus](https://learn.microsoft.com/azure/service-bus-messaging/) - Enterprise messaging
- [Azure Storage Queues](https://learn.microsoft.com/azure/storage/queues/) - Simple queuing
- [OpenTelemetry .NET](https://opentelemetry.io/docs/languages/net/) - Observability

---

## 💡 Motto

**"Send messages, not worries."** - Focus on business logic, not infrastructure.

---

*Last Updated: 2025-11-22*  
*Version: 0.2.0*  
*Target Framework: .NET 10.0*
