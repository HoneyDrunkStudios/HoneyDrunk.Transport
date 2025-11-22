# 🚌 AzureServiceBus - Azure Service Bus Transport

[← Back to File Guide](FILE_GUIDE.md)

---

## Overview

Azure Service Bus transport for enterprise messaging with advanced features like topics, sessions, transactions, and duplicate detection.

**Location:** `HoneyDrunk.Transport.AzureServiceBus/`

**Key Characteristics:**
- ✅ Topics and subscriptions (pub/sub)
- ✅ Sessions for ordered processing
- ✅ Transactional receive
- ✅ Message size up to 1MB (256KB standard, 1MB premium)
- ✅ Dead-letter queue built-in
- ✅ Duplicate detection
- ✅ Scheduled messages
- ⚠️ Higher cost than Storage Queue
- ⚠️ More complex setup

---

## AzureServiceBusOptions.cs

```csharp
public sealed class AzureServiceBusOptions
{
    public string? ConnectionString { get; set; }
    public string Address { get; set; } = string.Empty;
    public bool IsQueue { get; set; } = true;
    public string? SubscriptionName { get; set; }
    public bool EnableSessions { get; set; } = false;
    public int MaxConcurrentCalls { get; set; } = 1;
    public TimeSpan MaxAutoLockRenewalDuration { get; set; } = TimeSpan.FromMinutes(5);
    public ServiceBusReceiveMode ReceiveMode { get; set; } = ServiceBusReceiveMode.PeekLock;
    
    // Blob fallback for publish failures
    public BlobFallbackOptions BlobFallback { get; set; } = new();
}

public sealed class BlobFallbackOptions
{
    public bool Enabled { get; set; } = false;
    public string? ConnectionString { get; set; }
    public Uri? AccountUrl { get; set; }
    public string ContainerName { get; set; } = "transport-fallback";
    public string BlobPrefix { get; set; } = "servicebus";
}
```

### Usage Example

```csharp
services.AddHoneyDrunkServiceBusTransport(options =>
{
    options.ConnectionString = configuration["ServiceBus:ConnectionString"];
    options.Address = "orders";
    options.IsQueue = false; // Using topic
    options.SubscriptionName = "order-processor";
    
    // Sessions for ordered processing
    options.EnableSessions = true;
    options.MaxConcurrentCalls = 5;
    options.MaxAutoLockRenewalDuration = TimeSpan.FromMinutes(10);
    
    // Blob fallback for publish failures
    options.BlobFallback.Enabled = true;
    options.BlobFallback.ConnectionString = configuration["Blob:ConnectionString"];
    options.BlobFallback.ContainerName = "transport-fallback";
    options.BlobFallback.BlobPrefix = "servicebus";
});
```

---

## ServiceBusTransportPublisher.cs

```csharp
public sealed class ServiceBusTransportPublisher(
    ServiceBusClient client,
    IOptions<AzureServiceBusOptions> options) 
    : ITransportPublisher
{
    public async Task PublishAsync(
        ITransportEnvelope envelope,
        IEndpointAddress destination,
        CancellationToken ct)
    {
        var sender = client.CreateSender(destination.Address);
        var message = MapToServiceBusMessage(envelope);
        
        await sender.SendMessageAsync(message, ct);
    }
}
```

### Usage Example

```csharp
public class OrderService(ITransportPublisher publisher)
{
    public async Task PublishOrderAsync(Order order, CancellationToken ct)
    {
        var envelope = CreateEnvelope(order);
        
        // Add Service Bus specific properties
        var enriched = envelope.WithHeaders(new Dictionary<string, string>
        {
            ["SessionId"] = order.CustomerId.ToString(), // For sessions
            ["PartitionKey"] = order.CustomerId.ToString() // For partitioning
        });
        
        await publisher.PublishAsync(enriched, new EndpointAddress("orders"), ct);
    }
}
```

---

## ServiceBusTransportConsumer.cs

```csharp
public sealed class ServiceBusTransportConsumer(
    ServiceBusClient client,
    IMessagePipeline pipeline,
    IOptions<AzureServiceBusOptions> options) 
    : ITransportConsumer
{
    public async Task StartAsync(CancellationToken ct)
    {
        var processor = CreateProcessor();
        processor.ProcessMessageAsync += OnMessageAsync;
        processor.ProcessErrorAsync += OnErrorAsync;
        await processor.StartProcessingAsync(ct);
    }
}
```

### Usage Example

```csharp
// Started automatically as BackgroundService
services.AddHoneyDrunkServiceBusTransport(options =>
{
    options.Address = "orders";
    options.SubscriptionName = "order-processor";
});
```

---

## Sessions Support

```csharp
// Enable sessions for ordered processing
services.AddHoneyDrunkServiceBusTransport(options =>
{
    options.Address = "orders";
    options.EnableSessions = true;
    options.MaxConcurrentCalls = 10; // Process 10 sessions concurrently
});

// Publish with SessionId
var envelope = factory.CreateEnvelope<OrderCreated>(payload);
var withSession = envelope.WithHeaders(new Dictionary<string, string>
{
    ["SessionId"] = order.CustomerId.ToString()
});

await publisher.PublishAsync(withSession, destination, ct);

// All messages with same SessionId processed in order
```

---

## Topics and Subscriptions

```csharp
// Publisher (publishes to topic)
services.AddHoneyDrunkServiceBusTransport(options =>
{
    options.Address = "orders"; // Topic name
    options.IsQueue = false;
});

// Consumer 1 (subscription: order-processor)
services.AddHoneyDrunkServiceBusTransport(options =>
{
    options.Address = "orders";
    options.IsQueue = false;
    options.SubscriptionName = "order-processor";
});

// Consumer 2 (subscription: analytics)
services.AddHoneyDrunkServiceBusTransport(options =>
{
    options.Address = "orders";
    options.IsQueue = false;
    options.SubscriptionName = "analytics";
});

// Both consumers receive all messages published to the topic
```

---

## Blob Fallback Pattern

When Service Bus publish fails (outage, throttling), messages are persisted to Blob Storage for later replay.

### Configuration

```csharp
services.AddHoneyDrunkServiceBusTransport(options =>
{
    options.ConnectionString = configuration["ServiceBus:ConnectionString"];
    options.Address = "orders";
    
    // Enable blob fallback
    options.BlobFallback.Enabled = true;
    options.BlobFallback.ConnectionString = configuration["Blob:ConnectionString"];
    options.BlobFallback.ContainerName = "transport-fallback";
    options.BlobFallback.BlobPrefix = "servicebus";
})
.WithBlobFallback(fb =>
{
    fb.Enabled = true;
    fb.ConnectionString = configuration["Blob:ConnectionString"];
    fb.ContainerName = "transport-fallback";
});
```

### Blob Storage Structure

```
{prefix}/{address}/{yyyy/MM/dd/HH}/{MessageId}.json

Example:
servicebus/orders/2025/01/15/10/01FZ8E1Y3M2R7R5K62YB9S6Q2G.json
```

### Blob Record Format

```json
{
  "messageId": "01FZ8E1Y3M2R7R5K62YB9S6Q2G",
  "correlationId": "abc-123",
  "causationId": "def-456",
  "timestamp": "2025-01-15T10:15:00Z",
  "messageType": "OrderCreated",
  "headers": { "Priority": "high" },
  "payload": "<base64>",
  "destination": {
    "address": "orders",
    "properties": { "SessionId": "customer-123" }
  },
  "failureAt": "2025-01-15T10:16:03Z",
  "failureType": "Azure.Messaging.ServiceBus.ServiceBusException",
  "failureMessage": "The messaging entity could not be found"
}
```

### Replaying Failed Messages

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
            
            try
            {
                var envelope = RecreateEnvelope(record);
                await publisher.PublishAsync(envelope, record.Destination, ct);
                
                // Delete blob after successful replay
                await container.DeleteBlobAsync(blob.Name, ct);
                
                _logger.LogInformation(
                    "Replayed message {MessageId}",
                    record.MessageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to replay message {MessageId}",
                    record.MessageId);
            }
        }
    }
}
```

---

## Dead-Letter Queue

```csharp
// Messages automatically moved to DLQ after max delivery attempts
// or when handler returns MessageProcessingResult.DeadLetter

// Monitor DLQ
var deadLetterReceiver = client.CreateReceiver(
    "orders",
    "order-processor",
    new ServiceBusReceiverOptions
    {
        SubQueue = SubQueue.DeadLetter
    });

await foreach (var message in deadLetterReceiver.ReceiveMessagesAsync(ct))
{
    _logger.LogWarning(
        "Dead-letter message {MessageId}: {Reason}",
        message.MessageId,
        message.DeadLetterReason);
}
```

---

## Scheduled Messages

```csharp
// Schedule message for future delivery
var envelope = factory.CreateEnvelope<OrderReminder>(payload);
var message = MapToServiceBusMessage(envelope);
message.ScheduledEnqueueTime = DateTimeOffset.UtcNow.AddHours(24);

await sender.SendMessageAsync(message, ct);
```

---

## Complete Setup Example

```csharp
var builder = WebApplication.CreateBuilder(args);

// 1. Register Kernel
builder.Services.AddHoneyDrunkCoreNode(nodeDescriptor);

// 2. Register Service Bus transport
builder.Services
    .AddHoneyDrunkServiceBusTransport(options =>
    {
        options.ConnectionString = builder.Configuration["ServiceBus:ConnectionString"];
        options.Address = "orders";
        options.IsQueue = false; // Topic
        options.SubscriptionName = "order-processor";
        options.EnableSessions = true;
        options.MaxConcurrentCalls = 10;
        
        // Blob fallback
        options.BlobFallback.Enabled = true;
        options.BlobFallback.ConnectionString = builder.Configuration["Blob:ConnectionString"];
    })
    .WithRetry(retry =>
    {
        retry.MaxAttempts = 5;
        retry.BackoffStrategy = BackoffStrategy.Exponential;
    });

// 3. Register handlers
builder.Services.AddMessageHandler<OrderCreated, OrderCreatedHandler>();

var app = builder.Build();
app.Run();
```

---

[← Back to File Guide](FILE_GUIDE.md)
