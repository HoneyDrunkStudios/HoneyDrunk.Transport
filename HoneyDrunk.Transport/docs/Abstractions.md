# 📋 Abstractions - Core Contracts

[← Back to File Guide](FILE_GUIDE.md)

---

## Table of Contents

- [Overview](#overview)
- [ITransportEnvelope.cs](#itransportenvelopecs)
- [IMessagePublisher.cs](#imessagepublishercs)
- [ITransportPublisher.cs](#itransportpublishercs)
- [ITransportConsumer.cs](#itransportconsumercs)
- [IMessageReceiver.cs](#imessagereceivercs)
- [IMessageHandler\<TMessage\>.cs](#imessagehandlertmessagecs)
- [MessageHandler\<TMessage\>.cs](#messagehandlertmessagecs-1)
- [MessageContext.cs](#messagecontextcs)
- [MessageProcessingResult.cs](#messageprocessingresultcs)
- [MessageProcessingFailure.cs](#messageprocessingfailurecs)
- [ITransportTransaction.cs](#itransporttransactioncs)
- [NoOpTransportTransaction.cs](#nooptransporttransactioncs)
- [ITransportTopology.cs](#itransporttopologycs)
- [IEndpointAddress.cs](#iendpointaddresscs)
- [EndpointAddress.cs](#endpointaddresscs)
- [PublishOptions.cs](#publishoptionscs)
- [MessagePriority.cs](#messageprioritycs)
- [IMessageSerializer.cs](#imessageserializercs)

---

## Overview

Core interfaces and transport-agnostic types that define the transport abstraction layer. This folder contains both pure interfaces and shared contract types that all transports and handlers rely on. These contracts enable transport-agnostic messaging where applications depend on interfaces rather than specific broker implementations.

**Location:** `HoneyDrunk.Transport/Abstractions/`

**Key Abstractions:**
- **IMessagePublisher** - Primary application/service-facing API for publishing typed messages with Grid context
- **ITransportPublisher** - Low-level transport envelope publisher
- **IMessageHandler<T>** - Interface-based message handler pattern
- **MessageHandler<T>** - Delegate type for functional/inline handlers
- **ITransportTransaction** - Transaction context for message processing
- **ITransportTopology** - Declares transport adapter capabilities

**Shared Contract Types:**
- **MessageContext** - Handler context with envelope, transaction, and Grid context
- **MessageProcessingResult** - Handler return type (Success, Retry, DeadLetter, Abandon)
- **MessageProcessingFailure** - Structured error metadata for diagnostics
- **EndpointAddress** - Transport-agnostic endpoint address with typed metadata
- **PublishOptions** - Publishing customization (TTL, scheduling, priority)
- **MessagePriority** - Priority enumeration for supported transports

[↑ Back to top](#table-of-contents)

---

## ITransportEnvelope.cs

```csharp
public interface ITransportEnvelope
{
    string MessageId { get; }
    string? CorrelationId { get; }
    string? CausationId { get; }
    string? NodeId { get; }
    string? StudioId { get; }
    string? TenantId { get; }      // Kernel v0.3.0+
    string? ProjectId { get; }     // Kernel v0.3.0+
    string? Environment { get; }
    DateTimeOffset Timestamp { get; }
    string MessageType { get; }
    IReadOnlyDictionary<string, string> Headers { get; }
    ReadOnlyMemory<byte> Payload { get; }
    
    ITransportEnvelope WithHeaders(IDictionary<string, string> additionalHeaders);
    ITransportEnvelope WithGridContext(
        string? correlationId = null,
        string? causationId = null,
        string? nodeId = null,
        string? studioId = null,
        string? tenantId = null,      // Kernel v0.3.0+
        string? projectId = null,     // Kernel v0.3.0+
        string? environment = null);
}
```

### Usage Example

```csharp
public async Task<MessageProcessingResult> HandleAsync(
    OrderCreated message,
    MessageContext context,
    CancellationToken ct)
{
    var envelope = context.Envelope;
    _logger.LogInformation(
        "Processing {MessageType} from Node {NodeId} in Tenant {TenantId}, Project {ProjectId}",
        envelope.MessageType,
        envelope.NodeId,
        envelope.TenantId,
        envelope.ProjectId);
    
    return MessageProcessingResult.Success;
}
```

[↑ Back to top](#table-of-contents)

---

## IMessagePublisher.cs

**⭐ Primary application-facing abstraction for publishing messages**

```csharp
public interface IMessagePublisher
{
    Task PublishAsync<T>(
        string destination,
        T message,
        IGridContext gridContext,
        CancellationToken cancellationToken = default)
        where T : class;
    
    Task PublishBatchAsync<T>(
        string destination,
        IEnumerable<T> messages,
        IGridContext gridContext,
        CancellationToken cancellationToken = default)
        where T : class;
}
```

### Purpose

High-level typed message publisher for application-level messaging. This is the **primary abstraction** for application and service layers to publish domain events and commands without coupling to transport-specific envelope structures.

### Key Features

- **Typed messaging** - Accepts raw POCO messages
- **Automatic serialization** - Handles message serialization internally
- **Grid context propagation** - Automatically propagates correlation, causation, node, tenant, and project metadata
- **Envelope creation** - Wraps messages in transport envelopes behind the scenes
- **Batch support** - Efficient batch publishing for high-throughput scenarios

### Usage Example

```csharp
public class OrderService(
    IMessagePublisher publisher,
    IGridContext gridContext)
{
    public async Task CreateOrderAsync(CreateOrderCommand command, CancellationToken ct)
    {
        // Process order creation...
        var order = await _repository.CreateOrderAsync(command, ct);
        
        // Publish domain event - simple and clean!
        var orderCreated = new OrderCreated(order.Id, order.CustomerId);
        await publisher.PublishAsync(
            destination: "orders.created",
            message: orderCreated,
            gridContext: gridContext,
            cancellationToken: ct);
    }
    
    public async Task PublishOrderNotificationsAsync(
        IEnumerable<OrderNotification> notifications,
        CancellationToken ct)
    {
        // Batch publish for efficiency
        await publisher.PublishBatchAsync(
            destination: "notifications",
            messages: notifications,
            gridContext: gridContext,
            cancellationToken: ct);
    }
}
```

### When to Use

✅ **Use IMessagePublisher when:**
- Publishing from application or service layers
- Working with typed domain events or commands
- You want automatic Grid context propagation
- You need a clean, decoupled API

❌ **Use ITransportPublisher instead when:**
- Building transport middleware or infrastructure
- You need full control over envelope creation
- Working with pre-serialized payloads

[↑ Back to top](#table-of-contents)

---

## ITransportPublisher.cs

**Low-level transport envelope publisher**

```csharp
public interface ITransportPublisher
{
    Task PublishAsync(
        ITransportEnvelope envelope,
        IEndpointAddress destination,
        PublishOptions? options = null,
        CancellationToken cancellationToken = default);
    
    Task PublishAsync(
        IEnumerable<ITransportEnvelope> envelopes,
        IEndpointAddress destination,
        PublishOptions? options = null,
        CancellationToken cancellationToken = default);
}
```

### Purpose

Low-level abstraction for publishing pre-constructed transport envelopes. Used by infrastructure code, middleware, and transport implementations.

### Usage Example

```csharp
public class OrderService(
    ITransportPublisher publisher,
    EnvelopeFactory factory,
    IMessageSerializer serializer,
    IGridContext gridContext)
{
    public async Task PublishOrderCreatedAsync(Order order, CancellationToken ct)
    {
        var message = new OrderCreated(order.Id);
        var payload = serializer.Serialize(message);
        var envelope = factory.CreateEnvelopeWithGridContext<OrderCreated>(
            payload, gridContext);
        
        var destination = EndpointAddress.Create("orders", "orders.created");
        await publisher.PublishAsync(envelope, destination, ct);
    }
}
```

### When to Use

- Building transport middleware
- Custom envelope manipulation
- Infrastructure-level message routing
- Pre-serialized payload scenarios

[↑ Back to top](#table-of-contents)

---

## ITransportConsumer.cs

```csharp
public interface ITransportConsumer
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}
```

### Usage Example

```csharp
// Registered automatically by transport implementation
services.AddHoneyDrunkTransportCore()
    .AddHoneyDrunkServiceBusTransport(options => { /* ... */ });
```

[↑ Back to top](#table-of-contents)

---

## IMessageReceiver.cs

```csharp
public interface IMessageReceiver
{
    Task<MessageProcessingResult> ReceiveAsync(
        ITransportEnvelope envelope,
        ITransportTransaction transaction,
        CancellationToken cancellationToken = default);
}
```

### Purpose

Receives and processes individual message envelopes. This is the entry point for the message processing pipeline.

### Usage Example

```csharp
// Implemented internally by MessagePipeline
// Transport implementations call ReceiveAsync when messages arrive:

public class ServiceBusConsumer : ITransportConsumer
{
    private readonly IMessageReceiver _receiver;
    
    private async Task ProcessMessageAsync(ServiceBusReceivedMessage message)
    {
        var envelope = MapToTransportEnvelope(message);
        var transaction = new ServiceBusTransaction(message);
        
        var result = await _receiver.ReceiveAsync(envelope, transaction, ct);
        
        switch (result)
        {
            case MessageProcessingResult.Success:
                await transaction.CommitAsync(ct);
                break;
            case MessageProcessingResult.Retry:
                await transaction.RollbackAsync(ct);
                break;
            case MessageProcessingResult.DeadLetter:
                await DeadLetterAsync(message, ct);
                break;
        }
    }
}
```

### When to Use

- Building custom transport implementations
- Integrating with new message brokers
- Infrastructure-level message routing

[↑ Back to top](#table-of-contents)

---

## IMessageHandler<TMessage>.cs

**Interface-based message handler pattern**

```csharp
public interface IMessageHandler<TMessage> where TMessage : class
{
    Task<MessageProcessingResult> HandleAsync(
        TMessage message,
        MessageContext context,
        CancellationToken cancellationToken);
}
```

### Usage Example

```csharp
public class OrderCreatedHandler : IMessageHandler<OrderCreated>
{
    private readonly ILogger<OrderCreatedHandler> _logger;
    private readonly IOrderRepository _repository;
    
    public OrderCreatedHandler(
        ILogger<OrderCreatedHandler> logger,
        IOrderRepository repository)
    {
        _logger = logger;
        _repository = repository;
    }
    
    public async Task<MessageProcessingResult> HandleAsync(
        OrderCreated message,
        MessageContext context,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "Processing order {OrderId} with correlation {CorrelationId}",
            message.OrderId,
            context.GridContext?.CorrelationId);
        
        await _repository.MarkOrderAsProcessedAsync(message.OrderId, ct);
        return MessageProcessingResult.Success;
    }
}

// Registration
services.AddMessageHandler<OrderCreated, OrderCreatedHandler>();
```

### Benefits

- Dependency injection support
- Testable and mockable
- Class-based handlers with state
- Full access to DI container

[↑ Back to top](#table-of-contents)

---

## MessageHandler<TMessage>.cs

**Delegate-based functional handler pattern**

```csharp
public delegate Task MessageHandler<in TMessage>(
    TMessage message,
    MessageContext context,
    CancellationToken cancellationToken)
    where TMessage : class;
```

### Purpose

Functional delegate for inline message handlers. Provides a lightweight alternative to interface-based handlers.

### Usage Example

```csharp
// Inline functional handler
services.AddMessageHandler<OrderCreated>(async (message, context, ct) =>
{
    _logger.LogInformation("Order created: {OrderId}", message.OrderId);
    await ProcessOrderAsync(message, ct);
});

// Named method handler
services.AddMessageHandler<PaymentProcessed>(HandlePaymentProcessed);

async Task HandlePaymentProcessed(
    PaymentProcessed message,
    MessageContext context,
    CancellationToken ct)
{
    await _paymentService.RecordPaymentAsync(message.PaymentId, ct);
}
```

### Benefits

- Lightweight and concise
- No class boilerplate needed
- Great for simple handlers
- Closures for external dependencies

[↑ Back to top](#table-of-contents)

---

## MessageContext.cs

**Handler context with envelope, transaction, and Grid context**

```csharp
public sealed class MessageContext
{
    public required ITransportEnvelope Envelope { get; init; }
    public IGridContext? GridContext { get; set; }
    public required ITransportTransaction Transaction { get; init; }
    public int DeliveryCount { get; init; }
    public Dictionary<string, object> Properties { get; init; } = [];
}
```

### Purpose

Provides handlers with access to message metadata, transaction context, and distributed tracing information. The `GridContext` is populated by `GridContextPropagationMiddleware` from envelope metadata.

### Usage Example

```csharp
public async Task<MessageProcessingResult> HandleAsync(
    OrderCreated message,
    MessageContext context,
    CancellationToken ct)
{
    // Access envelope metadata
    var messageId = context.Envelope.MessageId;
    var messageType = context.Envelope.MessageType;
    
    // Access Grid context for distributed tracing
    var correlationId = context.GridContext?.CorrelationId;
    var nodeId = context.GridContext?.NodeId;
    var tenantId = context.GridContext?.TenantId;
    
    // Check delivery count for retry logic
    if (context.DeliveryCount > 3)
    {
        _logger.LogWarning("Message {MessageId} has been retried {Count} times",
            messageId, context.DeliveryCount);
    }
    
    // Access middleware-set properties
    if (context.Properties.TryGetValue("ProcessingStartTime", out var startTime))
    {
        // Use processing start time
    }
    
    return MessageProcessingResult.Success;
}
```

[↑ Back to top](#table-of-contents)

---

## MessageProcessingResult.cs

**Handler return type enumeration**

```csharp
public enum MessageProcessingResult
{
    Success,
    Retry,
    DeadLetter,
    Abandon
}
```

### Purpose

Defines the possible outcomes of message processing. The transport adapter uses this result to determine how to handle the message after processing.

### Values

| Value | Description | Transport Action |
|-------|-------------|------------------|
| `Success` | Message processed successfully | Complete/acknowledge the message |
| `Retry` | Processing failed, retry later | Abandon for redelivery with backoff |
| `DeadLetter` | Permanent failure, don't retry | Move to dead-letter queue |
| `Abandon` | Release message back to queue | Abandon without incrementing delivery count |

### Usage Example

```csharp
public async Task<MessageProcessingResult> HandleAsync(
    OrderCreated message,
    MessageContext context,
    CancellationToken ct)
{
    try
    {
        await ProcessOrderAsync(message, ct);
        return MessageProcessingResult.Success;
    }
    catch (TransientException ex)
    {
        _logger.LogWarning(ex, "Transient failure, will retry");
        return MessageProcessingResult.Retry;
    }
    catch (ValidationException ex)
    {
        _logger.LogError(ex, "Validation failed, moving to DLQ");
        return MessageProcessingResult.DeadLetter;
    }
}
```

[↑ Back to top](#table-of-contents)

---

## MessageProcessingFailure.cs

**Structured error metadata for diagnostics**

> **Contract Note:** Handlers currently return `MessageProcessingResult` as the stable contract. `MessageProcessingFailure` provides richer error metadata used by error handling strategies and telemetry, and may become a first-class handler return type in a future major version.

```csharp
public sealed class MessageProcessingFailure
{
    public required MessageProcessingResult Result { get; init; }
    public string? Reason { get; init; }
    public string? Category { get; init; }
    public Exception? Exception { get; init; }
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }

    public static MessageProcessingFailure Success();
    public static MessageProcessingFailure Retry(
        string reason,
        Exception? exception = null,
        string? category = null,
        IReadOnlyDictionary<string, object>? metadata = null);
    public static MessageProcessingFailure DeadLetter(
        string reason,
        string? category = null,
        Exception? exception = null,
        IReadOnlyDictionary<string, object>? metadata = null);
    public static MessageProcessingFailure FromException(
        Exception exception,
        int deliveryCount,
        int maxRetries = 5);
}
```

### Purpose

Provides richer error context than the simple `MessageProcessingResult` enum. Used by error handling strategies and telemetry for diagnostics, categorization, and analytics.

### Key Features

- **Result** - The underlying processing result (Success, Retry, DeadLetter)
- **Reason** - Human-readable explanation of the failure
- **Category** - Grouping key for analytics (e.g., "Validation", "Timeout", "DatabaseError")
- **Exception** - The underlying exception if applicable
- **Metadata** - Additional diagnostic data

### Usage Example

Example of using `MessageProcessingFailure` in error handling strategies, custom pipelines, or future handler patterns:

```csharp
public async Task<MessageProcessingFailure> HandleWithFailureAsync(
    OrderCreated message,
    MessageContext context,
    CancellationToken ct)
{
    try
    {
        await ProcessOrderAsync(message, ct);
        return MessageProcessingFailure.Success();
    }
    catch (TimeoutException ex)
    {
        return MessageProcessingFailure.Retry(
            reason: "Database timeout during order processing",
            exception: ex,
            category: "Timeout",
            metadata: new Dictionary<string, object>
            {
                ["OrderId"] = message.OrderId,
                ["Attempt"] = context.DeliveryCount
            });
    }
    catch (OrderValidationException ex)
    {
        return MessageProcessingFailure.DeadLetter(
            reason: ex.Message,
            category: "Validation",
            exception: ex);
    }
}

// Or use automatic categorization from exception
catch (Exception ex)
{
    return MessageProcessingFailure.FromException(
        exception: ex,
        deliveryCount: context.DeliveryCount,
        maxRetries: 5);
}
```

[↑ Back to top](#table-of-contents)

---

## ITransportTransaction.cs

**Transaction context for message processing**

```csharp
public interface ITransportTransaction
{
    string TransactionId { get; }
    IReadOnlyDictionary<string, object> Context { get; }
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
```

### Purpose

Abstracts transport-specific transaction semantics. Allows middleware and handlers to participate in message acknowledgment decisions.

### Usage Example

```csharp
// Transport implementations create transactions:
public class ServiceBusTransportTransaction : ITransportTransaction
{
    private readonly ProcessMessageEventArgs _args;
    
    public string TransactionId { get; }
    public IReadOnlyDictionary<string, object> Context { get; }
    
    public async Task CommitAsync(CancellationToken ct)
    {
        await _args.CompleteMessageAsync(_args.Message, ct);
    }
    
    public async Task RollbackAsync(CancellationToken ct)
    {
        await _args.AbandonMessageAsync(_args.Message, cancellationToken: ct);
    }
}

// Handlers can access the transaction via MessageContext:
public async Task<MessageProcessingResult> HandleAsync(
    OrderCreated message,
    MessageContext context,
    CancellationToken ct)
{
    // Transaction is available for advanced scenarios
    var txId = context.Transaction.TransactionId;
    
    // Processing...
    return MessageProcessingResult.Success;
}
```

[↑ Back to top](#table-of-contents)

---

## NoOpTransportTransaction.cs

**Transport-agnostic no-op transaction implementation**

```csharp
public sealed class NoOpTransportTransaction : ITransportTransaction
{
    public static readonly ITransportTransaction Instance = new NoOpTransportTransaction();
    
    public string TransactionId { get; }
    public IReadOnlyDictionary<string, object> Context => EmptyContext;
    
    public Task CommitAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;
    
    public Task RollbackAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
```

### Purpose

Default transaction implementation for transports that don't support transactions (e.g., InMemory transport) or for testing scenarios.

### Usage Example

```csharp
// Use in tests
var context = new MessageContext
{
    Envelope = testEnvelope,
    Transaction = NoOpTransportTransaction.Instance,
    DeliveryCount = 1
};

// InMemory transport uses this by default
var result = await handler.HandleAsync(message, context, ct);
```

[↑ Back to top](#table-of-contents)

---

## ITransportTopology.cs

**Declares transport adapter capabilities**

```csharp
public interface ITransportTopology
{
    string Name { get; }
    bool SupportsTopics { get; }
    bool SupportsSubscriptions { get; }
    bool SupportsSessions { get; }
    bool SupportsOrdering { get; }
    bool SupportsScheduledMessages { get; }
    bool SupportsBatching { get; }
    bool SupportsTransactions { get; }
    bool SupportsDeadLetterQueue { get; }
    bool SupportsPriority { get; }
    long? MaxMessageSize { get; }
}
```

### Purpose

Declares what features a transport adapter supports. Used for runtime validation and feature detection to prevent silent failures when using unsupported features.

### Implementations

| Transport | Topics | Sessions | Ordering | Scheduled | Transactions | DLQ | Priority | Max Size |
|-----------|--------|----------|----------|-----------|--------------|-----|----------|----------|
| **ServiceBus** | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ | 256KB/100MB |
| **StorageQueue** | ❌ | ❌ | ❌ | ✅ | ❌ | ❌ | ❌ | 64KB |
| **InMemory** | ✅ | ❌ | ✅ | ❌ | ❌ | ✅ | ❌ | null |

### Usage Example

```csharp
public class TopologyAwarePublisher(
    ITransportPublisher publisher,
    ITransportTopology topology,
    EnvelopeFactory envelopeFactory)
{
    public async Task PublishWithSessionAsync<T>(
        ITransportEnvelope envelope,
        string sessionId,
        CancellationToken ct)
    {
        if (!topology.SupportsSessions)
        {
            throw new NotSupportedException(
                $"Transport '{topology.Name}' does not support sessions");
        }
        
        var destination = EndpointAddress.Create(
            name: "orders",
            address: "orders",
            sessionId: sessionId);
        
        await publisher.PublishAsync(envelope, destination, options: null, ct);
    }
}
```

[↑ Back to top](#table-of-contents)

---

## IEndpointAddress.cs

**Logical transport endpoint address contract**

```csharp
public interface IEndpointAddress
{
    string Name { get; }
    string Address { get; }
    string? SessionId { get; }
    string? PartitionKey { get; }
    DateTimeOffset? ScheduledEnqueueTime { get; }
    TimeSpan? TimeToLive { get; }
    IReadOnlyDictionary<string, string> AdditionalProperties { get; }
}
```

### Purpose

Represents a logical transport endpoint with typed metadata. Provides a clean API for specifying destination addresses with optional routing metadata.

### Properties

| Property | Description | Used By |
|----------|-------------|---------|
| `Name` | Logical endpoint name | All transports |
| `Address` | Physical address (queue/topic name) | All transports |
| `SessionId` | Session for ordered processing | ServiceBus |
| `PartitionKey` | Partition for ordering within partition | ServiceBus |
| `ScheduledEnqueueTime` | Delayed delivery time | ServiceBus, StorageQueue |
| `TimeToLive` | Message expiration | ServiceBus, StorageQueue |
| `AdditionalProperties` | Adapter-specific extensions | Varies |

[↑ Back to top](#table-of-contents)

---

## EndpointAddress.cs

**Transport-agnostic endpoint address implementation**

```csharp
public sealed class EndpointAddress : IEndpointAddress
{
    public string Name { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public string? SessionId { get; init; }
    public string? PartitionKey { get; init; }
    public DateTimeOffset? ScheduledEnqueueTime { get; init; }
    public TimeSpan? TimeToLive { get; init; }
    public IReadOnlyDictionary<string, string> AdditionalProperties { get; init; }

    public static IEndpointAddress Create(string name, string address);
    public static IEndpointAddress Create(
        string name,
        string address,
        string? sessionId = null,
        string? partitionKey = null,
        DateTimeOffset? scheduledEnqueueTime = null,
        TimeSpan? timeToLive = null,
        IDictionary<string, string>? additionalProperties = null);
}
```

### Usage Example

```csharp
// Simple endpoint
var simple = EndpointAddress.Create("orders", "orders-queue");

// Session-based ordering
var sessionBased = EndpointAddress.Create(
    name: "orders",
    address: "orders-topic",
    sessionId: customerId.ToString());

// Scheduled delivery with TTL
var scheduled = EndpointAddress.Create(
    name: "reminders",
    address: "reminders-queue",
    scheduledEnqueueTime: DateTimeOffset.UtcNow.AddHours(24),
    timeToLive: TimeSpan.FromDays(7));

// Partitioned with custom properties
var partitioned = EndpointAddress.Create(
    name: "events",
    address: "events-topic",
    partitionKey: region,
    additionalProperties: new Dictionary<string, string>
    {
        ["ContentType"] = "application/json"
    });
```

[↑ Back to top](#table-of-contents)

---

## PublishOptions.cs

**Publishing customization options**

```csharp
public sealed class PublishOptions
{
    public TimeSpan? TimeToLive { get; init; }
    public DateTimeOffset? ScheduledEnqueueTime { get; init; }
    public string? PartitionKey { get; init; }
    public string? SessionId { get; init; }
    public MessagePriority Priority { get; init; } = MessagePriority.Normal;
    public IReadOnlyDictionary<string, object>? AdditionalOptions { get; init; }
}
```

### Purpose

Allows customization of message delivery behavior at publish time. Not all properties are supported by all transport adapters - check `ITransportTopology` for capabilities.

### Usage Example

```csharp
var options = new PublishOptions
{
    TimeToLive = TimeSpan.FromHours(24),
    ScheduledEnqueueTime = DateTimeOffset.UtcNow.AddMinutes(30),
    SessionId = customerId.ToString(),
    Priority = MessagePriority.High
};

await publisher.PublishAsync(envelope, destination, options, ct);
```

[↑ Back to top](#table-of-contents)

---

## MessagePriority.cs

**Priority enumeration for supported transports**

```csharp
public enum MessagePriority
{
    Low = 0,
    Normal = 1,
    High = 2
}
```

### Purpose

Defines message priority levels for transport adapters that support prioritization. Higher priority messages are processed before lower priority messages.

### Note

Not all transports support message priority. Check `ITransportTopology.SupportsPriority` before relying on this feature.

### Usage Example

```csharp
var options = new PublishOptions
{
    Priority = MessagePriority.High
};

// Critical order - process first
await publisher.PublishAsync(
    urgentOrderEnvelope,
    destination,
    options,
    ct);
```

[↑ Back to top](#table-of-contents)

---

## IMessageSerializer.cs

**Serialization contract for message payloads**

```csharp
public interface IMessageSerializer
{
    string ContentType { get; }
    ReadOnlyMemory<byte> Serialize<TMessage>(TMessage message) where TMessage : class;
    TMessage Deserialize<TMessage>(ReadOnlyMemory<byte> payload) where TMessage : class;
    object Deserialize(ReadOnlyMemory<byte> payload, Type messageType);
}
```

### Purpose

Abstracts message serialization/deserialization. The default implementation uses `System.Text.Json`, but custom serializers can be registered for other formats.

### Usage Example

```csharp
// Default JSON serializer is registered automatically
services.AddHoneyDrunkTransportCore();

// Custom serializer registration
services.AddSingleton<IMessageSerializer, MessagePackSerializer>();

// Using the serializer
public class EnvelopeBuilder(IMessageSerializer serializer)
{
    public ITransportEnvelope CreateEnvelope<T>(T message) where T : class
    {
        var payload = serializer.Serialize(message);
        return new TransportEnvelope
        {
            MessageId = Guid.NewGuid().ToString(),
            MessageType = typeof(T).FullName!,
            Payload = payload,
            Timestamp = DateTimeOffset.UtcNow
        };
    }
}
```

---

[← Back to File Guide](FILE_GUIDE.md) | [↑ Back to top](#table-of-contents)