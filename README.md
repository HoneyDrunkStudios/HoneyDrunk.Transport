# HoneyDrunk.Transport

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/10.0)

> **Reliable messaging and outbox infrastructure for the Hive** - Transport unifies brokers, queues, and event buses under one contract ensuring delivery, order, and idempotence. It powers communication between Nodes—Data, Pulse, Vault, and beyond—so every message finds its way.

**Signal Quote:** *"Every message finds its way."*

---

## 📦 What Is This?

HoneyDrunk.Transport is the **messaging backbone** of HoneyDrunk.OS ("the Hive"). It provides a transport-agnostic abstraction layer over different message brokers with built-in resilience, observability, and exactly-once semantics:

- ✅ **Transport Abstraction** - Unified interface for Azure Service Bus, RabbitMQ, Kafka, in-memory, and more
- ✅ **Middleware Pipeline** - Onion-style processing with correlation, telemetry, logging, and retry
- ✅ **Envelope Pattern** - Immutable message wrapping with correlation/causation tracking
- ✅ **Transactional Outbox** - Exactly-once processing with database transactions
- ✅ **Kernel Integration** - Uses `IClock`, `IIdGenerator`, `IKernelContext` for deterministic, testable messaging
- ✅ **Framework Integration** - Extends Microsoft.Extensions, integrates seamlessly with ASP.NET Core
- ✅ **Blob Fallback for Service Bus** - Persist failed publishes to Azure Blob Storage for later replay

---

## 🚀 Quick Start

### Installation

```xml
<ItemGroup>
  <!-- Core Transport -->
  <PackageReference Include="HoneyDrunk.Transport" Version="0.1.0" />
  
  <!-- Azure Service Bus Provider -->
  <PackageReference Include="HoneyDrunk.Transport.AzureServiceBus" Version="0.1.0" />
  
  <!-- Azure Storage Queue Provider -->
  <PackageReference Include="HoneyDrunk.Transport.StorageQueue" Version="0.1.0" />
  
  <!-- In-Memory Provider (for testing) -->
  <PackageReference Include="HoneyDrunk.Transport.InMemory" Version="0.1.0" />
</ItemGroup>
```

### Register Transport Services

```csharp
using HoneyDrunk.Transport.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Register Transport core (includes Kernel defaults)
builder.Services.AddHoneyDrunkTransportCore(options =>
{
    options.EnableTelemetry = true;
    options.EnableLogging = true;
    options.EnableCorrelation = true;
});

// Option 1: Add Azure Service Bus transport
builder.Services.AddHoneyDrunkServiceBusTransport(options =>
{
    options.FullyQualifiedNamespace = "mynamespace.servicebus.windows.net";
    options.EntityType = ServiceBusEntityType.Queue;
    options.Address = "my-queue";
    options.AutoComplete = true;

    // Optional: enable Blob fallback for publish failures
    // options.BlobFallback.Enabled = true;
    // options.BlobFallback.ConnectionString = builder.Configuration["Blob:ConnectionString"];
    // options.BlobFallback.ContainerName = "transport-fallback";
    // options.BlobPrefix = "servicebus";
});

// Option 2: Add Azure Storage Queue transport
builder.Services.AddHoneyDrunkTransportStorageQueue(
    connectionString: builder.Configuration["StorageQueue:ConnectionString"]!,
    queueName: "orders")
    .WithMaxDequeueCount(5)
    .WithConcurrency(10);

// Register message handlers
builder.Services.AddMessageHandler<OrderCreatedEvent, OrderCreatedHandler>();

var app = builder.Build();
app.Run();
```

---

## 🎯 Features

### 🔍 Core Components

| Component | Purpose | Key Types |
|-----------|---------|-----------|
| **Transport Abstraction** | Unified publisher/consumer interface | `ITransportPublisher`, `ITransportConsumer` |
| **Message Pipeline** | Middleware execution engine | `IMessagePipeline`, `IMessageMiddleware` |
| **Envelope System** | Immutable message wrapping | `ITransportEnvelope`, `EnvelopeFactory` |
| **Kernel Context** | Correlation/causation tracking | `IKernelContextFactory`, `KernelContext` |
| **Serialization** | Pluggable message serialization | `IMessageSerializer`, `JsonMessageSerializer` |
| **Outbox Pattern** | Transactional outbox support | `IOutboxStore`, `IOutboxDispatcher` |

### 🔗 Kernel Integration

HoneyDrunk.Transport **extends** HoneyDrunk.Kernel with messaging primitives:

| Kernel Service | How Transport Uses It |
|----------------|----------------------|
| `IIdGenerator` | Message ID generation (ULID) |
| `IClock` | Deterministic message timestamps |
| `IKernelContext` | Correlation/causation propagation |
| `IMetricsCollector` | Message processing metrics |
| `ILogger<T>` | Structured logging throughout |

### 🚀 Available Transports

| Transport | Package | Status |
|-----------|---------|--------|
| **Azure Service Bus** | `HoneyDrunk.Transport.AzureServiceBus` | ✅ Available |
| **Azure Storage Queue** | `HoneyDrunk.Transport.StorageQueue` | ✅ Available |
| **In-Memory** | `HoneyDrunk.Transport.InMemory` | ✅ Available (Testing) |
| **RabbitMQ** | `HoneyDrunk.Transport.RabbitMQ` | 🚧 Planned |
| **Kafka** | `HoneyDrunk.Transport.Kafka` | 🚧 Planned |

---

## 📖 Usage Examples

### Publishing Messages

```csharp
using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.Primitives;

public class OrderService(
    ITransportPublisher publisher,
    EnvelopeFactory envelopeFactory,
    IMessageSerializer serializer)
{
    public async Task CreateOrderAsync(CreateOrderCommand command)
    {
        // Create order...
        
        // Publish event
        var @event = new OrderCreatedEvent { OrderId = orderId, Total = total };
        var payload = serializer.Serialize(@event);
        var envelope = envelopeFactory.CreateEnvelope<OrderCreatedEvent>(
            payload,
            correlationId: command.CorrelationId);
        
        await publisher.PublishAsync(
            envelope,
            new EndpointAddress("orders-topic"),
            cancellationToken);
    }
}
```

### Handling Messages

```csharp
using HoneyDrunk.Transport.Abstractions;

public class OrderCreatedHandler : IMessageHandler<OrderCreatedEvent>
{
    private readonly ILogger<OrderCreatedHandler> _logger;
    
    public OrderCreatedHandler(ILogger<OrderCreatedHandler> logger)
    {
        _logger = logger;
    }
    
    public async Task HandleAsync(
        OrderCreatedEvent message,
        MessageContext context,
        CancellationToken cancellationToken)
    {
        // Access kernel context for correlation tracking
        if (context.Properties.TryGetValue("KernelContext", out var ctxObj)
            && ctxObj is IKernelContext kernelContext)
        {
            _logger.LogInformation(
                "Processing order {OrderId} with CorrelationId {CorrelationId}",
                message.OrderId,
                kernelContext.CorrelationId);
        }
        
        // Process the event
        await SendConfirmationEmailAsync(message.OrderId, cancellationToken);
    }
}
```

### Custom Middleware

```csharp
using HoneyDrunk.Transport.Pipeline;

public class ValidationMiddleware : IMessageMiddleware
{
    public async Task InvokeAsync(
        ITransportEnvelope envelope,
        MessageContext context,
        Func<Task> next,
        CancellationToken cancellationToken)
    {
        // Validate envelope
        if (string.IsNullOrEmpty(envelope.MessageType))
        {
            throw new MessageHandlerException(
                "MessageType is required",
                MessageProcessingResult.DeadLetter);
        }
        
        // Continue pipeline
        await next();
    }
}

// Register in DI
services.AddMessageMiddleware<ValidationMiddleware>();
```

### Transactional Outbox

```csharp
using HoneyDrunk.Transport.Outbox;

public class OrderService(IOutboxStore outboxStore, IDbContext dbContext)
{
    public async Task CreateOrderAsync(CreateOrderCommand command)
    {
        await using var transaction = await dbContext.BeginTransactionAsync();
        
        try
        {
            // Save order to database
            var order = new Order { /* ... */ };
            await dbContext.Orders.AddAsync(order);
            
            // Save message to outbox (same transaction)
            var envelope = CreateOrderCreatedEnvelope(order);
            await outboxStore.SaveAsync(
                envelope,
                new EndpointAddress("orders-topic"),
                cancellationToken);
            
            await transaction.CommitAsync();
            
            // Background dispatcher will publish from outbox
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
```

### Azure Service Bus Blob Fallback (optional)

```csharp
// Enable fallback to Azure Blob Storage when publish fails
builder.Services.AddHoneyDrunkServiceBusTransport(options =>
{
    options.ConnectionString = builder.Configuration["ServiceBus:ConnectionString"];    
    options.Address = "orders";

    options.BlobFallback.Enabled = true;
    options.BlobFallback.ConnectionString = builder.Configuration["Blob:ConnectionString"]; // or use AccountUrl with MSI
    options.BlobFallback.ContainerName = "transport-fallback";
    options.BlobFallback.BlobPrefix = "servicebus";
});
```

Behavior: on publish exception, the publisher saves the full envelope + destination metadata to blob JSON and suppresses the error if the upload succeeds. If blob upload fails too, the original error is rethrown.

### Azure Storage Queue with Poison Handling

```csharp
using HoneyDrunk.Transport.Abstractions;

// Handler with explicit error control
public class PaymentProcessingHandler : IMessageHandler<ProcessPaymentCommand>
{
    private readonly IPaymentGateway _gateway;
    private readonly ILogger<PaymentProcessingHandler> _logger;
    
    public PaymentProcessingHandler(
        IPaymentGateway gateway,
        ILogger<PaymentProcessingHandler> logger)
    {
        _gateway = gateway;
        _logger = logger;
    }
    
    public async Task<MessageProcessingResult> HandleAsync(
        ProcessPaymentCommand message,
        MessageContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            // Check dequeue count to detect repeated failures
            if (context.DeliveryCount > 3)
            {
                _logger.LogWarning(
                    "Payment {PaymentId} has been retried {Count} times",
                    message.PaymentId,
                    context.DeliveryCount);
            }
            
            var result = await _gateway.ProcessPaymentAsync(
                message.PaymentId,
                message.Amount,
                cancellationToken);
            
            if (result.IsSuccess)
            {
                return MessageProcessingResult.Success; // Message deleted from queue
            }
            else if (result.IsTransientError)
            {
                // Transient error (timeout, rate limit) - retry
                _logger.LogWarning(
                    "Transient error processing payment {PaymentId}: {Error}",
                    message.PaymentId,
                    result.ErrorMessage);
                
                return MessageProcessingResult.Retry; // Message becomes visible again
            }
            else
            {
                // Permanent error (invalid card, insufficient funds) - dead letter
                _logger.LogError(
                    "Permanent error processing payment {PaymentId}: {Error}",
                    message.PaymentId,
                    result.ErrorMessage);
                
                return MessageProcessingResult.DeadLetter; // Moved to poison queue
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing payment {PaymentId}", message.PaymentId);
            
            // After MaxDequeueCount (default: 5), message automatically moves to poison queue
            return MessageProcessingResult.Retry;
        }
    }
}
```

---

## 🧪 Testing & Validation

### In-Memory Transport for Tests

```csharp
using HoneyDrunk.Transport.InMemory;
using Xunit;

public class OrderServiceTests
{
    [Fact]
    public async Task CreateOrder_PublishesOrderCreatedEvent()
    {
        // Arrange
        var broker = new InMemoryBroker();
        var publisher = new InMemoryTransportPublisher(broker, logger);
        var service = new OrderService(publisher, /* ... */);
        
        var messagesReceived = new List<ITransportEnvelope>();
        broker.Subscribe("orders-topic", (envelope, ct) =>
        {
            messagesReceived.Add(envelope);
            return Task.CompletedTask;
        });
        
        // Act
        await service.CreateOrderAsync(new CreateOrderCommand { /* ... */ });
        
        // Assert
        Assert.Single(messagesReceived);
        Assert.Equal("OrderCreatedEvent", messagesReceived[0].MessageType);
    }
}
```

### Testing with Fixed Time

```csharp
using HoneyDrunk.Kernel.Abstractions.Time;

public class EnvelopeFactoryTests
{
    [Fact]
    public void CreateEnvelope_UsesFixedTimestamp()
    {
        // Arrange
        var fixedTime = new DateTimeOffset(2025, 1, 15, 12, 0, 0, TimeSpan.Zero);
        var clock = new FixedClock(fixedTime);
        var idGenerator = new TestIdGenerator("test-id");
        var factory = new EnvelopeFactory(idGenerator, clock);
        
        // Act
        var envelope = factory.CreateEnvelope<TestMessage>(payload);
        
        // Assert
        Assert.Equal(fixedTime, envelope.Timestamp);
        Assert.Equal("test-id", envelope.MessageId);
    }
}
```

---

## 🛠️ Configuration

### Transport Core Options

```csharp
builder.Services.AddHoneyDrunkTransportCore(options =>
{
    options.EndpointName = "my-service";
    options.Address = "my-queue";
    options.EnableTelemetry = true;
    options.EnableLogging = true;
    options.EnableCorrelation = true;
    options.MaxConcurrency = 10;
    options.PrefetchCount = 20;
});
```

### Azure Service Bus Options

```csharp
builder.Services.AddHoneyDrunkServiceBusTransport(options =>
{
    // Connection
    options.FullyQualifiedNamespace = "mynamespace.servicebus.windows.net";
    options.ConnectionString = config["ServiceBus:ConnectionString"];
    
    // Entity
    options.EntityType = ServiceBusEntityType.Topic;
    options.Address = "orders-topic";
    options.SubscriptionName = "order-processor";
    
    // Processing
    options.AutoComplete = true;
    options.SessionEnabled = false;
    options.MaxConcurrency = 10;
    options.PrefetchCount = 20;
    options.MessageLockDuration = TimeSpan.FromMinutes(5);
    
    // Retry
    options.ServiceBusRetry.Mode = ServiceBusRetryMode.Exponential;
    options.ServiceBusRetry.MaxRetries = 3;
    options.ServiceBusRetry.Delay = TimeSpan.FromSeconds(0.8);
    options.ServiceBusRetry.MaxDelay = TimeSpan.FromMinutes(1);
    
    // Dead Letter
    options.EnableDeadLetterQueue = true;
    options.MaxDeliveryCount = 10;
    
    // Blob fallback (optional)
    // options.BlobFallback.Enabled = true;
    // options.BlobFallback.ConnectionString = config["Blob:ConnectionString"]; // or AccountUrl + MSI
    // options.BlobFallback.ContainerName = "transport-fallback";
    // options.BlobFallback.BlobPrefix = "servicebus";
});
```

### Azure Storage Queue Options

```csharp
builder.Services.AddHoneyDrunkTransportStorageQueue(options =>
{
    // Connection
    options.ConnectionString = config["StorageQueue:ConnectionString"];
    options.QueueName = "orders";
    options.CreateIfNotExists = true;
    
    // Message Settings
    options.Base64EncodePayload = true;
    options.MessageTimeToLive = TimeSpan.FromDays(7);
    options.VisibilityTimeout = TimeSpan.FromSeconds(30);
    
    // Processing
    options.MaxConcurrency = 5;
    options.PrefetchMaxMessages = 16;
    
    // Poison Handling
    options.MaxDequeueCount = 5;
    options.PoisonQueueName = "orders-poison";
    
    // Polling
    options.EmptyQueuePollingInterval = TimeSpan.FromSeconds(1);
    options.MaxPollingInterval = TimeSpan.FromSeconds(5);
    
    // Observability
    options.EnableTelemetry = true;
    options.EnableLogging = true;
    options.EnableCorrelation = true;
});

// Or use fluent configuration
builder.Services
    .AddHoneyDrunkTransportStorageQueue(
        config["StorageQueue:ConnectionString"]!,
        "orders")
    .WithMaxDequeueCount(3)
    .WithVisibilityTimeout(TimeSpan.FromSeconds(60))
    .WithPrefetchCount(32)
    .WithConcurrency(10)
    .WithPoisonQueue("orders-dlq");
```

### Repository Layout

```
HoneyDrunk.Transport/
 ├── HoneyDrunk.Transport/                    # Core abstractions & pipeline
 │   ├── Abstractions/                        # Contracts & interfaces
 │   ├── Pipeline/                            # Middleware execution engine
 │   ├── Configuration/                       # Options & settings
 │   ├── Context/                             # Kernel context integration
 │   ├── Primitives/                          # Envelope & factory
 │   ├── Outbox/                              # Transactional outbox
 │   └── DependencyInjection/                 # DI registration
 ├── HoneyDrunk.Transport.AzureServiceBus/    # Azure Service Bus provider
 ├── HoneyDrunk.Transport.StorageQueue/       # Azure Storage Queue provider
 ├── HoneyDrunk.Transport.InMemory/           # In-memory provider
 ├── HoneyDrunk.Transport.Tests/              # Test project
 ├── HoneyDrunk.Transport.slnx
 ├── .editorconfig
 └── .github/workflows/
     ├── validate-pr.yml
     └── publish.yml
```

#### Storage Queue vs Service Bus

**When to use Storage Queue:**
- ✅ Simple queue-based messaging
- ✅ Cost-effective for high-volume scenarios
- ✅ No need for advanced features (sessions, transactions)
- ✅ Message size < 64KB
- ✅ At-least-once delivery is acceptable

**When to use Service Bus:**
- ✅ Need topics/subscriptions (pub/sub)
- ✅ Require sessions for ordered processing
- ✅ Need transactional receive
- ✅ Message size up to 1MB (or 100MB with Premium)
- ✅ Dead-letter queue with automatic retry policies
- ✅ Advanced features (duplicate detection, message deferral)

**Storage Queue Limitations:**
- ⚠️ Maximum message size: ~64KB (after base64 encoding)
- ⚠️ No native dead-letter queue (implemented via poison queue pattern)
- ⚠️ No FIFO guarantees beyond queue semantics
- ⚠️ No built-in duplicate detection
- ⚠️ No transactional receive
- ⚠️ Batch operations are not atomic (parallelized individual sends)

For large payloads, consider storing data in Blob Storage and sending a reference/pointer message.
