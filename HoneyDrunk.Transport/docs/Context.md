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

Grid context integration for distributed context propagation across Node boundaries. Bridges Transport envelopes to Kernel's `IGridContext`.

**Location:** `HoneyDrunk.Transport/Context/`

> **Key Concept:** Transport does not create a new concept of Grid context. It maps message envelopes into Kernel's existing `IGridContext` so handlers always run inside a coherent Grid scope.

### Layering

| Layer | Owns | Source |
|-------|------|--------|
| **Kernel** | `IGridContext` interface, node identity abstractions | — |
| **Transport** | `IGridContextFactory`, `TransportGridContext` | Envelope metadata |
| **Envelope** | Operation lineage (CorrelationId, CausationId, TenantId, ProjectId) | Publishing node |

---

## IGridContextFactory.cs

> **Note:** This interface is defined in `HoneyDrunk.Transport.Context`, but conceptually it is the transport-specific bridge into Kernel's `IGridContext`. Application code almost never calls this directly—it is infrastructure plumbing between Kernel and Transport.

```csharp
public interface IGridContextFactory
{
    IGridContext CreateFromEnvelope(
        ITransportEnvelope envelope,
        CancellationToken cancellationToken);
}
```

### Purpose

`IGridContextFactory` is the bridge from a transport envelope into Kernel's `IGridContext`. Transport calls this factory from `GridContextPropagationMiddleware` so handlers always see a fully-hydrated Grid context.

### When It's Called

1. Consumer receives message from broker
2. `GridContextPropagationMiddleware` invokes `IGridContextFactory.CreateFromEnvelope()`
3. Resulting `IGridContext` is set on `MessageContext.GridContext`
4. Handler accesses context through `MessageContext`

---

## GridContextFactory.cs

Default implementation that extracts Grid metadata from transport envelopes.

```csharp
public sealed class GridContextFactory : IGridContextFactory
{
    private readonly TimeProvider _timeProvider;

    public GridContextFactory(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public IGridContext CreateFromEnvelope(
        ITransportEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var correlationId = envelope.CorrelationId ?? envelope.MessageId;
        var causationId = envelope.CausationId ?? envelope.MessageId;

        var baggage = envelope.Headers != null
            ? new Dictionary<string, string>(envelope.Headers)
            : [];

        return new TransportGridContext(
            correlationId: correlationId,
            causationId: causationId,
            nodeId: envelope.NodeId ?? string.Empty,
            studioId: envelope.StudioId ?? string.Empty,
            tenantId: envelope.TenantId,
            projectId: envelope.ProjectId,
            environment: envelope.Environment ?? string.Empty,
            baggage: baggage,
            createdAtUtc: _timeProvider.GetUtcNow(),
            cancellation: cancellationToken);
    }
}
```

### Mapping Behavior

| Property | Source | Fallback |
|----------|--------|----------|
| `CorrelationId` | `envelope.CorrelationId` | `envelope.MessageId` |
| `CausationId` | `envelope.CausationId` | `envelope.MessageId` |
| `NodeId` | `envelope.NodeId` | `string.Empty` |
| `StudioId` | `envelope.StudioId` | `string.Empty` |
| `TenantId` | `envelope.TenantId` | `null` |
| `ProjectId` | `envelope.ProjectId` | `null` |
| `Environment` | `envelope.Environment` | `string.Empty` |
| `Baggage` | `envelope.Headers` | Empty dictionary |

> **Design Note:** Currently, `NodeId` is taken from the envelope (the **source node** that published the message). If your application needs to distinguish between source node and current node, consider:
> - Adding an `INodeIdentity` abstraction in Kernel that provides the current node's identity
> - Storing `envelope.NodeId` as `SourceNodeId` in baggage
> - Using `INodeIdentity.NodeId` for `GridContext.NodeId`
>
> This is a v2 consideration for cleaner layering.

### Registration

```csharp
// Registered automatically
services.AddHoneyDrunkTransportCore();  // Registers GridContextFactory
```

---

## TransportGridContext.cs

Lightweight `IGridContext` implementation for the Transport consumer side.

```csharp
internal sealed class TransportGridContext : IGridContext
{
    public string CorrelationId { get; }
    public string? CausationId { get; }
    public string NodeId { get; }
    public string StudioId { get; }
    public string? TenantId { get; }
    public string? ProjectId { get; }
    public string Environment { get; }
    public IReadOnlyDictionary<string, string> Baggage { get; }
    public CancellationToken Cancellation { get; }
    public DateTimeOffset CreatedAtUtc { get; }

    public IDisposable BeginScope() => new NoOpScope();
    public IGridContext CreateChildContext(string? nodeId = null);
    public IGridContext WithBaggage(string key, string value);
}
```

### Role in the Grid

`TransportGridContext` is a lightweight `IGridContext` implementation used only on the **consumer side** of messaging. Kernel still owns the primary Grid context types; Transport creates a projected context for handlers based on:

- **Envelope metadata**: CorrelationId, CausationId, TenantId, ProjectId, NodeId (source), StudioId, Environment
- **Envelope headers**: Copied into Baggage

> **Note:** This is not "the global Grid context implementation"—it is a projection that lives at the messaging edge. Applications that have Kernel available should prefer Kernel's full `GridContext` implementation for non-messaging scenarios.

### Methods

| Method | Description |
|--------|-------------|
| `BeginScope()` | Returns a no-op scope (lightweight implementation) |
| `CreateChildContext(nodeId?)` | Creates child context with current CorrelationId as CausationId |
| `WithBaggage(key, value)` | Returns new context with additional baggage entry |

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
        "Processing order from Node {SourceNodeId} for Tenant {TenantId}, Project {ProjectId}",
        gridContext?.NodeId,      // Source node that published the message
        gridContext?.TenantId,
        gridContext?.ProjectId);
    
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
│     - GridContext.NodeId: "order-service"                   │
│     - Envelope.NodeId: "order-service" (source node)        │
│     - Envelope.CorrelationId: "abc-123"                     │
│     - Envelope.TenantId: "tenant-xyz"                       │
│     - Envelope.ProjectId: "proj-001"                        │
│     - Envelope.StudioId: "production"                       │
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
│     - Reads CorrelationId / CausationId from envelope       │
│     - Reads TenantId / ProjectId from envelope              │
│     - Reads NodeId / StudioId / Environment from envelope   │
│     - Creates IGridContext via GridContextFactory           │
│     - Populates MessageContext.GridContext                  │
│  3. Handler accesses context.GridContext                    │
│     - CorrelationId: "abc-123" (propagated)                 │
│     - CausationId: "abc-123" (from original)                │
│     - NodeId: "order-service" (source node from envelope)   │
│     - TenantId: "tenant-xyz" (propagated)                   │
│     - ProjectId: "proj-001" (propagated)                    │
│     - StudioId: "production" (propagated)                   │
└─────────────────────────────────────────────────────────────┘
```

### v2 Consideration: Current Node Identity

In the current implementation, `GridContext.NodeId` represents the **source node** that published the message. For scenarios where you need the **current node's** identity (Node B = "payment-service"), consider:

1. **Kernel `INodeIdentity`**: Have Kernel provide current node identity
2. **GridContextFactory v2**: Inject `INodeIdentity` and use it for `NodeId`
3. **Source node in baggage**: Store `envelope.NodeId` as `SourceNodeId` in baggage

This is a breaking change reserved for v2 to maintain cleaner layering between "who am I" (Kernel) and "where did this come from" (envelope).

---

[← Back to File Guide](FILE_GUIDE.md) | [↑ Back to top](#table-of-contents)
