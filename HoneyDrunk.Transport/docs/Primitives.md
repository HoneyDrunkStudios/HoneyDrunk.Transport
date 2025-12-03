# 🔧 Primitives - Building Blocks

[← Back to File Guide](FILE_GUIDE.md)

---

## Table of Contents

- [Overview](#overview)
- [TransportEnvelope.cs](#transportenvelopecs)
- [EnvelopeFactory.cs](#envelopefactorycs)

---

## Overview

Concrete implementations that back the core transport abstractions. This folder contains the envelope and factory types that wrap messages with Grid context metadata for transport across service boundaries.

**Location:** `HoneyDrunk.Transport/Primitives/`

**What Lives Here:**
- **TransportEnvelope** - Immutable envelope implementation with Grid context support
- **EnvelopeFactory** - Factory for creating envelopes with proper ID generation and timestamps

**What Primitives Do NOT Contain:**
- Serialization (lives in `DependencyInjection/` as `JsonMessageSerializer`)
- Endpoint addressing (lives in `Abstractions/` as `EndpointAddress`)
- Transactions (lives in `Abstractions/` as `NoOpTransportTransaction`)

**Why These Live Elsewhere:**
- `EndpointAddress` lives in Abstractions because it is a transport-agnostic value object that implements the address contract - it's part of the shared contract surface, not an internal implementation detail.
- `JsonMessageSerializer` lives in DependencyInjection because it's a default registration, not a core primitive.
- `NoOpTransportTransaction` lives in Abstractions as part of the transaction contract surface.

---

## TransportEnvelope.cs

**Immutable envelope implementation aligned with Kernel IGridContext**

```csharp
public sealed class TransportEnvelope : ITransportEnvelope
{
    public string MessageId { get; init; } = string.Empty;
    public string? CorrelationId { get; init; }
    public string? CausationId { get; init; }
    public string? NodeId { get; init; }
    public string? StudioId { get; init; }
    public string? TenantId { get; init; }
    public string? ProjectId { get; init; }
    public string? Environment { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public string MessageType { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, string> Headers { get; init; }
    public ReadOnlyMemory<byte> Payload { get; init; }

    public ITransportEnvelope WithHeaders(IDictionary<string, string> additionalHeaders);
    public ITransportEnvelope WithGridContext(
        string? correlationId = null,
        string? causationId = null,
        string? nodeId = null,
        string? studioId = null,
        string? tenantId = null,
        string? projectId = null,
        string? environment = null);
}
```

### Key Features

- **Immutable** - All properties are init-only, mutations return new instances
- **Grid context aligned** - Properties match `IGridContext` for seamless propagation
- **Multi-tenant support** - `TenantId` and `ProjectId` for multi-tenant scenarios
- **WithHeaders()** - Immutably add headers, returning a new envelope
- **WithGridContext()** - Immutably update Grid context properties

The `WithHeaders` and `WithGridContext` methods are implemented within `TransportEnvelope` to ensure immutability by returning cloned instances with updated metadata.

### Usage Example

```csharp
// Create envelope directly (prefer EnvelopeFactory for most cases)
var envelope = new TransportEnvelope
{
    MessageId = "msg-123",
    MessageType = typeof(OrderCreated).FullName!,
    CorrelationId = "corr-456",
    NodeId = "order-service",
    TenantId = "tenant-abc",
    Timestamp = DateTimeOffset.UtcNow,
    Headers = new Dictionary<string, string>(),
    Payload = payload
};

// Add headers immutably (returns new envelope)
var enriched = envelope.WithHeaders(new Dictionary<string, string>
{
    ["Priority"] = "high",
    ["Source"] = "api"
});

// Update Grid context immutably (returns new envelope)
var updated = envelope.WithGridContext(
    correlationId: "new-corr-789",
    tenantId: "different-tenant");
```

[↑ Back to top](#table-of-contents)

---

## EnvelopeFactory.cs

**Factory for creating transport envelopes with proper ID generation and timestamps**

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
    
    public ITransportEnvelope CreateEnvelopeWithId(
        string messageId,
        string messageType,
        ReadOnlyMemory<byte> payload,
        string? correlationId = null,
        string? causationId = null,
        IDictionary<string, string>? headers = null);
    
    public ITransportEnvelope CreateReply(
        ITransportEnvelope original,
        string replyMessageType,
        ReadOnlyMemory<byte> payload,
        IDictionary<string, string>? additionalHeaders = null);
}
```

### Key Features

- **Deterministic ID generation** - Uses Kernel's ULID-based ID generator for MessageId creation
- **TimeProvider integration** - Testable timestamps via injected `TimeProvider`
- **Grid context propagation** - Automatically propagates all Grid context properties including baggage
- **Reply support** - Creates properly chained reply envelopes with causation tracking

### Factory Methods

| Method | Purpose |
|--------|---------|
| `CreateEnvelope<T>` | Basic envelope with auto-generated ID and timestamp |
| `CreateEnvelopeWithGridContext<T>` | Envelope with full Grid context propagation (including baggage) |
| `CreateEnvelopeWithId` | Envelope with explicit message ID (for replay/idempotency scenarios) |
| `CreateReply` | Reply envelope derived from original (causation chain preserved) |

### Usage Example

```csharp
public class OrderService(
    EnvelopeFactory factory,
    IMessageSerializer serializer,
    ITransportPublisher publisher,
    IGridContext gridContext)
{
    public async Task PublishOrderCreatedAsync(Order order, CancellationToken ct)
    {
        var message = new OrderCreated(order.Id, order.CustomerId);
        var payload = serializer.Serialize(message);
        
        // Factory auto-generates MessageId (ULID) and Timestamp
        // Grid context properties (CorrelationId, NodeId, TenantId, etc.) are propagated
        var envelope = factory.CreateEnvelopeWithGridContext<OrderCreated>(
            payload,
            gridContext,
            headers: new Dictionary<string, string>
            {
                ["Priority"] = "normal"
            });
        
        var destination = EndpointAddress.Create("orders", "orders.created");
        await publisher.PublishAsync(envelope, destination, options: null, ct);
    }
    
    public async Task PublishReplyAsync(
        ITransportEnvelope originalEnvelope,
        OrderConfirmed confirmation,
        CancellationToken ct)
    {
        var payload = serializer.Serialize(confirmation);
        
        // CreateReply preserves correlation chain:
        // - CorrelationId = original.CorrelationId ?? original.MessageId
        // - CausationId = original.MessageId
        // - Inherits NodeId, TenantId, ProjectId, Environment
        var replyEnvelope = factory.CreateReply(
            originalEnvelope,
            typeof(OrderConfirmed).FullName!,
            payload);
        
        var destination = EndpointAddress.Create("orders", "orders.confirmed");
        await publisher.PublishAsync(replyEnvelope, destination, options: null, ct);
    }
}
```

### Grid Context Propagation

When using `CreateEnvelopeWithGridContext`, the factory propagates:

| From IGridContext | To TransportEnvelope |
|-------------------|---------------------|
| `CorrelationId` | `CorrelationId` |
| `CausationId` | `CausationId` |
| `NodeId` | `NodeId` |
| `StudioId` | `StudioId` |
| `TenantId` | `TenantId` |
| `ProjectId` | `ProjectId` |
| `Environment` | `Environment` |
| `Baggage` | Merged into `Headers` |

[↑ Back to top](#table-of-contents)

---

[← Back to File Guide](FILE_GUIDE.md) | [↑ Back to top](#table-of-contents)
