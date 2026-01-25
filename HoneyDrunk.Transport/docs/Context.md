# 🌐 Context - Grid Integration

[← Back to File Guide](FILE_GUIDE.md)

---

## Table of Contents

- [Overview](#overview)
- [IGridContextFactory.cs](#igridcontextfactorycs)
- [GridContextFactory.cs](#gridcontextfactorycs)
- [Grid Context Propagation Flow](#grid-context-propagation-flow)

---

## Overview

Grid context integration for distributed context propagation across Node boundaries. Bridges Transport envelopes to Kernel's `IGridContext`.

**Location:** `HoneyDrunk.Transport/Context/`

> **Key Concept (Kernel vNext v0.4.0+):** Transport does **not** create its own GridContext. It **initializes** an existing DI-scoped `IGridContext` owned by Kernel using envelope metadata. This ensures exactly one GridContext per DI scope, maintaining the Kernel vNext invariant.

### Layering

| Layer | Owns | Responsibility |
|-------|------|----------------|
| **Kernel** | `IGridContext`, `GridContext` class | DI-scoped context ownership, initialization API |
| **Transport** | `IGridContextFactory` | Initialize Kernel's context from envelope metadata |
| **Envelope** | Operation lineage | CorrelationId, CausationId, TenantId, ProjectId, Headers |

### Kernel vNext Invariant

```
✅ INVARIANT: ReferenceEquals(DI GridContext, MessageContext.GridContext) == true
```

Transport middleware resolves the **same** `IGridContext` instance that handlers receive from DI. No duplicate contexts are created.

---

## IGridContextFactory.cs

```csharp
public interface IGridContextFactory
{
    void InitializeFromEnvelope(
        IGridContext gridContext,
        ITransportEnvelope envelope,
        CancellationToken cancellationToken);
}
```

### Purpose

`IGridContextFactory` initializes an **existing** DI-scoped `IGridContext` with envelope metadata. Unlike the pre-v0.4.0 pattern that created new contexts, this factory mutates the Kernel-owned context in place.

### When It's Called

1. Consumer receives message from broker
2. Consumer creates a DI scope for the message
3. `GridContextPropagationMiddleware` resolves `IGridContext` from the scope
4. Middleware calls `IGridContextFactory.InitializeFromEnvelope(gridContext, envelope, ct)`
5. Handler accesses the **same** context through `MessageContext.GridContext` or DI

---

## GridContextFactory.cs

Default implementation that initializes Kernel's `GridContext` from transport envelopes.

```csharp
public sealed class GridContextFactory : IGridContextFactory
{
    public void InitializeFromEnvelope(
        IGridContext gridContext,
        ITransportEnvelope envelope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(gridContext);
        ArgumentNullException.ThrowIfNull(envelope);

        // gridContext must be Kernel's GridContext for Initialize() to work
        if (gridContext is not KernelGridContext kernelContext)
        {
            throw new InvalidOperationException(
                "Expected GridContext from Kernel DI scope");
        }

        var correlationId = envelope.CorrelationId ?? envelope.MessageId;
        var causationId = envelope.CausationId ?? envelope.MessageId;
        var baggage = envelope.Headers != null
            ? new Dictionary<string, string>(envelope.Headers)
            : new Dictionary<string, string>();

        // Initialize the DI-owned GridContext with envelope metadata
        kernelContext.Initialize(
            correlationId: correlationId,
            causationId: causationId,
            tenantId: envelope.TenantId,
            projectId: envelope.ProjectId,
            baggage: baggage,
            cancellation: cancellationToken);
    }
}
```

### Mapping Behavior

| Property | Source | Fallback |
|----------|--------|----------|
| `CorrelationId` | `envelope.CorrelationId` | `envelope.MessageId` |
| `CausationId` | `envelope.CausationId` | `envelope.MessageId` |
| `TenantId` | `envelope.TenantId` | `null` |
| `ProjectId` | `envelope.ProjectId` | `null` |
| `Baggage` | `envelope.Headers` | Empty dictionary |

> **Note:** `NodeId`, `StudioId`, and `Environment` come from Kernel's `GridContext` constructor (set at DI registration time), not from the envelope. This ensures the context reflects the **current node's** identity.

### Registration

```csharp
// Transport registers the factory
services.AddHoneyDrunkTransportCore();  // Registers GridContextFactory

// Kernel registers the scoped GridContext (application responsibility)
services.AddScoped<IGridContext>(_ => 
    new GridContext(nodeId, studioId, environment));
```

---

## Grid Context Propagation Flow

```
┌─────────────────────────────────────────────────────────────┐
│ Node A (Order Service)                                      │
│                                                              │
│  1. Kernel creates DI-scoped GridContext                    │
│  2. Application initializes GridContext                     │
│  3. EnvelopeFactory copies context to envelope:             │
│     - Envelope.CorrelationId: "abc-123"                     │
│     - Envelope.TenantId: "tenant-xyz"                       │
│     - Envelope.ProjectId: "proj-001"                        │
│     - Envelope.Headers: baggage                             │
│  4. Publish to queue/topic                                  │
└─────────────────────────────────────────────────────────────┘
                            ↓
              ┌─────────────────────────┐
              │   Queue/Topic           │
              └─────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ Node B (Payment Service) - Kernel vNext Pattern            │
│                                                              │
│  1. Consumer receives envelope                              │
│  2. Consumer creates DI scope via IServiceScopeFactory      │
│  3. DI scope contains uninitialized GridContext (Kernel)    │
│  4. GridContextPropagationMiddleware:                       │
│     a. Resolves IGridContext from scope                     │
│     b. Calls factory.InitializeFromEnvelope(ctx, env, ct)   │
│     c. Sets MessageContext.GridContext = ctx (same ref)     │
│  5. Handler receives SAME context via:                      │
│     - MessageContext.GridContext                            │
│     - DI injection of IGridContext                          │
│                                                              │
│  ✅ ReferenceEquals(DI ctx, MessageContext.GridContext)     │
└─────────────────────────────────────────────────────────────┘
```

### Key Differences from Pre-v0.4.0

| Aspect | Pre-v0.4.0 | v0.4.0+ (Kernel vNext) |
|--------|-----------|------------------------|
| Context Creation | `GridContextFactory.CreateFromEnvelope()` creates new `TransportGridContext` | `GridContextFactory.InitializeFromEnvelope()` initializes existing Kernel `GridContext` |
| Context Type | `TransportGridContext` (Transport-owned) | `GridContext` (Kernel-owned) |
| DI Integration | Handler gets different instance than DI | Handler and DI share exact same instance |
| Scope Ownership | Transport creates projected context | Kernel owns scoped context, Transport initializes it |

---

[← Back to File Guide](FILE_GUIDE.md) | [↑ Back to top](#table-of-contents)
