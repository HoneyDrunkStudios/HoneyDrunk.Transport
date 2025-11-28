# 📋 Abstractions - Core Contracts

[← Back to File Guide](FILE_GUIDE.md)

---

## Overview

Core interfaces that define the transport abstraction layer. These contracts enable transport-agnostic messaging where applications depend on interfaces rather than specific broker implementations.

**Location:** `HoneyDrunk.Transport/Abstractions/`

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

---

## ITransportPublisher.cs

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

### Usage Example

```csharp
public class OrderService(
    ITransportPublisher publisher,
    EnvelopeFactory factory,
    IGridContext gridContext)
{
    public async Task PublishOrderCreatedAsync(Order order, CancellationToken ct)
    {
        var message = new OrderCreated(order.Id);
        var payload = _serializer.Serialize(message);
        var envelope = factory.CreateEnvelopeWithGridContext<OrderCreated>(
            payload, gridContext);
        
        await publisher.PublishAsync(envelope, new EndpointAddress("orders"), ct);
    }
}
```

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

---

## IMessageHandler<TMessage>.cs

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
    public async Task<MessageProcessingResult> HandleAsync(
        OrderCreated message,
        MessageContext context,
        CancellationToken ct)
    {
        await ProcessOrderAsync(message, ct);
        return MessageProcessingResult.Success;
    }
}

services.AddMessageHandler<OrderCreated, OrderCreatedHandler>();
```

---

## MessageContext.cs

```csharp
public sealed class MessageContext
{
    public required ITransportEnvelope Envelope { get; init; }
    public IGridContext? GridContext { get; set; }
    public ITransportTransaction Transaction { get; init; }
    public Dictionary<string, object> Properties { get; init; }
    public int DeliveryCount { get; init; }
}
```

### Usage Example

```csharp
public async Task<MessageProcessingResult> HandleAsync(
    PaymentProcessing message,
    MessageContext context,
    CancellationToken ct)
{
    var gridContext = context.GridContext;
    _logger.LogInformation("Correlation: {CorrelationId}", gridContext?.CorrelationId);
    
    if (context.DeliveryCount > 3)
        _logger.LogWarning("Retry attempt {Attempt}", context.DeliveryCount);
    
    return MessageProcessingResult.Success;
}
```

---

## MessageProcessingResult.cs

```csharp
public enum MessageProcessingResult
{
    Success,      // Complete message, remove from queue
    Retry,        // Reprocess with backoff
    DeadLetter    // Move to DLQ, no retry
}
```

### Usage Example

```csharp
try
{
    await ProcessAsync(message, ct);
    return MessageProcessingResult.Success;
}
catch (TransientException)
{
    return MessageProcessingResult.Retry;
}
catch (ValidationException)
{
    return MessageProcessingResult.DeadLetter;
}
```

---

## IEndpointAddress.cs

```csharp
public interface IEndpointAddress
{
    string Name { get; }
    string Address { get; }
    IReadOnlyDictionary<string, string> Properties { get; }
}
```

### Usage Example

```csharp
var destination = new EndpointAddress("orders.created");
await publisher.PublishAsync(envelope, destination, ct);
```

---

## IMessageSerializer.cs

```csharp
public interface IMessageSerializer
{
    ReadOnlyMemory<byte> Serialize<T>(T message) where T : class;
    T Deserialize<T>(ReadOnlyMemory<byte> data) where T : class;
}
```

### Usage Example

```csharp
// Default JSON serializer (automatic)
services.AddHoneyDrunkTransportCore();

// Custom serializer
services.AddSingleton<IMessageSerializer, ProtobufSerializer>();
```

---

[← Back to File Guide](FILE_GUIDE.md)
