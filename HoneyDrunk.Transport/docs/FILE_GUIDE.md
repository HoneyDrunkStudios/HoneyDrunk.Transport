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
- **Runtime**: Unified orchestrator for consumer lifecycle management
- **Topology**: Transport adapter capability contracts

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
| 📋 **Abstractions** | [Abstractions.md](Abstractions.md) | Core contracts (ITransportEnvelope, ITransportPublisher, ITransportConsumer, IMessageHandler, ITransportTopology) |
| 🔧 **Primitives** | [Primitives.md](Primitives.md) | Building blocks (TransportEnvelope, EnvelopeFactory, message serialization) |
| ⚙️ **Configuration** | [Configuration.md](Configuration.md) | Settings (RetryOptions, BackoffStrategy, error handling strategies) |
| 🔄 **Pipeline** | [Pipeline.md](Pipeline.md) | Message processing chain (middleware, handlers, execution flow, TransportExecutionContext) |
| 🌐 **Context** | [Context.md](Context.md) | Grid context integration (IGridContext, GridContextFactory, propagation) |
| 🏃 **Runtime** | [Runtime.md](Runtime.md) | Unified lifecycle orchestration (ITransportRuntime, TransportRuntimeHost) |
| ❤️ **Health** | [Health.md](Health.md) | Health monitoring (ITransportHealthContributor, health checks) |
| 📈 **Metrics** | [Metrics.md](Metrics.md) | Metrics collection (ITransportMetrics, telemetry integration) |
| 📤 **Outbox** | [Outbox.md](Outbox.md) | Transactional outbox pattern (IOutboxStore, IOutboxDispatcher, DefaultOutboxDispatcher, reliable messaging) |
| 🔌 **DI** | [DependencyInjection.md](DependencyInjection.md) | Service registration (fluent builders, extensions) |

### 🔸 Transport Implementations

| Document | Description |
|----------|-------------|
| [InMemory.md](InMemory.md) | In-memory transport for testing (no external dependencies, InMemoryTopology) |
| [StorageQueue.md](StorageQueue.md) | Azure Storage Queue transport (cost-effective, high-volume, StorageQueueTopology) |
| [AzureServiceBus.md](AzureServiceBus.md) | Azure Service Bus transport (topics, sessions, advanced features, ServiceBusTopology) |

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

// Step 2: Register Transport (automatically registers TransportRuntimeHost)
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
app.Run();  // TransportRuntimeHost starts automatically as IHostedService
```

```csharp
// Publishing Messages with typed addressing
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
        
        // Publish event with Grid context and typed addressing
        var message = new OrderCreated(order.Id, order.CustomerId);
        var payload = serializer.Serialize(message);
        var envelope = factory.CreateEnvelopeWithGridContext<OrderCreated>(
            payload,
            gridContext);
        
        // Use typed endpoint addressing with session, partition, TTL, scheduling
        var destination = EndpointAddress.Create(
            name: "orders",
            address: "orders",
            sessionId: order.CustomerId.ToString(),  // Session-based ordering
            partitionKey: order.Region,              // Partition by region
            timeToLive: TimeSpan.FromHours(24),      // Message expiration
            scheduledEnqueueTime: DateTimeOffset.UtcNow.AddMinutes(5)); // Delayed delivery
        
        await publisher.PublishAsync(envelope, destination, ct);
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
        catch (TransientException ex)
        {
            return MessageProcessingResult.Retry;  // Retry with backoff
        }
        catch (ValidationException ex)
        {
            return MessageProcessingResult.DeadLetter;  // Move to DLQ
        }
    }
}
```

### Outbox Pattern

```csharp
// Assumes AddHoneyDrunkTransportCore() and transport adapter are already registered

// Register outbox store (implement IOutboxStore for your database)
builder.Services.AddSingleton<IOutboxStore, SqlServerOutboxStore>();

// Register DefaultOutboxDispatcher as background service
builder.Services.AddHostedService<DefaultOutboxDispatcher>();

// Configure dispatcher options
builder.Services.Configure<OutboxDispatcherOptions>(options =>
{
    options.PollInterval = TimeSpan.FromSeconds(5);
    options.BatchSize = 100;
    options.MaxRetryAttempts = 5;
    options.BaseRetryDelay = TimeSpan.FromSeconds(1);
    options.MaxRetryDelay = TimeSpan.FromMinutes(5);
});
```

---

## 🔷 Design Philosophy

### Core Principles

1. **Transport-agnostic** - Swap brokers without changing business logic
2. **Grid-aware** - First-class integration with Kernel for distributed context
3. **Middleware pipeline** - Cross-cutting concerns without handler pollution
4. **Immutable envelopes** - Thread-safe, testable message wrappers
5. **Abstractions-only** - Depend on `HoneyDrunk.Kernel.Abstractions`, not runtime
6. **Runtime orchestration** - Unified lifecycle management via TransportRuntimeHost
7. **Topology validation** - Capability contracts prevent silent feature failures

### Why These Patterns?

**Envelope Pattern:**
- Separates metadata (tracking, routing) from payload (business data)
- Enables distributed tracing via correlation/causation IDs
- Grid context propagation (NodeId, StudioId, Environment, TenantId, ProjectId)
- Immutable design prevents accidental mutations

**Middleware Pipeline:**
- DRY principle - implement cross-cutting logic once
- Onion pattern - each layer wraps the next
- Built-in middleware (correlation, telemetry, logging)
- Extensible via custom middleware
- TransportExecutionContext for middleware-specific metadata

**Grid Context Integration:**
- Automatic context propagation across Node boundaries
- Correlation tracking for distributed operations
- Causation chains for cause-effect relationships
- Baggage for custom metadata propagation
- Multi-tenancy support (TenantId, ProjectId)

**Transactional Outbox:**
- Exactly-once message delivery semantics
- Database transaction consistency
- Eventual delivery guarantees
- Poison message handling
- DefaultOutboxDispatcher with exponential backoff

**Runtime Orchestration:**
- Unified consumer lifecycle via TransportRuntimeHost
- Thread-safe start/stop with semaphore locking
- Health contributor aggregation
- Integration with ASP.NET Core IHostedService

**Topology Capabilities:**
- ITransportTopology declares adapter features
- Runtime validation prevents unsupported feature usage
- Fail-fast configuration errors
- Clear documentation of adapter limitations

### Current Feature Set

**Core Infrastructure:**
- ✅ **ITransportRuntime** - Unified lifecycle orchestrator (TransportRuntimeHost)
- ✅ **ITransportTopology** - Capability contracts (ServiceBus, StorageQueue, InMemory)
- ✅ **IEndpointAddress** - Typed metadata (SessionId, PartitionKey, TTL, ScheduledEnqueueTime)

**Message Processing:**
- ✅ **PublishOptions** - TTL, scheduling, priority, partitioning support
- ✅ **TransportExecutionContext** - Middleware context with broker properties
- ✅ **DefaultOutboxDispatcher** - Background service with exponential backoff
- ✅ **MessageProcessingFailure** - Structured error metadata
- ✅ **Error Handling Strategies** - Configurable, map-based, and default strategies

**Production Features:**
- ✅ Core abstraction layer (`ITransportEnvelope`, `ITransportPublisher`, `ITransportConsumer`, `IMessageHandler`)
- ✅ Middleware pipeline with Grid context propagation
- ✅ Retry/backoff strategies with configurable error handling
- ✅ InMemory, StorageQueue, ServiceBus adapters with topology
- ✅ Health monitoring abstractions
- ✅ Runtime orchestration with TransportRuntimeHost
- ✅ Outbox dispatcher implementation
- ✅ Typed endpoint addressing with metadata
- ✅ High-performance logging with LoggerMessage source generators

See [ROADMAP.md](ROADMAP.md) for complete implementation details and [ARCHITECTURAL_GAPS.md](ARCHITECTURAL_GAPS.md) for status tracking.

---

## 📦 Project Structure

```
HoneyDrunk.Transport/
├── HoneyDrunk.Transport/              # Core abstractions and pipeline
│   ├── Abstractions/                  # Contracts and interfaces
│   │   ├── ITransportEnvelope.cs
│   │   ├── ITransportPublisher.cs
│   │   ├── ITransportConsumer.cs
│   │   ├── ITransportRuntime.cs
│   │   ├── ITransportTopology.cs
│   │   ├── IEndpointAddress.cs
│   │   ├── PublishOptions.cs
│   │   ├── MessagePriority.cs
│   │   └── MessageProcessingFailure.cs
│   ├── Primitives/                    # Building blocks
│   │   ├── TransportEnvelope.cs
│   │   ├── EnvelopeFactory.cs
│   │   └── EndpointAddress.cs
│   ├── Configuration/                 # Settings and options
│   │   ├── DefaultErrorHandlingStrategy.cs
│   │   ├── ConfigurableErrorHandlingStrategy.cs
│   │   └── ExceptionTypeMapStrategy.cs
│   ├── Pipeline/                      # Middleware execution
│   │   ├── TransportExecutionContext.cs
│   │   └── MessagePipeline.cs
│   ├── Context/                       # Grid context integration
│   ├── Runtime/                       # Lifecycle orchestration
│   │   ├── ITransportRuntime.cs
│   │   └── TransportRuntimeHost.cs
│   ├── Health/                        # Health monitoring
│   ├── Metrics/                       # Metrics collection
│   ├── Outbox/                        # Transactional outbox
│   │   ├── IOutboxStore.cs
│   │   ├── IOutboxDispatcher.cs
│   │   ├── DefaultOutboxDispatcher.cs
│   │   └── OutboxDispatcherOptions.cs
│   ├── Telemetry/                     # OpenTelemetry integration
│   └── DependencyInjection/          # Service registration
│
├── HoneyDrunk.Transport.InMemory/    # In-memory transport
│   ├── InMemoryBroker.cs
│   ├── InMemoryTopology.cs
│   ├── InMemoryTransportPublisher.cs
│   └── InMemoryTransportConsumer.cs
│
├── HoneyDrunk.Transport.StorageQueue/ # Azure Storage Queue transport
│   ├── StorageQueueOptions.cs
│   ├── StorageQueueTopology.cs
│   ├── StorageQueueSender.cs
│   └── StorageQueueProcessor.cs
│
├── HoneyDrunk.Transport.AzureServiceBus/ # Azure Service Bus transport
│   ├── AzureServiceBusOptions.cs
│   ├── ServiceBusTopology.cs
│   ├── ServiceBusTransportPublisher.cs
│   └── ServiceBusTransportConsumer.cs
│
└── HoneyDrunk.Transport.Tests/       # Unit and integration tests
    ├── Core/                          # Core functionality tests
    ├── InMemory/                      # InMemory transport tests
    └── Support/                       # Test helpers
```

---

## 🆕 Key Features

### Runtime Orchestration
- **TransportRuntimeHost** - Unified consumer lifecycle management
- Automatic registration as `IHostedService`
- Thread-safe start/stop operations
- Health contributor aggregation
- High-performance logging with LoggerMessage source generators

### Topology Capabilities
- **ITransportTopology** interface for declaring adapter features
- `ServiceBusTopology` - Full feature set (topics, sessions, transactions, ordering, scheduled messages)
- `StorageQueueTopology` - Limited features (queue-only, no sessions, no transactions)
- `InMemoryTopology` - Testing features (topics, subscriptions, ordering)
- Runtime validation prevents silent feature failures

### Typed Endpoint Addressing
- Typed properties: `SessionId`, `PartitionKey`, `ScheduledEnqueueTime`, `TimeToLive`
- Fallback to `AdditionalProperties` for adapter-specific metadata
- Cleaner API: `EndpointAddress.Create(name, address, sessionId: "123", timeToLive: TimeSpan.FromHours(24))`

### Transport Execution Context
- **TransportExecutionContext** for middleware (richer than MessageContext)
- `BrokerProperties` dictionary for adapter-specific metadata
- `RetryAttempt` and `ProcessingDuration` tracking
- `ToMessageContext()` conversion for handler invocation

### Outbox Pattern
- **DefaultOutboxDispatcher** as production-ready BackgroundService
- Exponential backoff retry strategy
- Configurable via `OutboxDispatcherOptions`
- Poison message handling after max retries
- Circuit breaker for repeated failures
- **Storage Integration**: Outbox storage is provided by application-level implementations of `IOutboxStore`, which often integrate with HoneyDrunk.Data provider packages (e.g., SQL Server, PostgreSQL), but Transport does not reference Data directly

### Error Handling
- **ConfigurableErrorHandlingStrategy** - Fluent rule-based configuration
- **ExceptionTypeMapStrategy** - Dictionary-based exception mapping with builder
- **DefaultErrorHandlingStrategy** - Enhanced with high-performance logging
- **MessageProcessingFailure** - Structured error context with Reason, Category, Exception, Metadata

---

## 🔗 Relationships

### Upstream Dependencies

- **HoneyDrunk.Kernel.Abstractions** - Core abstractions (IGridContext, CorrelationId, TimeProvider)
- **Microsoft.Extensions.Logging.Abstractions** - LoggerMessage source generators
- **Microsoft.Extensions.*** - DI, Hosting, Configuration abstractions
- **System.Text.Json** - Default message serialization

**Note on Data Integration:** Transport defines `IOutboxStore` for transactional outbox patterns, but does not depend on HoneyDrunk.Data. Application-level implementations of `IOutboxStore` often integrate with Data provider packages (e.g., SQL Server, PostgreSQL) to provide storage capabilities.


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
- [ROADMAP.md](ROADMAP.md) - Implementation tracking
- [.github/copilot-instructions.md](../.github/copilot-instructions.md) - Coding standards
- [ARCHITECTURAL_GAPS.md](ARCHITECTURAL_GAPS.md) - Design status and future roadmap

### Related Projects
- [HoneyDrunk.Kernel](https://github.com/HoneyDrunkStudios/HoneyDrunk.Kernel) - Core Grid primitives
- [HoneyDrunk.Standards](https://github.com/HoneyDrunkStudios/HoneyDrunk.Standards) - Analyzers and conventions

### External References
- [Azure Service Bus](https://learn.microsoft.com/azure/service-bus-messaging/) - Enterprise messaging
- [Azure Storage Queues](https://learn.microsoft.com/azure/storage/queues/) - Simple queuing
- [OpenTelemetry .NET](https://opentelemetry.io/docs/languages/net/) - Observability
- [LoggerMessage Source Generators](https://learn.microsoft.com/dotnet/core/extensions/logger-message-generator) - High-performance logging

---

## 💡 Motto

**"Send messages, not worries."** - Focus on business logic, not infrastructure.

---

*Last Updated: 2025-01-22*  
*Target Framework: .NET 10.0*  
