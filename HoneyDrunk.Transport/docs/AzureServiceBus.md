# 🚌 AzureServiceBus - Azure Service Bus Transport

[← Back to File Guide](FILE_GUIDE.md)

---

## Table of Contents

- [Overview](#overview)
- [AzureServiceBusOptions.cs](#azureservicebusoptionscs)
- [ServiceBusTransportPublisher.cs](#servicebustransportpublishercs)
- [ServiceBusTransportConsumer.cs](#servicebustransportconsumercs)
- [Sessions Support](#sessions-support)
- [Topics and Subscriptions](#topics-and-subscriptions)
- [Blob Fallback](#blob-fallback)
- [Dead-Letter Queue](#dead-letter-queue)
- [Scheduled Messages](#scheduled-messages)
- [Complete Setup Example](#complete-setup-example)

---

## Overview

Azure Service Bus transport for enterprise messaging with advanced features like topics, sessions, transactions, and duplicate detection.

The Service Bus transport implements the standard transport contracts (`ITransportPublisher`, `ITransportConsumer`, `ITransportTopology`) and participates in the shared pipeline and runtime just like Storage Queue and InMemory.

**Location:** `HoneyDrunk.Transport.AzureServiceBus/`

**Key Characteristics:**
- ✅ Topics and subscriptions (pub/sub)
- ✅ Sessions for ordered processing
- ✅ Transactional receive
- ✅ Message size up to 1MB (256KB standard, 100MB premium)
- ✅ Dead-letter queue built-in
- ✅ Duplicate detection
- ✅ Scheduled messages
- ⚠️ Higher cost than Storage Queue
- ⚠️ More complex setup

**What Gets Registered:**

```csharp
services.AddHoneyDrunkServiceBusTransport(...)
// Registers:
// - ServiceBusTransportPublisher as ITransportPublisher
// - ServiceBusTransportConsumer as ITransportConsumer
// - ServiceBusTopology as ITransportTopology
// - ServiceBusClient (shared)
// - BlobFallbackStore (if enabled)
```

---

## AzureServiceBusOptions.cs

Configuration for Azure Service Bus transport. Extends `TransportOptions`.

```csharp
// Simplified excerpt. Full type documented in Configuration → AzureServiceBusOptions.
public sealed class AzureServiceBusOptions : TransportOptions
{
    // Authentication (choose one)
    public string FullyQualifiedNamespace { get; set; } = string.Empty;  // Managed identity
    public string? ConnectionString { get; set; }                        // Connection string
    
    // Entity configuration
    public ServiceBusEntityType EntityType { get; set; } = ServiceBusEntityType.Queue;
    public string? SubscriptionName { get; set; }
    
    // Processing
    public TimeSpan MaxWaitTime { get; set; } = TimeSpan.FromSeconds(60);
    public ServiceBusRetryOptions ServiceBusRetry { get; set; } = new();
    
    // Reliability
    public bool EnableDeadLetterQueue { get; set; } = true;
    public int MaxDeliveryCount { get; set; } = 10;
    
    // Fallback
    public BlobFallbackOptions BlobFallback { get; set; } = new();
}
```

> **Full Definition:** See [Configuration → AzureServiceBusOptions](Configuration.md#azureservicebusoptionscs) for all properties including `ServiceBusRetryOptions`, session settings, and validation rules.

### Usage Example

```csharp
services.AddHoneyDrunkServiceBusTransport(options =>
{
    // Authentication (managed identity recommended)
    options.FullyQualifiedNamespace = "mynamespace.servicebus.windows.net";
    // OR: options.ConnectionString = configuration["ServiceBus:ConnectionString"];
    
    options.Address = "orders";
    options.EntityType = ServiceBusEntityType.Topic;
    options.SubscriptionName = "order-processor";
    
    // Sessions for ordered processing
    options.SessionEnabled = true;
    options.MaxConcurrency = 10;
    
    // Blob fallback for publish failures
    options.BlobFallback.Enabled = true;
    options.BlobFallback.AccountUrl = "https://myaccount.blob.core.windows.net";
    options.BlobFallback.ContainerName = "transport-fallback";
});
```

[↑ Back to top](#table-of-contents)

---

## ServiceBusTransportPublisher.cs

Standard `ITransportPublisher` implementation for Azure Service Bus.

```csharp
internal sealed class ServiceBusTransportPublisher(
    ServiceBusClient client,
    IOptions<AzureServiceBusOptions> options,
    IBlobFallbackStore? blobFallback,
    ILogger<ServiceBusTransportPublisher> logger) 
    : ITransportPublisher, IAsyncDisposable
{
    public Task PublishAsync(
        ITransportEnvelope envelope,
        IEndpointAddress destination,
        PublishOptions? options = null,
        CancellationToken cancellationToken = default);
    
    public Task PublishAsync(
        IEnumerable<ITransportEnvelope> envelopes,
        IEndpointAddress destination,
        PublishOptions? options = null,
        CancellationToken cancellationToken = default);
    
    public ValueTask DisposeAsync();
}
```

### Behavior

- Maps `ITransportEnvelope` to `ServiceBusMessage`
- Reads `SessionId`, `PartitionKey`, `ScheduledEnqueueTime`, `TimeToLive` from `IEndpointAddress`
- Falls back to Blob Storage on publish failure (if `BlobFallback.Enabled`)
- Telemetry via `TransportTelemetry` when `EnableTelemetry = true`

### Usage Example

```csharp
public class OrderService(
    ITransportPublisher publisher,
    EnvelopeFactory factory,
    IMessageSerializer serializer)
{
    public async Task PublishOrderAsync(Order order, CancellationToken ct)
    {
        var envelope = factory.CreateEnvelope<OrderCreated>(
            serializer.Serialize(new OrderCreated(order.Id)));
        
        // Use EndpointAddress for session/partition - NOT headers
        var destination = EndpointAddress.Create(
            name: "orders",
            address: "orders",
            sessionId: order.CustomerId.ToString(),
            partitionKey: order.CustomerId.ToString());
        
        await publisher.PublishAsync(envelope, destination, ct);
    }
}
```

> **Design Note:** Use `IEndpointAddress` properties (`sessionId`, `partitionKey`, `scheduledEnqueueTime`, `timeToLive`) for Service Bus metadata instead of manipulating headers. The publisher maps these to `ServiceBusMessage` properties internally.

[↑ Back to top](#table-of-contents)

---

## ServiceBusTransportConsumer.cs

Standard `ITransportConsumer` implementation for Azure Service Bus.

```csharp
internal sealed class ServiceBusTransportConsumer(
    ServiceBusClient client,
    IMessagePipeline pipeline,
    IOptions<AzureServiceBusOptions> options,
    ILogger<ServiceBusTransportConsumer> logger) 
    : ITransportConsumer, IAsyncDisposable
{
    public Task StartAsync(CancellationToken cancellationToken = default);
    public Task StopAsync(CancellationToken cancellationToken = default);
    public ValueTask DisposeAsync();
}
```

### Behavior

- Registered as `ITransportConsumer` and started by `TransportRuntimeHost` when the Node starts
- Uses `IMessagePipeline` for handler execution (GridContext → Telemetry → Logging → Handler)
- Maps `ServiceBusReceivedMessage.DeliveryCount` into `MessageContext.DeliveryCount` for error strategies
- Handles `ProcessMessageAsync` and `ProcessErrorAsync` callbacks from the SDK processor

### What StartAsync Does

On `StartAsync`, the consumer:

1. Creates a `ServiceBusProcessor` (or `ServiceBusSessionProcessor` if sessions enabled)
2. Registers `ProcessMessageAsync` callback that:
   - Deserializes `ServiceBusReceivedMessage` to `ITransportEnvelope`
   - Creates `MessageContext` with `DeliveryCount` from the SDK message
   - Calls `IMessagePipeline.ProcessAsync()`
   - Completes, abandons, or dead-letters based on `MessageProcessingResult`
3. Starts the processor

### Usage Example

```csharp
// Registered automatically - TransportRuntimeHost starts the consumer
services.AddHoneyDrunkTransportCore()
    .AddHoneyDrunkServiceBusTransport(options =>
    {
        options.Address = "orders";
        options.SubscriptionName = "order-processor";
    });
```

[↑ Back to top](#table-of-contents)

---

## Sessions Support

Sessions enable ordered processing by grouping messages with the same `SessionId`.

### Configuration

```csharp
services.AddHoneyDrunkServiceBusTransport(options =>
{
    options.Address = "orders";
    options.SessionEnabled = true;
    options.MaxConcurrency = 10;  // Process 10 sessions concurrently
});
```

### Publishing with Session

```csharp
var envelope = factory.CreateEnvelope<OrderCreated>(payload);

// Use EndpointAddress.Create with sessionId parameter
var destination = EndpointAddress.Create(
    name: "orders",
    address: "orders",
    sessionId: order.CustomerId.ToString());

await publisher.PublishAsync(envelope, destination, ct);

// All messages with same SessionId processed in order within that session
```

### Session Validation

- If `SessionEnabled = true` on options, the consumer creates a `ServiceBusSessionProcessor`
- If `SessionEnabled = false`, the `SessionId` property on `IEndpointAddress` is ignored by the publisher (no validation error, just no session semantics)
- `ITransportTopology.SupportsSessions` returns `true` for Service Bus

[↑ Back to top](#table-of-contents)

---

## Topics and Subscriptions

Topics enable fan-out (pub/sub) where multiple subscriptions receive copies of each message.

### Registration Model

The Service Bus transport supports registering **multiple consumers** with different subscription configurations. Each call to `AddHoneyDrunkServiceBusTransport` with a different `SubscriptionName` registers a separate `ITransportConsumer`.

```csharp
// Step 1: Register core transport
builder.Services.AddHoneyDrunkTransportCore(options =>
{
    options.EnableTelemetry = true;
});

// Step 2: Register Service Bus with topic configuration
// The publisher is shared; each subscription gets its own consumer

// Publisher + Consumer for subscription "order-processor"
builder.Services.AddHoneyDrunkServiceBusTransport(options =>
{
    options.FullyQualifiedNamespace = "mynamespace.servicebus.windows.net";
    options.Address = "orders";  // Topic name
    options.EntityType = ServiceBusEntityType.Topic;
    options.SubscriptionName = "order-processor";
});

// Additional consumer for subscription "analytics"
builder.Services.AddHoneyDrunkServiceBusTransport(options =>
{
    options.FullyQualifiedNamespace = "mynamespace.servicebus.windows.net";
    options.Address = "orders";
    options.EntityType = ServiceBusEntityType.Topic;
    options.SubscriptionName = "analytics";
});

// Both consumers receive all messages published to the "orders" topic
```

> **Note:** If your application only publishes (no consumption), use a single registration without `SubscriptionName`. If you need multiple subscriptions in the same process, register once per subscription.

[↑ Back to top](#table-of-contents)

---

## Blob Fallback

When Service Bus publish fails (outage, throttling, size exceeded), messages are persisted to Blob Storage for later replay.

> **This is a first-class feature** of the Service Bus transport, controlled by `BlobFallbackOptions` on `AzureServiceBusOptions`. The publisher wraps its `SendMessageAsync` calls in fallback logic automatically.

### Configuration

```csharp
services.AddHoneyDrunkServiceBusTransport(options =>
{
    options.FullyQualifiedNamespace = "mynamespace.servicebus.windows.net";
    options.Address = "orders";
    
    // Enable blob fallback (first-class feature)
    options.BlobFallback.Enabled = true;
    options.BlobFallback.AccountUrl = "https://myaccount.blob.core.windows.net";
    options.BlobFallback.ContainerName = "transport-fallback";
    options.BlobFallback.BlobPrefix = "servicebus";
    options.BlobFallback.TimeToLive = TimeSpan.FromDays(7);
});
```

> **Full BlobFallbackOptions:** See [Configuration → BlobFallbackOptions](Configuration.md#blobfallbackoptionscs) for all properties including `ConnectionString` vs `AccountUrl`, `TimeToLive`, and container naming.

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
    "name": "orders",
    "address": "orders",
    "sessionId": "customer-123"
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
                var destination = EndpointAddress.Create(
                    record.Destination.Name,
                    record.Destination.Address,
                    sessionId: record.Destination.SessionId);
                
                await publisher.PublishAsync(envelope, destination, ct);
                
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

[↑ Back to top](#table-of-contents)

---

## Dead-Letter Queue

Messages are moved to the dead-letter queue (DLQ) when:
- Handler returns `MessageProcessingResult.DeadLetter`
- Delivery count exceeds `MaxDeliveryCount` (as decided by `IErrorHandlingStrategy`)
- Message processing throws and the error strategy returns `ErrorHandlingDecision.DeadLetter`

```csharp
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

[↑ Back to top](#table-of-contents)

---

## Scheduled Messages

Service Bus supports scheduled message delivery via `ScheduledEnqueueTime`.

### Using EndpointAddress (Recommended)

```csharp
var envelope = factory.CreateEnvelope<OrderReminder>(payload);

var destination = EndpointAddress.Create(
    name: "reminders",
    address: "reminders",
    scheduledEnqueueTime: DateTimeOffset.UtcNow.AddHours(24));

await publisher.PublishAsync(envelope, destination, ct);
```

### Using PublishOptions

```csharp
var envelope = factory.CreateEnvelope<OrderReminder>(payload);
var destination = EndpointAddress.Create("reminders", "reminders");

var options = new PublishOptions
{
    ScheduledEnqueueTime = DateTimeOffset.UtcNow.AddHours(24)
};

await publisher.PublishAsync(envelope, destination, options, ct);
```

> **Abstraction Note:** Both `IEndpointAddress.ScheduledEnqueueTime` and `PublishOptions.ScheduledEnqueueTime` are mapped to `ServiceBusMessage.ScheduledEnqueueTime` by the publisher. Use whichever fits your publishing pattern.

[↑ Back to top](#table-of-contents)

---

## Complete Setup Example

```csharp
var builder = WebApplication.CreateBuilder(args);

// 1. Register Kernel
builder.Services.AddHoneyDrunkCoreNode(nodeDescriptor);

// 2. Register Transport Core + Service Bus
builder.Services
    .AddHoneyDrunkTransportCore(options =>
    {
        options.EnableTelemetry = true;
        options.EnableLogging = true;
        options.EnableCorrelation = true;
    })
    .AddHoneyDrunkServiceBusTransport(options =>
    {
        // Authentication (managed identity recommended)
        options.FullyQualifiedNamespace = builder.Configuration["ServiceBus:Namespace"];
        
        // Entity configuration
        options.Address = "orders";
        options.EntityType = ServiceBusEntityType.Topic;
        options.SubscriptionName = "order-processor";
        
        // Sessions
        options.SessionEnabled = true;
        options.MaxConcurrency = 10;
        
        // Blob fallback
        options.BlobFallback.Enabled = true;
        options.BlobFallback.AccountUrl = builder.Configuration["Blob:AccountUrl"];
        options.BlobFallback.ContainerName = "transport-fallback";
    })
    .WithRetry(retry =>
    {
        retry.MaxAttempts = 5;
        retry.InitialDelay = TimeSpan.FromSeconds(1);
        retry.BackoffStrategy = BackoffStrategy.Exponential;
    });

// 3. Register handlers
builder.Services.AddMessageHandler<OrderCreated, OrderCreatedHandler>();
builder.Services.AddMessageHandler<OrderUpdated, OrderUpdatedHandler>();

var app = builder.Build();
app.Run();  // TransportRuntimeHost starts ServiceBusTransportConsumer
```

[↑ Back to top](#table-of-contents)

---

[← Back to File Guide](FILE_GUIDE.md) | [↑ Back to top](#table-of-contents)
