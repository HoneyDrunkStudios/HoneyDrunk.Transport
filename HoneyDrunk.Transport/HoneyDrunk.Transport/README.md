# HoneyDrunk.Transport

[![NuGet](https://img.shields.io/nuget/v/HoneyDrunk.Transport.svg)](https://www.nuget.org/packages/HoneyDrunk.Transport/)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/10.0)

> **Transport-Agnostic Messaging for .NET** - Unified abstraction over message brokers with a middleware pipeline, Grid context propagation, and retry strategies.

## 📋 What Is This?

**HoneyDrunk.Transport** is the messaging backbone for HoneyDrunk.OS applications. It provides a **transport-agnostic** abstraction that lets you send and receive messages without coupling to specific broker implementations (Azure Service Bus, Storage Queue, etc.). Built on a middleware pipeline pattern similar to ASP.NET Core, it integrates seamlessly with **HoneyDrunk.Kernel** for Grid-aware distributed context propagation.

**Core Responsibilities:**
- ✅ **Publisher/Consumer Abstractions** - `ITransportPublisher` and `ITransportConsumer` for broker-agnostic messaging
- ✅ **Envelope Pattern** - Immutable message wrappers with correlation, causation, and Grid context
- ✅ **Middleware Pipeline** - Onion-style processing (GridContext → Telemetry → Logging → Handler)
- ✅ **Retry Strategies** - Configurable backoff (Fixed, Linear, Exponential)
- ✅ **Transactional Outbox** - Exactly-once delivery with database transactions
- ✅ **Grid Integration** - Uses Kernel `IGridContext` for correlation and causation across Nodes
- ✅ **Health & Metrics** - Built-in health contributors and telemetry integration

**Signal Quote:** *"Send messages, not worries."*

---

## 📦 What's Inside

### 📋 Abstractions
Core contracts for transport-agnostic messaging:
- **ITransportEnvelope** - Message wrapper with correlation, Grid context, and metadata
- **ITransportPublisher** - Publishes messages to destinations
- **ITransportConsumer** - Consumes messages and processes through pipeline
- **IMessageHandler\<TMessage\>** - Type-safe message processing
- **IMessageMiddleware** - Pipeline middleware for cross-cutting concerns
- **MessageContext** - Per-message execution context exposed to handlers
- **MessageProcessingResult** - Success, Retry, or DeadLetter outcomes

### 🔧 Primitives
Building blocks for message handling:
- **TransportEnvelope** - Immutable envelope implementation
- **EnvelopeFactory** - Creates envelopes with auto-generated IDs and Grid context
- **JsonMessageSerializer** - Default JSON serialization

### ⚙️ Configuration
Settings and retry policies:
- **TransportCoreOptions** - EnableTelemetry, EnableLogging, EnableCorrelation
- **RetryOptions** - MaxAttempts, InitialDelay, BackoffStrategy
- **BackoffStrategy** - Fixed, Linear, Exponential

### 🔄 Pipeline
Middleware execution system:
- **GridContextPropagationMiddleware** - Extracts Grid context from envelope
- **TelemetryMiddleware** - OpenTelemetry tracing
- **LoggingMiddleware** - Structured logging

### 🏃 Runtime
Unified lifecycle control:
- **ITransportRuntime** - Starts and stops all transport consumers, integrates with `IHostedService`

### 📤 Outbox
Transactional messaging pattern:
- **IOutboxStore** - Persists messages in database transactions
- **IOutboxDispatcher** - Background reliable delivery

> Implementations of `IOutboxStore` live in your application or Data Node. Transport only defines the contracts and dispatcher pattern.

---

## 📥 Installation

```bash
dotnet add package HoneyDrunk.Transport
```

```xml
<PackageReference Include="HoneyDrunk.Transport" Version="0.2.0" />
```

---

## 💡 Quick Example

*Assume `EnvelopeFactory`, `ITransportPublisher`, `IMessageSerializer`, and `IGridContext` are injected via DI.*

```csharp
// Setup
builder.Services.AddHoneyDrunkCoreNode(nodeDescriptor);
builder.Services.AddHoneyDrunkTransportCore(options =>
{
    options.EnableTelemetry = true;
    options.EnableLogging = true;
    options.EnableCorrelation = true;
})
.AddHoneyDrunkServiceBusTransport(/* ... */);

// Publish
public class OrderService(
    EnvelopeFactory factory,
    ITransportPublisher publisher,
    IMessageSerializer serializer,
    IGridContext gridContext)
{
    public async Task CreateOrderAsync(Order order, CancellationToken ct)
    {
        var message = new OrderCreated(order.Id, order.CustomerId);
        var payload = serializer.Serialize(message);
        
        var envelope = factory.CreateEnvelopeWithGridContext<OrderCreated>(payload, gridContext);
        await publisher.PublishAsync(envelope, EndpointAddress.Create("orders", "orders"), ct);
    }
}

// Handle
public class OrderCreatedHandler : IMessageHandler<OrderCreated>
{
    public async Task<MessageProcessingResult> HandleAsync(
        OrderCreated message,
        MessageContext context,
        CancellationToken ct)
    {
        var grid = context.GridContext;
        // Process with full Grid context (CorrelationId, NodeId, StudioId, etc.)
        return MessageProcessingResult.Success;
    }
}
```

---

## 🔗 Related Packages

- **[HoneyDrunk.Kernel](https://www.nuget.org/packages/HoneyDrunk.Kernel/)** - Runtime implementations
- **[HoneyDrunk.Transport.AzureServiceBus](https://www.nuget.org/packages/HoneyDrunk.Transport.AzureServiceBus/)** - Service Bus transport
- **[HoneyDrunk.Transport.StorageQueue](https://www.nuget.org/packages/HoneyDrunk.Transport.StorageQueue/)** - Storage Queue transport
- **[HoneyDrunk.Transport.InMemory](https://www.nuget.org/packages/HoneyDrunk.Transport.InMemory/)** - In-memory testing transport

---

## 📚 Documentation

- **[Complete File Guide](docs/FILE_GUIDE.md)** - Architecture documentation
- **[Abstractions](docs/Abstractions.md)** - Core contracts
- **[Pipeline](docs/Pipeline.md)** - Middleware system
- **[Configuration](docs/Configuration.md)** - Settings and retry
- **[Runtime](docs/Runtime.md)** - Consumer lifecycle
- **[Testing](docs/Testing.md)** - Test patterns

---

## 📄 License

This project is licensed under the [MIT License](LICENSE).

---

**Built with 🍯 by HoneyDrunk Studios**

[GitHub](https://github.com/HoneyDrunkStudios/HoneyDrunk.Transport) • [Documentation](docs/FILE_GUIDE.md) • [Issues](https://github.com/HoneyDrunkStudios/HoneyDrunk.Transport/issues)
