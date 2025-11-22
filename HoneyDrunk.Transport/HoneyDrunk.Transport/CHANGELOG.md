# Changelog

All notable changes to HoneyDrunk.Transport will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.2.0] - 2025-11-22

### ⚠️ BREAKING CHANGES
- **Kernel Integration**: Transport now requires HoneyDrunk.Kernel to be registered via `AddHoneyDrunkCoreNode` before calling `AddHoneyDrunkTransportCore`
- **ITransportEnvelope Changes**: Added `NodeId`, `StudioId`, `Environment` properties for Grid-aware context propagation
- **Method Signature Changes**: 
  - `WithCorrelation()` replaced by `WithGridContext()`
  - `EnvelopeFactory` now requires `TimeProvider` instead of `IIdGenerator` and `IClock`
- **MessageContext Changes**: Added `IGridContext` property as first-class member

### Added
- ✅ **Grid Context Integration**: Full IGridContext support for distributed context propagation
  - Added `NodeId`, `StudioId`, `Environment` to `ITransportEnvelope`
  - Added `CreateEnvelopeWithGridContext()` method to `EnvelopeFactory`
  - Added `IGridContext` property to `MessageContext`
  - Added `GridContextPropagationMiddleware` for automatic context propagation

- ✅ **Health Contributors**: New health monitoring infrastructure
  - `ITransportHealthContributor` - Interface for health checks
  - `TransportHealthResult` - Health check result type
  - `PublisherHealthContributor` - Publisher health monitoring
  - `OutboxHealthContributor` - Outbox backlog monitoring

- ✅ **Metrics Infrastructure**: Metrics collection abstractions
  - `ITransportMetrics` - Metrics collection interface
  - `NoOpTransportMetrics` - Default no-op implementation
  - Ready for Kernel `IMetricsCollector` integration

- ✅ **Context Factory**: `IGridContextFactory` and `GridContextFactory` for bridging Transport envelopes to Kernel Grid context

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
