# HoneyDrunk.Transport.StorageQueue

[![NuGet](https://img.shields.io/nuget/v/HoneyDrunk.Transport.StorageQueue.svg)](https://www.nuget.org/packages/HoneyDrunk.Transport.StorageQueue/)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/10.0)

> **Cost-Effective Azure Messaging** - Azure Storage Queue transport for high-volume, simple queue scenarios at $0.0004 per 10,000 transactions.

## 📋 What Is This?

**HoneyDrunk.Transport.StorageQueue** provides Azure Storage Queue integration for cost-effective, high-volume messaging. Ideal for simple queue-based scenarios where advanced features like topics, sessions, or transactions aren't required. Includes built-in poison queue handling, exponential backoff polling, and two-level concurrency control.

This provider is configured via `StorageQueueOptions`, extending the core `TransportOptions` type.

**Key Features:**
- ✅ **Cost-Effective** - ~10x cheaper than Service Bus
- ✅ **High Volume** - Millions of messages per day
- ✅ **Simple Queues** - Point-to-point messaging
- ✅ **Poison Queue** - Automatic dead-letter handling
- ✅ **Exponential Backoff** - Empty queue polling optimization
- ✅ **Two-Level Concurrency** - MaxConcurrency × BatchProcessingConcurrency
- ⚠️ **Message Size Limit** - 64KB (after Base64 encoding)
- ⚠️ **At-Least-Once** - No duplicate detection

**What Storage Queue Does Not Support:**
> Storage Queue does not support sessions, transactions, duplicate detection, or pub/sub semantics. For those features, use Azure Service Bus.

**Signal Quote:** *"High volume, low cost, simple semantics."*

---

## 📦 What's Inside

### StorageQueueOptions
Configuration for connection, queues, and concurrency:
- **ConnectionString** / **AccountEndpoint** - Authentication
- **QueueName** / **PoisonQueueName** - Queue identifiers
- **MaxDequeueCount** - Poison threshold (default: 5)
- **MaxConcurrency** - Number of parallel fetch loops (default: 5)
- **BatchProcessingConcurrency** - Concurrent messages per fetch loop (default: 1)
- **PrefetchMaxMessages** - Batch size 1-32 (default: 16)
- **VisibilityTimeout** - Message lock duration (default: 30s)

### Concurrency Model
`MaxConcurrency` controls parallel fetch loops. `BatchProcessingConcurrency` controls how many messages each loop may process concurrently.

```
Total Concurrent Processing = MaxConcurrency × BatchProcessingConcurrency

Examples:
- MaxConcurrency=5, BatchProcessingConcurrency=1 = 5 concurrent
- MaxConcurrency=10, BatchProcessingConcurrency=4 = 40 concurrent
```

### MessageTooLargeException
Exception thrown when messages exceed the 64KB limit. The 64KB limit applies to the **final Base64-encoded message body**, not the raw payload. For oversized messages, store the payload in Blob Storage and send a reference message.

### Runtime Integration
Storage Queue integrates with `TransportRuntimeHost`, health contributors, and the standard telemetry middleware for consistent observability across transports.

---

## 📥 Installation

```bash
dotnet add package HoneyDrunk.Transport.StorageQueue
```

```xml
<PackageReference Include="HoneyDrunk.Transport.StorageQueue" Version="0.1.1" />
```

---

## 💡 Quick Example

### Setup

```csharp
builder.Services.AddHoneyDrunkCoreNode(nodeDescriptor);

builder.Services
    .AddHoneyDrunkTransportCore(options =>
    {
        options.EnableTelemetry = true;
        options.EnableLogging = true;
    })
    .AddHoneyDrunkTransportStorageQueue(
        builder.Configuration["StorageQueue:ConnectionString"]!,
        "orders")
    .WithMaxDequeueCount(5)
    .WithConcurrency(10)
    .WithBatchProcessingConcurrency(4) // 10 × 4 = 40 concurrent
    .WithPoisonQueue("orders-poison");
```

### Handle Oversized Messages (Application-Level)

```csharp
try
{
    await publisher.PublishAsync(envelope, destination, ct);
}
catch (MessageTooLargeException ex)
{
    // Application-level handling: store payload in Blob Storage, send reference
    var blobUrl = await _blobStorage.UploadAsync(largePayload, ct);
    var referenceMessage = new BlobReferenceMessage(blobUrl);
    var referenceEnvelope = factory.CreateEnvelope<BlobReferenceMessage>(
        serializer.Serialize(referenceMessage));
    
    await publisher.PublishAsync(referenceEnvelope, destination, ct);
}
```

---

## 🎯 When to Use

**Choose Storage Queue when:**
- ✅ Cost optimization is critical
- ✅ Message volume is very high (millions/day)
- ✅ Simple queue semantics are sufficient
- ✅ Message size < 64KB
- ✅ At-least-once delivery is acceptable

**Choose Service Bus when:**
- ✅ Need topics/subscriptions (pub/sub)
- ✅ Require sessions for ordered processing
- ✅ Need transactional receive
- ✅ Message size up to 100MB
- ✅ Duplicate detection is required

---

## 🔗 Related Packages

- **[HoneyDrunk.Transport](https://www.nuget.org/packages/HoneyDrunk.Transport/)** - Core abstractions
- **[HoneyDrunk.Transport.AzureServiceBus](https://www.nuget.org/packages/HoneyDrunk.Transport.AzureServiceBus/)** - Advanced Service Bus features
- **[HoneyDrunk.Transport.InMemory](https://www.nuget.org/packages/HoneyDrunk.Transport.InMemory/)** - Testing transport

---

## 📚 Documentation

- **[Storage Queue Guide](../docs/StorageQueue.md)** - Detailed implementation guide
- **[Configuration Guide](../docs/Configuration.md)** - StorageQueueOptions reference
- **[Complete File Guide](../docs/FILE_GUIDE.md)** - Architecture documentation

---

## 📄 License

This project is licensed under the [MIT License](../LICENSE).

---

**Built with 🍯 by HoneyDrunk Studios**

[GitHub](https://github.com/HoneyDrunkStudios/HoneyDrunk.Transport) • [Documentation](../docs/FILE_GUIDE.md) • [Issues](https://github.com/HoneyDrunkStudios/HoneyDrunk.Transport/issues)
