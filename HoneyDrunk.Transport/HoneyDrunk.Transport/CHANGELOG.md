# Changelog

All notable changes to HoneyDrunk.Transport will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.3.0] - 2025-12-03

### Breaking Changes
- **Kernel v0.3.0 Upgrade**: Transport now requires HoneyDrunk.Kernel.Abstractions v0.3.0
- **ITransportEnvelope Changes**: Added `TenantId` and `ProjectId` properties for multi-tenant/multi-project support
- **WithGridContext Signature**: Added `tenantId` and `projectId` parameters
- **GridHeaderNames**: Now uses Kernel's canonical header names (`X-Causation-Id`, `X-Correlation-Id`, etc.) instead of hardcoded strings

### Added
- **Multi-Tenancy Support**: Full `TenantId` and `ProjectId` propagation through all transport layers
  - Added `TenantId` and `ProjectId` properties to `ITransportEnvelope`
  - Updated `EnvelopeFactory.CreateEnvelopeWithGridContext()` to map new fields from `IGridContext`
  - Updated `GridContextFactory` to reconstruct `TenantId` and `ProjectId` from envelopes
  - Azure Service Bus: Maps `TenantId`/`ProjectId` using `GridHeaderNames.TenantId` and `GridHeaderNames.ProjectId`
  - Storage Queue: Includes `tenantId` and `projectId` in JSON envelope schema

- **GridHeaderNames Standardization**: All adapters now use Kernel's canonical header names
  - Replaced hardcoded `"CausationId"` with `GridHeaderNames.CausationId` (`"X-Causation-Id"`)
  - Replaced hardcoded `"CorrelationId"` with `GridHeaderNames.CorrelationId` (`"X-Correlation-Id"`)
  - Consistent header naming across Azure Service Bus, Storage Queue, and InMemory transports

- **Documentation**: Comprehensive guide rewrites for consistency across all transports
  - `docs/AzureServiceBus.md` - Aligned with Configuration.md, EndpointAddress patterns
  - `docs/Testing.md` - Rewritten with proper unit/integration patterns, no ServiceProvider mutation
  - All project READMEs updated for NuGet consistency

### Changed
- **Dependency Updates**: Now depends on `HoneyDrunk.Kernel.Abstractions` v0.3.0
- **GridContextFactory**: No longer duplicates `StudioId -> TenantId`; uses actual `TenantId` and `ProjectId` from envelopes
- **Azure Service Bus EnvelopeMapper**: Updated reserved properties list to include `TenantId`, `ProjectId`, and all Grid header names
- **Storage Queue Envelope Schema**: Breaking change - JSON schema now includes `tenantId` and `projectId` fields

### Fixed
- **Context Propagation Fidelity**: Grid context now preserves all fields through message round-trips
  - Fixed loss of `TenantId` and `ProjectId` in distributed messaging
  - Fixed header name inconsistencies between transport adapters
  - Ensured symmetric field mapping across all transport implementations

### Migration Guide

**Step 1: Update Package References**
```xml
<PackageReference Include="HoneyDrunk.Transport" Version="0.3.0" />
<PackageReference Include="HoneyDrunk.Transport.AzureServiceBus" Version="0.3.0" />
```

**Step 2: Storage Queue Migration (BREAKING)**
If you have existing messages in Azure Storage Queue, they will be missing `tenantId` and `projectId` fields.
Options:
- **Option A (Recommended)**: Drain existing queues before upgrading
- **Option B**: Handle null `TenantId`/`ProjectId` in handlers (fields are nullable)

**Step 3: Verify Grid Context Propagation**
```csharp
public async Task HandleAsync(MyMessage message, MessageContext context, CancellationToken ct)
{
    var gridContext = context.GridContext;
    
    // NEW: TenantId and ProjectId now available
    _logger.LogInformation(
        "Processing in Tenant {TenantId}, Project {ProjectId}",
        gridContext?.TenantId,
        gridContext?.ProjectId);
}
```

**Step 4: Test Header Access (If Applicable)**

If you directly access headers:

```csharp
// OLD (will break)
var causationId = message.ApplicationProperties["CausationId"];

// NEW (correct)
using HoneyDrunk.Kernel.Abstractions.Context;
var causationId = message.ApplicationProperties[GridHeaderNames.CausationId];
```

**No Code Changes Required If:**
- ✅ You only use `ITransportPublisher` (low-level API)
- ✅ You don't inspect Grid headers directly
- ✅ You drain Storage Queue before upgrading

### Architecture

The v0.3.0 release maintains strict layering:

```
Kernel (base primitives)
  ↓
Data (storage primitives, conventions)
  ↓
Transport (messaging, outbox, uses Data)
  ↓
Application Nodes (uses all)

✅ Data exposes storage primitives (connections, conventions)
✅ Transport depends on Data for persistence
✅ Data never knows Transport exists
✅ No circular dependencies
```

**Note**: Transport implements outbox patterns by using Data's storage primitives, not by exposing abstractions for Data to call.

## [0.2.0] - 2025-11-22

### Breaking Changes
- **Kernel Integration**: Transport now requires HoneyDrunk.Kernel to be registered via `AddHoneyDrunkCoreNode` before calling `AddHoneyDrunkTransportCore`
- **ITransportEnvelope Changes**: Added `NodeId`, `StudioId`, `Environment` properties for Grid-aware context propagation
- **Method Signature Changes**: 
  - `WithCorrelation()` replaced by `WithGridContext()`
  - `EnvelopeFactory` now requires `TimeProvider` instead of `IIdGenerator` and `IClock`
- **MessageContext Changes**: Added `IGridContext` property as first-class member

### Added
- **Grid Context Integration**: Full IGridContext support for distributed context propagation
  - Added `NodeId`, `StudioId`, `Environment` to `ITransportEnvelope`
  - Added `CreateEnvelopeWithGridContext()` method to `EnvelopeFactory`
  - Added `IGridContext` property to `MessageContext`
  - Added `GridContextPropagationMiddleware` for automatic context propagation

- **Health Contributors**: New health monitoring infrastructure
  - `ITransportHealthContributor` - Interface for health checks
  - `TransportHealthResult` - Health check result type
  - `PublisherHealthContributor` - Publisher health monitoring
  - `OutboxHealthContributor` - Outbox backlog monitoring

- **Metrics Infrastructure**: Metrics collection abstractions
  - `ITransportMetrics` - Metrics collection interface
  - `NoOpTransportMetrics` - Default no-op implementation
  - Ready for Kernel `IMetricsCollector` integration

- **Context Factory**: `IGridContextFactory` and `GridContextFactory` for bridging Transport envelopes to Kernel Grid context

### Changed
- **Dependency Updates**: Now depends on `HoneyDrunk.Kernel.Abstractions` v0.2.1
- **Time Abstraction**: Uses .NET's built-in `TimeProvider` instead of custom `IClock`
- **ID Generation**: Uses Kernel's `CorrelationId.NewId()` instead of custom `IIdGenerator`
- **Default Middleware**: `GridContextPropagationMiddleware` replaces `CorrelationMiddleware` as primary context propagation mechanism
- **Service Registration**: Removed `AddKernelDefaults()` call - Transport now layers on top of Kernel

### Deprecated
- `CorrelationMiddleware` - Use `GridContextPropagationMiddleware` instead (will be removed in v1.0)
- `WithCorrelation()` method on `ITransportEnvelope` - Use `WithGridContext()` instead

### Migration Guide
See [KERNEL_V2_INTEGRATION.md](KERNEL_V2_INTEGRATION.md) for complete migration guide.

**Quick Migration:**
```csharp
// Step 1: Register Kernel first
var nodeDescriptor = new NodeDescriptor { NodeId = "my-node", /* ... */ };
builder.Services.AddHoneyDrunkCoreNode(nodeDescriptor);

// Step 2: Then register Transport
builder.Services.AddHoneyDrunkTransportCore(options => { /* ... */ });

// Step 3: Access Grid context directly
public async Task HandleAsync(MyMessage message, MessageContext context, CancellationToken ct)
{
    var gridContext = context.GridContext; // NEW: Direct access
    _logger.LogInformation("Processing in Node {NodeId}", gridContext?.NodeId);
    // ...
}
```

## [0.1.1] - 2025-11-18

### Changed
- Updated NuGet package dependencies to latest versions
- Updated HoneyDrunk.Kernel to version 0.1.2
- Updated HoneyDrunk.Standards to version 0.2.5
- Updated Microsoft.CodeAnalyzers.NetAnalyzers to version 10.0.100

## [0.1.0] - 2025-11-01

### Added
- Initial release with core abstractions (ITransportPublisher, ITransportConsumer, ITransportEnvelope)
- Middleware pipeline architecture for message processing
- Retry and error handling strategies with configurable backoff
- Transactional outbox pattern support
- Dependency injection extensions for fluent configuration
- Support for distributed tracing and telemetry
- Message envelope pattern with correlation and causation tracking
