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
- [ITransportTransaction.cs](#itransporttransactioncs)
- [NoOpTransportTransaction.cs](#nooptransporttransactioncs)
- [IEndpointAddress.cs](#iendpointaddresscs)
- [EndpointAddress.cs](#endpointaddresscs)
- [IMessageSerializer.cs](#imessageserializercs)

---

## Overview

Core interfaces that define the transport abstraction layer. These contracts enable transport-agnostic messaging where applications depend on interfaces rather than specific broker implementations.

**Location:** `HoneyDrunk.Transport/Abstractions/`

**Key Abstractions:**
- **IMessagePublisher** - Primary application-facing API for publishing typed messages
- **ITransportPublisher** - Low-level transport envelope publisher
- **IMessageHandler<T>** - Interface-based message handler pattern
- **MessageHandler<T>** - Delegate-based functional handler pattern
- **ITransportTransaction** - Transaction context for message processing

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

High-level typed message publisher for application-level messaging. This is the **primary abstraction** for Data and Service layers to publish domain events and commands without coupling to transport-specific envelope structures.

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
- Publishing from application/service/data layers
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
        CancellationToken cancellationToken = default);
    
    Task PublishAsync(
        IEnumerable<ITransportEnvelope> envelopes,
        IEndpointAddress destination,
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
