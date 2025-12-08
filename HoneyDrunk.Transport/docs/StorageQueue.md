# 📦 StorageQueue - Azure Storage Queue Transport

[← Back to File Guide](FILE_GUIDE.md)

---

## Table of Contents

- [Overview](#overview)
- [StorageQueueOptions.cs](#storagequeueoptionscs)
- [StorageQueueSender.cs](#storagequeuesendercs)
- [StorageQueueProcessor.cs](#storagequeueprocessorcs)
- [StorageQueueTopology.cs](#storagequeuetopologycs)
- [Concurrency Model](#concurrency-model)
- [Poison Queue Pattern](#poison-queue-pattern)
- [Message Size Handling](#message-size-handling)
- [Complete Setup Example](#complete-setup-example)
- [When to Use Storage Queue](#when-to-use-storage-queue)

---

## Overview

Azure Storage Queue transport for cost-effective, high-volume messaging. Ideal for simple queue-based scenarios without advanced features like topics or sessions.

The Storage Queue transport implements the standard transport contracts (`ITransportPublisher`, `ITransportConsumer`, `ITransportTopology`) and participates in the shared pipeline and runtime just like Service Bus and InMemory.

It is intended for simple, high-volume workloads where at-least-once delivery and small payloads are acceptable.

**Location:** `HoneyDrunk.Transport.StorageQueue/`

**Key Characteristics:**
- ✅ Cost-effective ($0.0004 per 10,000 transactions)
- ✅ Simple queue semantics
- ✅ Message size up to 64KB (base64-encoded)
- ✅ At-least-once delivery
- ✅ Built-in poison queue pattern
- ✅ Exponential backoff for empty queues
- ⚠️ No topics/subscriptions (pub/sub)
- ⚠️ No sessions or transactions
- ⚠️ No native duplicate detection

**What Gets Registered:**

```csharp
services.AddHoneyDrunkTransportStorageQueue(...)
// Registers:
// - StorageQueueSender as ITransportPublisher
// - StorageQueueProcessor as ITransportConsumer
// - StorageQueueTopology as ITransportTopology
// - QueueClientFactory (internal)
// - PoisonQueueMover (internal)
```

---

## StorageQueueOptions.cs

Configuration for Azure Storage Queue transport. Extends `TransportOptions` and implements `IValidatableObject`.

```csharp
// Simplified excerpt. For full definition see Configuration → StorageQueueOptions.
public sealed class StorageQueueOptions : TransportOptions, IValidatableObject
{
    // Authentication
    public string? ConnectionString { get; set; }
    public Uri? AccountEndpoint { get; set; }

    // Queue settings
    public string QueueName { get; set; } = string.Empty;
    public string? PoisonQueueName { get; set; }  // Defaults to "{QueueName}-poison"
    public bool CreateIfNotExists { get; set; } = true;

    // Message semantics
    public TimeSpan? MessageTimeToLive { get; set; }
    public TimeSpan VisibilityTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public int MaxDequeueCount { get; set; } = 5;

    // Concurrency (see Concurrency Model section)
    public int MaxConcurrency { get; set; } = 5;
    public int BatchProcessingConcurrency { get; set; } = 1;
    public int PrefetchMaxMessages { get; set; } = 16;

    // Polling
    public TimeSpan EmptyQueuePollingInterval { get; set; } = TimeSpan.FromSeconds(1);
    public TimeSpan MaxPollingInterval { get; set; } = TimeSpan.FromSeconds(5);
}
```

> **Validation:** See [Configuration](Configuration.md) for validation rules including `BatchProcessingConcurrency <= PrefetchMaxMessages`, `MaxConcurrency <= 100`, and Azure limits.

### Usage Example

```csharp
services.AddHoneyDrunkTransportStorageQueue(options =>
{
    // Connection
    options.ConnectionString = configuration["StorageQueue:ConnectionString"];
    options.QueueName = "orders";
    options.CreateIfNotExists = true;
    
    // Message settings
    options.VisibilityTimeout = TimeSpan.FromSeconds(30);
    options.MaxDequeueCount = 5;
    options.PoisonQueueName = "orders-poison";
    
    // Processing
    options.MaxConcurrency = 10;
    options.BatchProcessingConcurrency = 4;  // 10 × 4 = 40 concurrent
    options.PrefetchMaxMessages = 16;
});
```

[↑ Back to top](#table-of-contents)

---

## StorageQueueSender.cs

Standard `ITransportPublisher` implementation for Azure Storage Queue.

```csharp
internal sealed class StorageQueueSender(
    QueueClientFactory queueClientFactory,
    IOptions<StorageQueueOptions> options,
    ILogger<StorageQueueSender> logger) 
    : ITransportPublisher, IAsyncDisposable
{
    public Task PublishAsync(
        ITransportEnvelope envelope,
        IEndpointAddress destination,
        CancellationToken cancellationToken = default);
    
    public Task PublishBatchAsync(
        IEnumerable<ITransportEnvelope> envelopes,
        IEndpointAddress destination,
        CancellationToken cancellationToken = default);
    
    public ValueTask DisposeAsync();
}
```

### Behavior

- Serializes `ITransportEnvelope` to JSON with base64-encoded payload
- Validates message size before sending (throws `MessageTooLargeException` if > 64KB)
- `PublishBatchAsync` parallelizes sends with configurable `MaxBatchPublishConcurrency`
- Telemetry via `TransportTelemetry` when `EnableTelemetry = true`

> **Size Limit:** The envelope is serialized to JSON and sent as a single Storage Queue message. Storage Queue adds its own base64 encoding, so `TransportEnvelope` plus headers and payload must stay under the 64KB limit.

### Usage Example

```csharp
// Automatic usage through ITransportPublisher
public class OrderService(ITransportPublisher publisher, EnvelopeFactory factory)
{
    public async Task PublishOrderAsync(OrderCreated order, CancellationToken ct)
    {
        var envelope = factory.CreateEnvelope<OrderCreated>(
            _serializer.Serialize(order));
        
        await publisher.PublishAsync(
            envelope,
            EndpointAddress.Create("orders", "orders"),
            ct);
    }
}
```

[↑ Back to top](#table-of-contents)

---

## StorageQueueProcessor.cs

Standard `ITransportConsumer` implementation for Azure Storage Queue.

```csharp
internal sealed class StorageQueueProcessor(
    QueueClientFactory queueClientFactory,
    IMessagePipeline pipeline,
    PoisonQueueMover poisonQueueMover,
    IOptions<StorageQueueOptions> options,
    ILogger<StorageQueueProcessor> logger) 
    : ITransportConsumer, IAsyncDisposable
{
    public Task StartAsync(CancellationToken cancellationToken = default);
    public Task StopAsync(CancellationToken cancellationToken = default);
    public ValueTask DisposeAsync();
}
```

### What StartAsync Does

On `StartAsync`, the processor:

1. Creates `MaxConcurrency` concurrent fetch loops
2. Each loop calls `ReceiveMessagesAsync` with `PrefetchMaxMessages`
3. Deserializes each message to `TransportEnvelope`
4. Forwards to `IMessagePipeline.ProcessAsync()` for full pipeline execution
5. On `Success`: deletes message from queue
6. On `Retry`: leaves message to become visible after `VisibilityTimeout`
7. On `DeadLetter` or `MaxDequeueCount` exceeded: moves to poison queue

The processor is registered as `ITransportConsumer`, so `TransportRuntimeHost` will start and stop it with the rest of the node.

### Usage Example

```csharp
// Registered automatically - no manual interaction needed
services.AddHoneyDrunkTransportStorageQueue(options => { ... });
// TransportRuntimeHost starts the processor when the node starts
```

[↑ Back to top](#table-of-contents)

---

## StorageQueueTopology.cs

Implements `ITransportTopology` to declare Storage Queue transport capabilities.

```csharp
internal sealed class StorageQueueTopology : ITransportTopology
{
    public string Name => "StorageQueue";
    public bool SupportsTopics => false;
    public bool SupportsSubscriptions => false;
    public bool SupportsSessions => false;
    public bool SupportsOrdering => false;
    public bool SupportsScheduledMessages => false;
    public bool SupportsBatching => true;
    public bool SupportsTransactions => false;
    public bool SupportsDeadLetterQueue => false;  // Manual via poison queue
    public bool SupportsPriority => false;
    public long? MaxMessageSize => 64 * 1024;  // 64 KB
}
```

> See [Architecture → Topology Capabilities](Architecture.md) for how topology is used across transport adapters.

[↑ Back to top](#table-of-contents)

---

## Concurrency Model

Storage Queue transport uses a **two-level concurrency model**:

```
Total Concurrent Processing = MaxConcurrency × BatchProcessingConcurrency

Where:
- MaxConcurrency     = number of concurrent fetch loops (default: 5)
- BatchProcessingConcurrency = concurrent messages per fetch loop (default: 1)

Examples:
- MaxConcurrency=5, BatchProcessingConcurrency=1 (default) = 5 concurrent
- MaxConcurrency=10, BatchProcessingConcurrency=4 = 40 concurrent
- MaxConcurrency=5, BatchProcessingConcurrency=8 = 40 concurrent
```

### Concurrency Configuration

```csharp
// Default: 5 fetch loops × 1 sequential = 5 concurrent operations
services.AddHoneyDrunkTransportStorageQueue(/* ... */);

// High throughput: 10 fetch loops × 4 parallel per batch = 40 concurrent
services.AddHoneyDrunkTransportStorageQueue(/* ... */)
    .WithConcurrency(10)
    .WithBatchProcessingConcurrency(4);

// Alternative topology: 5 fetch loops × 8 parallel per batch = 40 concurrent
services.AddHoneyDrunkTransportStorageQueue(/* ... */)
    .WithConcurrency(5)
    .WithBatchProcessingConcurrency(8);
```

### Validation Rules

- `BatchProcessingConcurrency` must be ≤ `PrefetchMaxMessages` (no benefit processing more than fetched)
- `MaxConcurrency` cannot exceed 100 to prevent resource exhaustion

> See [Configuration](Configuration.md) for full validation rules.

[↑ Back to top](#table-of-contents)

---

## Poison Queue Pattern

The Storage Queue transport automatically moves messages that exceed `MaxDequeueCount` into the poison queue. The `PoisonQueueMover` is the internal helper that constructs the diagnostic payload.

### Defaults

- `PoisonQueueName` defaults to `"{QueueName}-poison"` if not set
- Messages move to poison queue after `MaxDequeueCount` delivery attempts (default: 5)
- Handler returning `MessageProcessingResult.DeadLetter` immediately moves to poison queue

### Poison Queue Message Format

```csharp
// Internal helper that adds diagnostic metadata
internal sealed class PoisonQueueMover(ILogger<PoisonQueueMover> logger)
{
    public async Task MoveMessageToPoisonQueueAsync(
        QueueClient poisonQueueClient,
        QueueMessage originalMessage,
        Exception? error,
        long dequeueCount,
        CancellationToken ct)
    {
        var poisonPayload = new
        {
            OriginalMessageId = originalMessage.MessageId,
            OriginalMessageText = originalMessage.Body.ToString(),
            DequeueCount = dequeueCount,
            PoisonedAt = DateTimeOffset.UtcNow,
            ErrorType = error?.GetType().FullName,
            // Optionally redact or omit ErrorMessage if sensitive
            ErrorMessage = error?.Message
            // Do not include stack traces in poison queue payloads; log them securely instead
        };
        
        await poisonQueueClient.SendMessageAsync(
            JsonSerializer.Serialize(poisonPayload),
            ct);
    }
}
```

### Monitoring Poison Queue

```csharp
public class PoisonQueueMonitor(QueueClient poisonClient)
{
    public async Task<IEnumerable<PoisonMessage>> GetPoisonMessagesAsync(CancellationToken ct)
    {
        var messages = await poisonClient.ReceiveMessagesAsync(maxMessages: 32, ct);
        
        return messages.Value.Select(m =>
            JsonSerializer.Deserialize<PoisonMessage>(m.Body.ToString())!);
    }
}
```

[↑ Back to top](#table-of-contents)

---

## Message Size Handling

The Storage Queue adapter detects oversized envelopes **before** calling the SDK and throws `MessageTooLargeException`.

```csharp
// From HoneyDrunk.Transport.Exceptions
public sealed class MessageTooLargeException : Exception
{
    public string MessageId { get; }
    public long ActualSize { get; }
    public long MaxSize { get; }
    public string TransportName { get; }
}
```

### Blob Fallback Pattern

The Storage Queue adapter does **not** automatically offload oversized messages. One common pattern is to detect `MessageTooLargeException` in application code and fall back to Blob Storage with a small reference message:

```csharp
try
{
    await publisher.PublishAsync(envelope, destination, ct);
}
catch (MessageTooLargeException ex)
{
    _logger.LogWarning(
        "Message {MessageId} is {ActualKB}KB (max: {MaxKB}KB), using blob fallback",
        ex.MessageId,
        ex.ActualSize / 1024,
        ex.MaxSize / 1024);
    
    // Store payload in Blob Storage
    var blobUrl = await _blobStorage.UploadAsync(envelope.Payload.ToArray(), ct);
    
    // Send reference message
    var referenceEnvelope = factory.CreateEnvelope<BlobReference>(
        _serializer.Serialize(new BlobReference(blobUrl)));
    
    await publisher.PublishAsync(referenceEnvelope, destination, ct);
}
```

[↑ Back to top](#table-of-contents)

---

## Complete Setup Example

```csharp
var builder = WebApplication.CreateBuilder(args);

// 1. Register Kernel
builder.Services.AddHoneyDrunkCoreNode(nodeDescriptor);

// 2. Register Storage Queue transport
builder.Services
    .AddHoneyDrunkTransportStorageQueue(
        builder.Configuration["StorageQueue:ConnectionString"]!,
        "orders")
    .WithMaxDequeueCount(5)
    .WithConcurrency(10)
    .WithBatchProcessingConcurrency(4)  // 10 × 4 = 40 concurrent
    .WithPoisonQueue("orders-poison")
    .WithVisibilityTimeout(TimeSpan.FromSeconds(30));

// 3. Register handlers
builder.Services.AddMessageHandler<OrderCreated, OrderCreatedHandler>();
builder.Services.AddMessageHandler<OrderUpdated, OrderUpdatedHandler>();

var app = builder.Build();
app.Run();  // TransportRuntimeHost starts StorageQueueProcessor
```

[↑ Back to top](#table-of-contents)

---

## When to Use Storage Queue

| Scenario | Storage Queue | Service Bus |
|----------|---------------|-------------|
| **Cost optimization** | ✅ $0.0004/10K ops | ❌ Higher cost |
| **High volume (millions/day)** | ✅ Excellent | ✅ Good |
| **Simple queue semantics** | ✅ Yes | ✅ Yes |
| **Message size < 64KB** | ✅ Yes | ✅ Up to 1MB+ |
| **At-least-once acceptable** | ✅ Yes | ✅ Yes |
| **Single consumer group per queue** | ✅ Yes | ✅ Yes |
| **No complex routing** | ✅ Yes | - |
| **Topics/subscriptions (fan-out)** | ❌ No | ✅ Yes |
| **Sessions (ordered processing)** | ❌ No | ✅ Yes |
| **Transactions** | ❌ No | ✅ Yes |
| **Duplicate detection** | ❌ No | ✅ Yes |
| **Fine-grained routing (filters)** | ❌ No | ✅ Yes |

**Choose Storage Queue when:**
- Cost optimization is critical
- Message volume is very high (millions/day)
- Simple queue semantics are sufficient
- Message size < 64KB
- At-least-once delivery is acceptable
- You are fine modeling everything as a single consumer group per queue with no complex routing graph

**Choose Service Bus when:**
- Need topics/subscriptions (pub/sub)
- Require sessions for ordered processing
- Need transactional receive
- Message size up to 1MB+
- Duplicate detection is required
- Need fine-grained routing (multiple subscriptions, filters, fan-out)

[↑ Back to top](#table-of-contents)

---

[← Back to File Guide](FILE_GUIDE.md) | [↑ Back to top](#table-of-contents)
