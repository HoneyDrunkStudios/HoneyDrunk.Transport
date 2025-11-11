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

---

## 🚀 Quick Start

### Installation

```xml
<ItemGroup>
  <!-- Core Transport -->
  <PackageReference Include="HoneyDrunk.Transport" Version="0.1.0" />
  
  <!-- Azure Service Bus Provider -->
  <PackageReference Include="HoneyDrunk.Transport.AzureServiceBus" Version="0.1.0" />
  
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

// Add Azure Service Bus transport
builder.Services.AddHoneyDrunkServiceBusTransport(options =>
{
    options.FullyQualifiedNamespace = "mynamespace.servicebus.windows.net";
    options.EntityType = ServiceBusEntityType.Queue;
    options.Address = "my-queue";
    options.AutoComplete = true;
});

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
});
```

### Retry Middleware

```csharp
builder.Services.AddMessageMiddleware(sp => 
    new RetryMiddleware(
        sp.GetRequiredService<ILogger<RetryMiddleware>>(),
        maxAttempts: 3));
```

---

## 🧱 Architecture

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
 ├── HoneyDrunk.Transport.InMemory/           # In-memory provider
 ├── HoneyDrunk.Transport.Tests/              # Test project
 ├── HoneyDrunk.Transport.slnx
 ├── .editorconfig
 └── .github/workflows/
     ├── validate-pr.yml
     └── publish.yml
```

### Design Philosophy

- **Transport Agnostic** – One interface, many brokers
- **Middleware First** – Composable, testable processing pipeline
- **Kernel Integrated** – Built on HoneyDrunk.Kernel primitives
- **Exactly-Once** – Transactional outbox for guaranteed delivery
- **Observable** – Telemetry, metrics, and distributed tracing built-in

### Production-Ready Features

HoneyDrunk.Transport is built with production reliability and safety in mind:

- **Thread-Safe Lifecycle** – All Start/Stop/Dispose operations properly synchronized with `SemaphoreSlim`
- **Concurrent Disposal Safety** – Uses `Interlocked.Exchange` to prevent double-disposal race conditions
- **Guaranteed Resource Cleanup** – Try-finally patterns ensure resources are always disposed, even on errors
- **Immutable Collections** – Thread-safe enumeration with `ImmutableList<T>` for concurrent scenarios
- **Credential Caching** – `DefaultAzureCredential` singleton prevents expensive re-initialization
- **Batch Safety** – Oversized message detection with clear error messages prevents silent data loss
- **Explicit Resource Management** – Structured disposal patterns with clear ownership semantics

These patterns ensure reliable operation under:
- Concurrent health check probes
- Graceful shutdown during deployments
- High-throughput message processing
- Circuit breaker scenarios
- Multi-threaded application hosts

### Middleware Pipeline

Messages flow through middleware in this order:

1. **CorrelationMiddleware** – Creates `IKernelContext` from envelope
2. **TelemetryMiddleware** – Starts distributed trace activity
3. **LoggingMiddleware** – Logs message processing lifecycle
4. **RetryMiddleware** – Enforces retry limits
5. **Custom Middleware** – Your application middleware
6. **Message Handler** – Final handler invocation

### Relationships

**Upstream Dependencies:**
- HoneyDrunk.Kernel (ID generation, time, context)
- HoneyDrunk.Standards (analyzers, conventions)

**Downstream Consumers:**
- HoneyDrunk.Data (outbox implementation)
- HoneyDrunk.Web.Rest (REST APIs with messaging)
- Service applications (order service, payment service, etc.)

---

## ⚙️ Build & Release

### CI/CD Integration

The package is validated and published automatically:

```yaml
# Validate on PR
- push → build + test
- pull_request → validate formatting and analyzers

# Publish on tag
- tag v* → build + test + pack + publish to NuGet
```

### Local Development

```sh
# Clone repository
git clone https://github.com/HoneyDrunkStudios/HoneyDrunk.Transport
cd HoneyDrunk.Transport

# Restore dependencies
dotnet restore

# Build solution
dotnet build

# Run tests
dotnet test HoneyDrunk.Transport.Tests/HoneyDrunk.Transport.Tests.csproj

# Pack packages
dotnet pack -c Release -o ./artifacts
```

---

## 📋 Testing Policy

- All tests live in `HoneyDrunk.Transport.Tests` — **none** in runtime projects
- Use `InMemoryBroker` for integration tests
- Tests **must** use `IClock` and `IIdGenerator` for deterministic runs
- CI gate: build fails if tests fail; coverage threshold optional

---

## 🤝 Contributing

Contributions are welcome! Please:

1. Read [.github/copilot-instructions.md](.github/copilot-instructions.md) for coding standards
2. Open an issue for discussion before major changes
3. Ensure all tests pass locally
4. Update documentation for new features

---

## 📄 License

This project is licensed under the [MIT License](LICENSE).

---

## 🐝 About HoneyDrunk Studios

HoneyDrunk.Transport is part of the **Hive** ecosystem - a collection of tools, libraries, and standards for building high-quality .NET applications.

**Other Projects:**
- 🚀 [HoneyDrunk.Kernel](https://github.com/HoneyDrunkStudios/HoneyDrunk.Kernel) - Foundational primitives
- 🚀 [HoneyDrunk.Standards](https://github.com/HoneyDrunkStudios/HoneyDrunk.Standards) - Build-transitive analyzers
- 🚧 HoneyDrunk.Data *(coming soon)* - Database abstractions
- 🚧 HoneyDrunk.Auth *(coming soon)* - Authentication/authorization

---

## 📞 Support

- **Questions:** Open a [discussion](https://github.com/HoneyDrunkStudios/HoneyDrunk.Transport/discussions)
- **Bugs:** File an [issue](https://github.com/HoneyDrunkStudios/HoneyDrunk.Transport/issues)
- **Feature Requests:** Open an [issue](https://github.com/HoneyDrunkStudios/HoneyDrunk.Transport/issues) with the `enhancement` label

---

## 🧃 Motto

**"Every message finds its way."**

---

<div align="center">

**Built with 🍯 by HoneyDrunk Studios**

[GitHub](https://github.com/HoneyDrunkStudios/HoneyDrunk.Transport) • [NuGet](https://www.nuget.org/packages/HoneyDrunk.Transport) • [Issues](https://github.com/HoneyDrunkStudios/HoneyDrunk.Transport/issues)

</div>
