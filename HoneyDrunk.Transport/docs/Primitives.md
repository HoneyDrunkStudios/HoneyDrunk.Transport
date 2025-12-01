# 🔧 Primitives - Building Blocks

[← Back to File Guide](FILE_GUIDE.md)

---

## Table of Contents

- [Overview](#overview)
- [TransportEnvelope.cs](#transportenvelopecs)
- [EnvelopeFactory.cs](#envelopefactorycs)
- [JsonMessageSerializer.cs](#jsonmessageserializercs)
- [EndpointAddress.cs](#endpointaddresscs)
- [NoOpTransportTransaction.cs](#nooptransporttransactioncs)

---

## Overview

Core implementation classes that provide the foundational building blocks for the transport system. These are the concrete types that implement the abstractions.

**Location:** `HoneyDrunk.Transport/Primitives/`

---

## TransportEnvelope.cs

```csharp
public sealed class TransportEnvelope : ITransportEnvelope
{
    public required string MessageId { get; init; }
    public string? CorrelationId { get; init; }
    public string? CausationId { get; init; }
    public string? NodeId { get; init; }
    public string? StudioId { get; init; }
    public string? Environment { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required string MessageType { get; init; }
    public required IReadOnlyDictionary<string, string> Headers { get; init; }
    public required ReadOnlyMemory<byte> Payload { get; init; }
}
```

### Usage Example

```csharp
// Create with headers
var envelope = new TransportEnvelope
{
    MessageId = "msg-123",
    MessageType = typeof(OrderCreated).FullName!,
    Timestamp = DateTimeOffset.UtcNow,
    Headers = new Dictionary<string, string> { ["Priority"] = "high" },
    Payload = payload
};

// Add more headers immutably
var enriched = envelope.WithHeaders(new Dictionary<string, string>
{
    ["TenantId"] = "tenant-123"
});
```

[↑ Back to top](#table-of-contents)

---

## EnvelopeFactory.cs

```csharp
public sealed class EnvelopeFactory(TimeProvider timeProvider)
{
    public ITransportEnvelope CreateEnvelope<TMessage>(
        ReadOnlyMemory<byte> payload,
        string? correlationId = null,
        string? causationId = null,
        IDictionary<string, string>? headers = null)
        where TMessage : class;
    
    public ITransportEnvelope CreateEnvelopeWithGridContext<TMessage>(
        ReadOnlyMemory<byte> payload,
        IGridContext gridContext,
        IDictionary<string, string>? headers = null)
        where TMessage : class;
}
```

### Usage Example

```csharp
public class OrderService(EnvelopeFactory factory, IGridContext gridContext)
{
    public ITransportEnvelope CreateOrderMessage(Order order)
    {
        var message = new OrderCreated(order.Id);
        var payload = _serializer.Serialize(message);
        
        // Factory auto-generates MessageId and Timestamp
        return factory.CreateEnvelopeWithGridContext<OrderCreated>(
            payload,
            gridContext);
    }
}
```

[↑ Back to top](#table-of-contents)

---

## JsonMessageSerializer.cs

```csharp
public sealed class JsonMessageSerializer : IMessageSerializer
{
    public ReadOnlyMemory<byte> Serialize<T>(T message) where T : class
    {
        return JsonSerializer.SerializeToUtf8Bytes(message, _options);
    }
    
    public T Deserialize<T>(ReadOnlyMemory<byte> data) where T : class
    {
        return JsonSerializer.Deserialize<T>(data.Span, _options)!;
    }
}
```

### Usage Example

```csharp
// Default (registered automatically)
services.AddHoneyDrunkTransportCore();  // Uses JsonMessageSerializer

// Custom JSON options
services.AddSingleton<IMessageSerializer>(
    new JsonMessageSerializer(new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    }));
```

[↑ Back to top](#table-of-contents)

---

## EndpointAddress.cs

```csharp
public sealed class EndpointAddress : IEndpointAddress
{
    public string Name { get; }
    public string Address { get; }
    public IReadOnlyDictionary<string, string> Properties { get; }
    
    public EndpointAddress(string address, IDictionary<string, string>? properties = null)
    {
        Address = address;
        Name = address;
        Properties = properties != null 
            ? new Dictionary<string, string>(properties) 
            : new Dictionary<string, string>();
    }
}
```

### Usage Example

```csharp
// Simple address
var destination = new EndpointAddress("orders.created");

// With routing properties
var destination = new EndpointAddress(
    "orders.created",
    new Dictionary<string, string>
    {
        ["PartitionKey"] = customerId.ToString(),
        ["SessionId"] = sessionId
    });
```

[↑ Back to top](#table-of-contents)

---

## NoOpTransportTransaction.cs

```csharp
public sealed class NoOpTransportTransaction : ITransportTransaction
{
    public Task CommitAsync(CancellationToken cancellationToken = default) 
        => Task.CompletedTask;
    
    public Task RollbackAsync(CancellationToken cancellationToken = default) 
        => Task.CompletedTask;
}
```

### Usage Example

```csharp
// Used automatically for non-transactional transports (InMemory, Storage Queue)
// No direct usage - accessed via MessageContext.Transaction
```

[↑ Back to top](#table-of-contents)

---

[← Back to File Guide](FILE_GUIDE.md) | [↑ Back to top](#table-of-contents)
