# HoneyDrunk.Transport - Repository Changelog

All notable changes to the HoneyDrunk.Transport repository will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

**Note:** See individual package CHANGELOGs for detailed changes:
- [HoneyDrunk.Transport CHANGELOG](HoneyDrunk.Transport/CHANGELOG.md)
- [HoneyDrunk.Transport.AzureServiceBus CHANGELOG](HoneyDrunk.Transport.AzureServiceBus/CHANGELOG.md)
- [HoneyDrunk.Transport.InMemory CHANGELOG](HoneyDrunk.Transport.InMemory/CHANGELOG.md)
- [HoneyDrunk.Transport.StorageQueue CHANGELOG](HoneyDrunk.Transport.StorageQueue/CHANGELOG.md)

---

## [Unreleased]

### Changed

- Aligned Transport test tooling with ADR-0047 by adopting HoneyDrunk.Standards.Tests 0.2.9 and refreshing HoneyDrunk.Standards to 0.2.9 across package projects.

### Added

- Backfilled focused unit coverage across core, Azure Service Bus, InMemory, and Storage Queue transport surfaces to seed the 70% repository coverage baseline.
- Added PR coverage gate baseline wiring with 75% patch coverage and 70% absolute coverage thresholds.

## [0.6.0] - 2026-05-18

### Changed

- Aligned all Transport packages with `HoneyDrunk.Kernel.Abstractions` v0.7.0.
- Removed runtime `HoneyDrunk.Kernel` package dependency from core Transport and InMemory packages.
- Switched inbound Grid context propagation to abstractions-only `GridContextSnapshot` creation.
- Consolidated Azure Service Bus standard and session consumer message-processing orchestration.
- Clarified Azure Service Bus and Storage Queue provider release notes for inherited core Transport dependency behavior.

### Fixed

- Added fail-fast validation for inbound envelopes missing required Grid identity fields instead of fabricating default producer identity.
- Covered null `MessageContext.ServiceProvider` middleware behavior with tests.

## [0.5.0] - 2026-05-04

### Changed

- Aligned solution package versions with Kernel v0.5.0.
- Updated `HoneyDrunk.Transport` Grid context initialization for Kernel's typed `TenantId` primitive.

## [0.4.0] - 2026-01-20

### Breaking Changes

- **GridContext Ownership**: Transport no longer creates its own GridContext; initializes Kernel's DI-scoped `IGridContext` via `IGridContextFactory.InitializeFromEnvelope()`
- **TransportGridContext Removed**: Use Kernel's `GridContext` instead
- **IGridContextFactory API Changed**: `CreateFromEnvelope()` replaced by `InitializeFromEnvelope()`
- **Kernel v0.4.0 Required**: Transport now requires full HoneyDrunk.Kernel v0.4.0
- **CorrelationMiddleware Removed**: Use `GridContextPropagationMiddleware` instead

### Added

- Kernel vNext invariant enforcement (`ReferenceEquals` DI GridContext and MessageContext.GridContext)
- `InMemoryTransportConsumer` creates DI scope per message via `IServiceScopeFactory`
- `MessageContext.ServiceProvider` property for scoped service provider access
- `EnvelopeValidationException` for fail-fast envelope validation
- `EnvelopeFactory` validates `GridContext.IsInitialized`, `CorrelationId`, and header/baggage size (48KB limit)

### Changed

- HoneyDrunk.Kernel: now references full package for `GridContext.Initialize()` access
- `GridContextPropagationMiddleware` resolves `IGridContext` from DI scope and initializes it
- `MessageHandlerInvoker` uses `MessageContext.ServiceProvider` for handler resolution

### Removed

- `TransportGridContext` class
- `IGridContextFactory.CreateFromEnvelope()` method
- `GridContextFactory` `TimeProvider` dependency
- `CorrelationMiddleware` (deprecated since v0.2.0)

## [0.3.0] - 2025-12-29

### Added

- Azure Storage Queue transport provider (`HoneyDrunk.Transport.StorageQueue`)
- Middleware pipeline architecture with onion-style execution
- Grid context propagation middleware
- Telemetry and logging middleware
- Transactional outbox pattern abstractions

### Changed

- Unified middleware registration via `ITransportBuilder`

## [0.2.0] - 2025-12-15

### Added

- Azure Service Bus transport provider (`HoneyDrunk.Transport.AzureServiceBus`)
- In-memory transport for testing (`HoneyDrunk.Transport.InMemory`)
- Health contributor abstractions
- Retry and backoff configuration

### Deprecated

- `CorrelationMiddleware` in favor of `GridContextPropagationMiddleware`

## [0.1.0] - 2025-11-01

### Added

- Initial release of HoneyDrunk.Transport
- Transport envelope pattern with `ITransportEnvelope`
- `ITransportPublisher` and `ITransportConsumer` abstractions
- `EnvelopeFactory` for Grid-aware envelope creation
- Fluent builder pattern for service registration
- Metrics collection abstractions

[0.5.0]: https://github.com/HoneyDrunkStudios/HoneyDrunk.Transport/releases/tag/v0.5.0
[0.4.0]: https://github.com/HoneyDrunkStudios/HoneyDrunk.Transport/releases/tag/v0.4.0
[0.3.0]: https://github.com/HoneyDrunkStudios/HoneyDrunk.Transport/releases/tag/v0.3.0
[0.2.0]: https://github.com/HoneyDrunkStudios/HoneyDrunk.Transport/releases/tag/v0.2.0
[0.1.0]: https://github.com/HoneyDrunkStudios/HoneyDrunk.Transport/releases/tag/v0.1.0
