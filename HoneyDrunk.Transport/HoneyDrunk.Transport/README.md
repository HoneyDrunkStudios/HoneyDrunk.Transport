# HoneyDrunk.Transport

[![NuGet](https://img.shields.io/nuget/v/HoneyDrunk.Transport.svg)](https://www.nuget.org/packages/HoneyDrunk.Transport/)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/10.0)

> **Transport-Agnostic Messaging for .NET** - Unified abstraction layer over message brokers with middleware pipeline, Grid context propagation, and retry strategies.

## 📋 What Is This?

**HoneyDrunk.Transport** is the messaging backbone for HoneyDrunk.OS applications. It provides a **transport-agnostic** abstraction that lets you send and receive messages without coupling to specific broker implementations (Azure Service Bus, Storage Queue, etc.). Built on a middleware pipeline pattern similar to ASP.NET Core, it integrates seamlessly with **HoneyDrunk.Kernel** for Grid-aware distributed context propagation.

**Core Responsibilities:**
- ✅ **Publisher/Consumer Abstractions** - `ITransportPublisher` and `ITransportConsumer` for broker-agnostic messaging
- ✅ **Envelope Pattern** - Immutable message wrappers with correlation, causation, and Grid context
- ✅ **Middleware Pipeline** - Onion-style processing (GridContext → Telemetry → Logging → Handler)
- ✅ **Retry Strategies** - Configurable backoff (Fixed, Linear, Exponential)
- ✅ **Transactional Outbox** - Exactly-once delivery with database transactions
- ✅ **Grid Integration** - Automatic context propagation across Node boundaries
- ✅ **Health & Metrics** - Built-in health contributors and telemetry integration

**Signal Quote:** *"Send messages, not worries."*

---

## 📦 What's Inside

### 📋 Abstractions
Core contracts for transport-agnostic messaging:
- **ITransportEnvelope** - Message wrapper with correlation, Grid context, and metadata
- **ITransportPublisher** - Publishes messages to destinations
- **ITransportConsumer** - Consumes messages and processes through pipeline
- **IMessageHandler<TMessage>** - Type-safe message processing
- **MessageProcessingResult** - Success, Retry, or DeadLetter outcomes

### 🔧 Primitives
Building blocks for message handling:
- **TransportEnvelope** - Immutable envelope implementation
- **EnvelopeFactory** - Creates envelopes with auto-generated IDs
- **JsonMessageSerializer** - Default JSON serialization

### ⚙️ Configuration
Settings and retry policies:
- **RetryOptions** - MaxAttempts, InitialDelay, BackoffStrategy
- **BackoffStrategy** - Fixed, Linear, Exponential

### 🔄 Pipeline
Middleware execution system:
- **GridContextPropagationMiddleware** - Extracts Grid context
- **TelemetryMiddleware** - OpenTelemetry tracing
- **LoggingMiddleware** - Structured logging

### 📤 Outbox
Transactional messaging pattern:
- **IOutboxStore** - Persists messages in database transactions
- **IOutboxDispatcher** - Background reliable delivery

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

```csharp
// Setup
builder.Services.AddHoneyDrunkCoreNode(nodeDescriptor);
builder.Services.AddHoneyDrunkTransportCore()
    .AddHoneyDrunkServiceBusTransport(/* ... */);

// Publish
var envelope = factory.CreateEnvelopeWithGridContext<OrderCreated>(payload, gridContext);
await publisher.PublishAsync(envelope, new EndpointAddress("orders"), ct);

// Handle
public class OrderCreatedHandler : IMessageHandler<OrderCreated>
{
    public async Task<MessageProcessingResult> HandleAsync(
        OrderCreated message,
        MessageContext context,
        CancellationToken ct)
    {
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
- **[Testing](docs/Testing.md)** - Test patterns

---

## 📄 License

This project is licensed under the [MIT License](LICENSE).

---

**Built with 🍯 by HoneyDrunk Studios**

[GitHub](https://github.com/HoneyDrunkStudios/HoneyDrunk.Transport) • [Documentation](docs/FILE_GUIDE.md) • [Issues](https://github.com/HoneyDrunkStudios/HoneyDrunk.Transport/issues)
