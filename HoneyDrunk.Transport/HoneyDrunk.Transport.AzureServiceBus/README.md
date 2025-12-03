# HoneyDrunk.Transport.AzureServiceBus

[![NuGet](https://img.shields.io/nuget/v/HoneyDrunk.Transport.AzureServiceBus.svg)](https://www.nuget.org/packages/HoneyDrunk.Transport.AzureServiceBus/)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/10.0)

> **Enterprise Messaging for Azure** - Azure Service Bus transport with topics, sessions, transactions, and blob fallback for mission-critical workloads.

## 📋 What Is This?

**HoneyDrunk.Transport.AzureServiceBus** provides Azure Service Bus integration for enterprise messaging scenarios. Supports topics/subscriptions (pub/sub), sessions (ordered processing), transactional receive, and blob storage fallback for publish failures. Ideal for high-throughput, high-correctness workloads where ordering, transactions, and DLQ behavior matter.

This provider is configured via `AzureServiceBusOptions`, extending the core `TransportOptions` type.

**Key Features:**
- ✅ **Topics & Subscriptions** - Pub/sub messaging patterns
- ✅ **Sessions** - Ordered message processing per session
- ✅ **Transactions** - Transactional receive (PeekLock mode)
- ✅ **Message Size** - Up to 100MB (256KB standard, 100MB premium)
- ✅ **Dead-Letter Queue** - Built-in DLQ support
- ✅ **Duplicate Detection** - Message deduplication
- ✅ **Blob Fallback** - Persist failed publishes to Blob Storage
- ⚠️ **Higher Cost** - More expensive than Storage Queue

**Signal Quote:** *"Enterprise messaging with all the bells and whistles."*

---

## 📦 What's Inside

### AzureServiceBusOptions
Configuration for Service Bus and blob fallback:
- **FullyQualifiedNamespace** - Service Bus namespace (managed identity)
- **ConnectionString** - Alternative to managed identity
- **Address** - Topic or queue name
- **EntityType** - `ServiceBusEntityType.Queue` or `ServiceBusEntityType.Topic`
- **SubscriptionName** - Topic subscription name
- **SessionEnabled** - Ordered processing via sessions
- **MaxConcurrency** - Concurrent message processing
- **BlobFallback** - Blob Storage persistence for publish failures

The transport uses PeekLock mode with configurable AutoComplete behavior. Set `options.AutoComplete = false` to take manual control of message completion.

### Blob Fallback Pattern
When Service Bus publish fails, messages are automatically persisted to Blob Storage:
```
{prefix}/{address}/{yyyy/MM/dd/HH}/{MessageId}.json
```

Each blob contains:
- Original message envelope
- Destination metadata
- Failure timestamp and exception details

> Blob fallback does **not** automatically replay messages. It preserves them durably so you can replay them intentionally using your own replayer logic.

### Session Support
Enable ordered processing per SessionId:
```csharp
options.SessionEnabled = true;
options.MaxConcurrency = 10; // Process 10 sessions concurrently
```

### Dead-Letter Queue
Messages move to DLQ when the handler returns `MessageProcessingResult.DeadLetter` or when the broker's `MaxDeliveryCount` is exceeded.

---

## 📥 Installation

```bash
dotnet add package HoneyDrunk.Transport.AzureServiceBus
```

```xml
<PackageReference Include="HoneyDrunk.Transport.AzureServiceBus" Version="0.1.2" />
```

---

## 💡 Quick Example

### Setup with Blob Fallback

```csharp
builder.Services.AddHoneyDrunkCoreNode(nodeDescriptor);

builder.Services
    .AddHoneyDrunkTransportCore(options =>
    {
        options.EnableTelemetry = true;
        options.EnableLogging = true;
        options.EnableCorrelation = true;
    })
    .AddHoneyDrunkServiceBusTransport(options =>
    {
        options.FullyQualifiedNamespace = "mynamespace.servicebus.windows.net";
        options.Address = "orders";
        options.EntityType = ServiceBusEntityType.Topic;
        options.SubscriptionName = "order-processor";
        options.SessionEnabled = true;
        options.MaxConcurrency = 10;
        
        // Blob fallback for publish failures
        options.BlobFallback.Enabled = true;
        options.BlobFallback.AccountUrl = "https://myaccount.blob.core.windows.net";
        options.BlobFallback.ContainerName = "transport-fallback";
    })
    .WithRetry(retry =>
    {
        retry.MaxAttempts = 5;
        retry.BackoffStrategy = BackoffStrategy.Exponential;
    });
```

### Publish with SessionId

```csharp
var envelope = factory.CreateEnvelope<OrderCreated>(payload);

// Use EndpointAddress for session - the transport maps this to ServiceBusMessage.SessionId
var destination = EndpointAddress.Create(
    name: "orders",
    address: "orders",
    sessionId: order.CustomerId.ToString());

await publisher.PublishAsync(envelope, destination, ct);
```

### Replay Failed Messages

```csharp
public class FailedMessageReplayer(
    BlobContainerClient container,
    ITransportPublisher publisher)
{
    public async Task ReplayFailedMessagesAsync(CancellationToken ct)
    {
        await foreach (var blob in container.GetBlobsAsync(prefix: "servicebus/orders", ct))
        {
            var json = await DownloadBlobAsync(blob.Name, ct);
            var record = JsonSerializer.Deserialize<FailedMessageRecord>(json);
            
            var envelope = RecreateEnvelope(record);
            var destination = EndpointAddress.Create(
                record.Destination.Name,
                record.Destination.Address,
                sessionId: record.Destination.SessionId);
            
            await publisher.PublishAsync(envelope, destination, ct);
            await container.DeleteBlobAsync(blob.Name, ct);
        }
    }
}
```

---

## 🎯 When to Use

**Choose Service Bus when:**
- ✅ Need topics/subscriptions (pub/sub)
- ✅ Require sessions for ordered processing
- ✅ Need transactional receive
- ✅ Message size up to 100MB
- ✅ Duplicate detection is required
- ✅ Mission-critical workloads

**Choose Storage Queue when:**
- ✅ Cost optimization is critical
- ✅ Simple queue semantics are sufficient
- ✅ Message size < 64KB

---

## 🔗 Related Packages

- **[HoneyDrunk.Transport](https://www.nuget.org/packages/HoneyDrunk.Transport/)** - Core abstractions
- **[HoneyDrunk.Transport.StorageQueue](https://www.nuget.org/packages/HoneyDrunk.Transport.StorageQueue/)** - Cost-effective alternative
- **[HoneyDrunk.Transport.InMemory](https://www.nuget.org/packages/HoneyDrunk.Transport.InMemory/)** - Testing transport

---

## 📚 Documentation

- **[Azure Service Bus Guide](../docs/AzureServiceBus.md)** - Detailed implementation guide
- **[Blob Fallback Guide](../docs/AzureServiceBus.md#blob-fallback)** - Failure handling patterns
- **[Complete File Guide](../docs/FILE_GUIDE.md)** - Architecture documentation

---

## 📄 License

This project is licensed under the [MIT License](../LICENSE).

---

**Built with 🍯 by HoneyDrunk Studios**

[GitHub](https://github.com/HoneyDrunkStudios/HoneyDrunk.Transport) • [Documentation](../docs/FILE_GUIDE.md) • [Issues](https://github.com/HoneyDrunkStudios/HoneyDrunk.Transport/issues)
