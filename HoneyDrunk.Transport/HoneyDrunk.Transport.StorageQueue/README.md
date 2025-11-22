# HoneyDrunk.Transport.StorageQueue

[![NuGet](https://img.shields.io/nuget/v/HoneyDrunk.Transport.StorageQueue.svg)](https://www.nuget.org/packages/HoneyDrunk.Transport.StorageQueue/)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/10.0)

> **Cost-Effective Azure Messaging** - Azure Storage Queue transport for high-volume, simple queue scenarios at $0.0004 per 10,000 transactions.

## 📋 What Is This?

**HoneyDrunk.Transport.StorageQueue** provides Azure Storage Queue integration for cost-effective, high-volume messaging. Ideal for simple queue-based scenarios where advanced features like topics, sessions, or transactions aren't required. Includes built-in poison queue handling, exponential backoff, and two-level concurrency control.

**Key Features:**
- ✅ **Cost-Effective** - ~10x cheaper than Service Bus
- ✅ **High Volume** - Millions of messages per day
- ✅ **Simple Queues** - No topics/subscriptions complexity
- ✅ **Poison Queue** - Automatic dead-letter handling
- ✅ **Exponential Backoff** - Empty queue polling optimization
- ✅ **Two-Level Concurrency** - MaxConcurrency × BatchProcessingConcurrency
- ⚠️ **Message Size Limit** - 64KB (base64-encoded)
- ⚠️ **At-Least-Once** - No duplicate detection

**Signal Quote:** *"High volume, low cost, simple semantics."*

---

## 📦 What's Inside

### StorageQueueOptions
Configuration for connection, queues, and concurrency:
- **ConnectionString** / **AccountEndpoint** - Authentication
- **QueueName** / **PoisonQueueName** - Queue identifiers
- **MaxDequeueCount** - Poison threshold (default: 5)
- **MaxConcurrency** - Number of fetch loops (default: 5)
- **BatchProcessingConcurrency** - Concurrent messages per batch (default: 1)
- **PrefetchMaxMessages** - Batch size 1-32 (default: 16)
- **VisibilityTimeout** - Message lock duration (default: 30s)

### Concurrency Model
```
Total Concurrent Processing = MaxConcurrency × BatchProcessingConcurrency

Examples:
- MaxConcurrency=5, BatchProcessingConcurrency=1 = 5 concurrent
- MaxConcurrency=10, BatchProcessingConcurrency=4 = 40 concurrent
```

### MessageTooLargeException
Exception thrown when messages exceed 64KB limit with guidance for Blob Storage fallback pattern.

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
    .AddHoneyDrunkTransportStorageQueue(
        builder.Configuration["StorageQueue:ConnectionString"]!,
        "orders")
    .WithMaxDequeueCount(5)
    .WithConcurrency(10)
    .WithBatchProcessingConcurrency(4) // 10 × 4 = 40 concurrent
    .WithPoisonQueue("orders-poison");
```

### Handle Oversized Messages

```csharp
try
{
    await publisher.PublishAsync(envelope, destination, ct);
}
catch (MessageTooLargeException ex)
{
    // Fallback: Store in Blob Storage
    var blobUrl = await _blobStorage.UploadAsync(payload, ct);
    var referenceMessage = new BlobReferenceMessage(blobUrl);
    await publisher.PublishAsync(CreateEnvelope(referenceMessage), destination, ct);
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
- ✅ Message size up to 1MB+
- ✅ Duplicate detection is required

---

## 🔗 Related Packages

- **[HoneyDrunk.Transport](https://www.nuget.org/packages/HoneyDrunk.Transport/)** - Core abstractions
- **[HoneyDrunk.Transport.AzureServiceBus](https://www.nuget.org/packages/HoneyDrunk.Transport.AzureServiceBus/)** - Advanced Service Bus features
- **[HoneyDrunk.Transport.InMemory](https://www.nuget.org/packages/HoneyDrunk.Transport.InMemory/)** - Testing transport

---

## 📚 Documentation

- **[Storage Queue Guide](../docs/StorageQueue.md)** - Detailed implementation guide
- **[Concurrency Guide](../docs/STORAGE_QUEUE_CONCURRENCY.md)** - Two-level concurrency tuning
- **[Complete File Guide](../docs/FILE_GUIDE.md)** - Architecture documentation

---

## 📄 License

This project is licensed under the [MIT License](../LICENSE).

---

**Built with 🍯 by HoneyDrunk Studios**

[GitHub](https://github.com/HoneyDrunkStudios/HoneyDrunk.Transport) • [Documentation](../docs/FILE_GUIDE.md) • [Issues](https://github.com/HoneyDrunkStudios/HoneyDrunk.Transport/issues)
