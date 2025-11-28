# 📦 StorageQueue - Azure Storage Queue Transport

[← Back to File Guide](FILE_GUIDE.md)

---

## Table of Contents

- [Overview](#overview)
- [StorageQueueOptions.cs](#storagequeueoptionscs)
- [StorageQueueSender.cs](#storagequeuesendercs)
- [StorageQueueProcessor.cs](#storagequeueprocessorcs)
- [Concurrency Model](#concurrency-model)
- [Poison Queue Pattern](#poison-queue-pattern)
- [Message Size Handling](#message-size-handling)
- [Complete Setup Example](#complete-setup-example)
- [When to Use Storage Queue](#when-to-use-storage-queue)

---

## Overview

Azure Storage Queue transport for cost-effective, high-volume messaging. Ideal for simple queue-based scenarios without advanced features like topics or sessions.

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

---

## StorageQueueOptions.cs

```csharp
public sealed class StorageQueueOptions
{
    public string? ConnectionString { get; set; }
    public Uri? AccountEndpoint { get; set; }
    public string QueueName { get; set; } = string.Empty;
    public string? PoisonQueueName { get; set; }
    public bool CreateIfNotExists { get; set; } = true;
    
    public int MaxDequeueCount { get; set; } = 5;
    public TimeSpan VisibilityTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public int PrefetchMaxMessages { get; set; } = 16;
    public int MaxConcurrency { get; set; } = 5;
    public int BatchProcessingConcurrency { get; set; } = 1;
    
    public TimeSpan EmptyQueuePollingInterval { get; set; } = TimeSpan.FromSeconds(1);
    public TimeSpan MaxPollingInterval { get; set; } = TimeSpan.FromSeconds(5);
}
```

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
    options.BatchProcessingConcurrency = 4; // 10 × 4 = 40 concurrent operations
    options.PrefetchMaxMessages = 16;
    
    // Polling (empty queue backoff)
    options.EmptyQueuePollingInterval = TimeSpan.FromSeconds(1);
    options.MaxPollingInterval = TimeSpan.FromSeconds(5);
});
```

---

## StorageQueueSender.cs

```csharp
public sealed class StorageQueueSender(QueueClientFactory factory) 
    : ITransportPublisher
{
    public async Task PublishAsync(
        ITransportEnvelope envelope,
        IEndpointAddress destination,
        CancellationToken ct)
    {
        var queueClient = await factory.GetOrCreatePrimaryQueueClientAsync(ct);
        var storageEnvelope = MapToStorageEnvelope(envelope);
        var json = JsonSerializer.Serialize(storageEnvelope);
        
        await queueClient.SendMessageAsync(json, ct);
    }
}
```

### Usage Example

```csharp
// Automatic usage through ITransportPublisher
public class OrderService(ITransportPublisher publisher)
{
    public async Task PublishOrderAsync(Order order, CancellationToken ct)
    {
        var envelope = CreateEnvelope(order);
        await publisher.PublishAsync(envelope, new EndpointAddress("orders"), ct);
        // Published to Storage Queue
    }
}
```

---

## StorageQueueProcessor.cs

```csharp
public sealed class StorageQueueProcessor(
    QueueClientFactory factory,
    IMessagePipeline pipeline,
    IOptions<StorageQueueOptions> options) 
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            // Fetch batch
            var messages = await FetchMessagesAsync(ct);
            
            if (!messages.Any())
            {
                await ApplyBackoffAsync(ct);
                continue;
            }
            
            // Process messages (sequential or parallel based on BatchProcessingConcurrency)
            await ProcessBatchAsync(messages, ct);
        }
    }
}
```

### Usage Example

```csharp
// Started automatically as BackgroundService
services.AddHoneyDrunkTransportStorageQueue(/* ... */);

// Processor polls queue and processes messages through pipeline
```

---

## Concurrency Model

Storage Queue transport uses a **two-level concurrency model**:

```
Total Concurrent Processing = MaxConcurrency × BatchProcessingConcurrency

Examples:
- MaxConcurrency=5, BatchProcessingConcurrency=1 (default) = 5 concurrent
- MaxConcurrency=10, BatchProcessingConcurrency=4 = 40 concurrent
- MaxConcurrency=5, BatchProcessingConcurrency=8 = 40 concurrent
```

### Concurrency Configuration

```csharp
// Default: 5 fetch loops × 1 sequential = 5 concurrent operations
services.AddHoneyDrunkTransportStorageQueue(/* ... */)
    .WithConcurrency(5);

// High throughput: 10 fetch loops × 4 parallel per batch = 40 concurrent
services.AddHoneyDrunkTransportStorageQueue(/* ... */)
    .WithConcurrency(10)
    .WithBatchProcessingConcurrency(4);

// Alternative topology: 5 fetch loops × 8 parallel per batch = 40 concurrent
services.AddHoneyDrunkTransportStorageQueue(/* ... */)
    .WithConcurrency(5)
    .WithBatchProcessingConcurrency(8);
```

---

## Poison Queue Pattern

```csharp
public sealed class PoisonQueueMover(QueueClientFactory factory)
{
    public async Task MoveToPoisonQueueAsync(
        QueueMessage message,
        Exception error,
        CancellationToken ct)
    {
        var poisonClient = await factory.GetOrCreatePoisonQueueClientAsync(ct);
        
        var poisonEnvelope = new
        {
            OriginalMessageId = message.MessageId,
            OriginalMessage = message.Body.ToString(),
            DequeueCount = message.DequeueCount,
            FirstFailureTimestamp = /* ... */,
            LastFailureTimestamp = DateTime.UtcNow,
            ErrorType = error.GetType().FullName,
            ErrorMessage = error.Message,
            ErrorStackTrace = error.StackTrace
        };
        
        await poisonClient.SendMessageAsync(
            JsonSerializer.Serialize(poisonEnvelope),
            ct);
    }
}
```

### Monitoring Poison Queue

```csharp
public class PoisonQueueMonitor
{
    public async Task<IEnumerable<PoisonMessage>> GetPoisonMessagesAsync(
        CancellationToken ct)
    {
        var poisonClient = new QueueClient(connectionString, "orders-poison");
        var messages = await poisonClient.ReceiveMessagesAsync(maxMessages: 32, ct);
        
        return messages.Value.Select(m =>
            JsonSerializer.Deserialize<PoisonMessage>(m.Body.ToString()));
    }
}
```

---

## Message Size Handling

```csharp
public class MessageTooLargeException : Exception
{
    public string MessageId { get; }
    public int ActualSize { get; }
    public int MaxSize { get; }
}

// Handle oversized messages
try
{
    await publisher.PublishAsync(largeEnvelope, destination, ct);
}
catch (MessageTooLargeException ex)
{
    _logger.LogError(ex,
        "Message {MessageId} is {ActualSize}KB (max: {MaxSize}KB)",
        ex.MessageId,
        ex.ActualSize / 1024,
        ex.MaxSize / 1024);
    
    // Fallback: Store in Blob Storage
    var blobUrl = await _blobStorage.UploadAsync(payload, ct);
    var referenceMessage = new BlobReferenceMessage(blobUrl);
    await publisher.PublishAsync(CreateEnvelope(referenceMessage), destination, ct);
}
```

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
    .WithBatchProcessingConcurrency(4) // 10 × 4 = 40 concurrent
    .WithPoisonQueue("orders-poison");

// 3. Register handlers
builder.Services.AddMessageHandler<OrderCreated, OrderCreatedHandler>();

var app = builder.Build();
app.Run();
```

---

## When to Use Storage Queue

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

[← Back to File Guide](FILE_GUIDE.md)
