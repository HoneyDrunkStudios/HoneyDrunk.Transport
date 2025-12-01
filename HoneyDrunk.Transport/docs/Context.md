# 🌐 Context - Grid Integration

[← Back to File Guide](FILE_GUIDE.md)

---

## Table of Contents

- [Overview](#overview)
- [IGridContextFactory.cs](#igridcontextfactorycs)
- [GridContextFactory.cs](#gridcontextfactorycs)
- [TransportGridContext.cs](#transportgridcontextcs)
- [Grid Context Propagation Flow](#grid-context-propagation-flow)

---

## Overview

Grid context integration for distributed context propagation across Node boundaries. Bridges Transport envelopes to Kernel's IGridContext.

**Location:** `HoneyDrunk.Transport/Context/`

---

## IGridContextFactory.cs

```csharp
public interface IGridContextFactory
{
    IGridContext CreateFromEnvelope(
        ITransportEnvelope envelope,
        CancellationToken cancellationToken);
}
```

### Usage Example

```csharp
// Used internally by GridContextPropagationMiddleware
// Not typically called directly in application code
```

---

## GridContextFactory.cs

```csharp
public sealed class GridContextFactory(TimeProvider timeProvider) 
    : IGridContextFactory
{
    public IGridContext CreateFromEnvelope(
        ITransportEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var correlationId = envelope.CorrelationId ?? envelope.MessageId;
        var causationId = envelope.CausationId ?? envelope.MessageId;
        
        var baggage = envelope.Headers != null
            ? new Dictionary<string, string>(envelope.Headers)
            : new Dictionary<string, string>();
        
        return new TransportGridContext(
            correlationId,
            causationId,
            envelope.NodeId ?? string.Empty,
            envelope.StudioId ?? string.Empty,
            envelope.Environment ?? string.Empty,
            baggage,
            _timeProvider.GetUtcNow(),
            cancellationToken);
    }
}
```

### Usage Example

```csharp
// Registered automatically
services.AddHoneyDrunkTransportCore();  // Registers GridContextFactory
```

---

## TransportGridContext.cs

Lightweight IGridContext implementation for Transport layer.

```csharp
internal sealed class TransportGridContext : IGridContext
{
    public string CorrelationId { get; }
    public string? CausationId { get; }
    public string NodeId { get; }
    public string StudioId { get; }
    public string Environment { get; }
    public IReadOnlyDictionary<string, string> Baggage { get; }
    public CancellationToken Cancellation { get; }
    public DateTimeOffset CreatedAtUtc { get; }
    
    public IDisposable BeginScope() => new NoOpScope();
    public IGridContext CreateChildContext(string? nodeId = null);
    public IGridContext WithBaggage(string key, string value);
}
```

### Usage Example

```csharp
// Created automatically from envelope
// Access via MessageContext.GridContext

public async Task<MessageProcessingResult> HandleAsync(
    OrderCreated message,
    MessageContext context,
    CancellationToken ct)
{
    var gridContext = context.GridContext;
    
    _logger.LogInformation(
        "Processing in Node {NodeId} with correlation {CorrelationId}",
        gridContext?.NodeId,
        gridContext?.CorrelationId);
    
    // Create child context for downstream operation
    var childContext = gridContext?.CreateChildContext("payment-node");
    await _paymentService.ProcessAsync(message, childContext, ct);
    
    return MessageProcessingResult.Success;
}
```

---

## Grid Context Propagation Flow

```
┌─────────────────────────────────────────────────────────────┐
│ Node A (Order Service)                                      │
│                                                              │
│  1. Inject IGridContext from Kernel                         │
│  2. Create envelope with Grid context via EnvelopeFactory   │
│     - NodeId: "order-service"                               │
│     - CorrelationId: "abc-123"                              │
│     - StudioId: "production"                                │
│  3. Publish to queue/topic                                  │
└─────────────────────────────────────────────────────────────┘
                            ↓
              ┌─────────────────────────┐
              │   Queue/Topic           │
              └─────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ Node B (Payment Service)                                    │
│                                                              │
│  1. Consumer receives envelope                              │
│  2. GridContextPropagationMiddleware                        │
│     - Extracts Grid fields from envelope                    │
│     - Creates IGridContext via GridContextFactory           │
│     - Populates MessageContext.GridContext                  │
│  3. Handler accesses context.GridContext                    │
│     - CorrelationId: "abc-123" (same)                       │
│     - CausationId: "abc-123" (from original)                │
│     - NodeId: "payment-service" (current node)              │
│     - StudioId: "production" (propagated)                   │
└─────────────────────────────────────────────────────────────┘
```

---

[← Back to File Guide](FILE_GUIDE.md) | [↑ Back to top](#table-of-contents)
