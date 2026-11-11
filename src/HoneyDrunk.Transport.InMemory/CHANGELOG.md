# Changelog

All notable changes to HoneyDrunk.Transport.InMemory will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.8.0] - 2026-11-11

### Changed

- Require .NET 11 or newer. Consumers on .NET 10 must remain on 0.7.x until they upgrade.
- Align with HoneyDrunk.Transport 0.8.0, including safe runtime disposal during overlapping host shutdown. Public APIs and message serialization remain unchanged.
- See the [repository release notes](../../CHANGELOG.md) for compatibility and regression-test details.

## [0.7.3] - 2026-11-05

### Changed

- Refresh NuGet dependencies and adopt upstream package releases in dependency order. Preserve target frameworks and public contracts.

| Dependency | Previous | Updated |
| --- | --- | --- |
| Azure.Core | 1.63.0 | 1.64.0 |
| Azure.Storage.Blobs | 12.29.2 | 12.30.1 |
| Azure.Storage.Queues | 12.27.1 | 12.28.1 |
| HoneyDrunk.Kernel | 0.8.1 | 0.8.2 |
| HoneyDrunk.Kernel.Abstractions | 0.8.1 | 0.8.2 |
| HoneyDrunk.Standards | 0.3.0 | 0.4.3 |
| HoneyDrunk.Standards.Tests | 0.3.0 | 0.4.3 |
| Microsoft.CodeAnalysis.NetAnalyzers | 10.0.401 | 10.0.403 |
| Microsoft.Extensions.Hosting | 10.0.12 | 10.0.14 |
| Microsoft.Extensions.Hosting.Abstractions | 10.0.12 | 10.0.14 |
| Microsoft.Extensions.Logging.Abstractions | 10.0.12 | 10.0.14 |
| Microsoft.Extensions.Logging.Console | 10.0.12 | 10.0.14 |
| Microsoft.Extensions.Options | 10.0.12 | 10.0.14 |
| Microsoft.Extensions.Options.DataAnnotations | 10.0.12 | 10.0.14 |

## [0.7.2] - 2026-09-26

### Changed

- Refresh dependency and shared build-tooling versions; preserve target frameworks and existing public contracts. See the [repository dependency changes](../../CHANGELOG.md).

## [Unreleased]

## [0.7.1] - 2026-05-27

### Internal

- Version alignment with the HoneyDrunk.Transport 0.7.1 Sonar follow-up release. No public API or behavior changes in this package.

## [0.7.0] - 2026-05-26

### Changed

- Sonar gate-cleanup (ADR-0011 D11). `InMemoryBroker.GetOrCreateQueue` uses the lambda parameter instead of capturing `address`.
- `HoneyDrunk.Kernel.Abstractions` `0.7.0` → `0.8.0`.
- `Microsoft.Extensions.Logging.Abstractions` `10.0.5` → `10.0.8`.

## [0.6.0] - 2026-05-18

### Changed

- **Kernel Abstractions v0.7.0 Alignment**: Inherits the core Transport runtime dependency cleanup and `GridContextSnapshot` propagation model.

## [0.4.0] - 2026-01-20

### Breaking Changes
- **Kernel v0.4.0 Upgrade**: Requires `HoneyDrunk.Kernel.Abstractions` v0.4.0
- **IGridContext Changes**: `WithBaggage()` replaced by `AddBaggage()` (inherited from core Transport)

### Changed
- **Dependency Updates**:
  - HoneyDrunk.Kernel.Abstractions: 0.3.0 → 0.4.0
  - HoneyDrunk.Kernel: 0.3.0 → 0.4.0
  - Microsoft.Extensions.Logging.Abstractions: 10.0.0 → 10.0.2
  - Microsoft.CodeAnalysis.NetAnalyzers: 10.0.100 → 10.0.102

### Added
- **Fail-Fast Validation**: Inherits envelope validation from HoneyDrunk.Transport v0.4.0

## [0.3.1] - 2026-01-18

### Fixed
- **ITransportMetrics DI Registration**: Inherits fix from HoneyDrunk.Transport v0.3.1 - `ITransportMetrics` is now automatically registered during transport setup.

## [0.3.0] - 2025-12-03

### Breaking Changes
- **Kernel v0.3.0 Upgrade**: Requires `HoneyDrunk.Kernel.Abstractions` v0.3.0
- **TenantId/ProjectId**: Envelope now includes `TenantId` and `ProjectId` for multi-tenant support

### Added
- **Multi-Tenancy Support**: Full `TenantId` and `ProjectId` propagation
- **TransportRuntimeHost Integration**: InMemoryTransportConsumer works with runtime host as IHostedService

### Changed
- **Documentation**: Clarified InMemory runs real pipeline (not a mock), added limitations section
- **Test Patterns**: Examples use `TaskCompletionSource` instead of `Task.Delay`

### Fixed
- **Terminology**: Changed "Pub/Sub" to "Multi-Observer" for honest semantics

## [0.2.0] - 2025-11-22

### Breaking Changes
- **Kernel Integration**: Requires HoneyDrunk.Kernel to be registered via `AddHoneyDrunkCoreNode` before calling `AddHoneyDrunkInMemoryTransport`
- **Grid Context**: Envelope now includes `NodeId`, `StudioId`, `Environment` fields for Grid-aware context propagation

### Added
- **Grid Context Support**: Automatic propagation of Kernel Grid context across messages in test scenarios
- **Full Pipeline Testing**: Test Grid context flow end-to-end

### Changed
- **Dependency Updates**: Now depends on `HoneyDrunk.Kernel.Abstractions` v0.2.1
- **Test Patterns**: Updated examples to use Grid context

## [0.1.1] - 2025-11-18

### Changed
- Updated NuGet package dependencies to latest versions
- Updated HoneyDrunk.Standards to version 0.2.5
- Updated Microsoft.Extensions.Logging.Abstractions to version 10.0.0
- Updated Microsoft.CodeAnalysis.NetAnalyzers to version 10.0.100

## [0.1.0] - 2025-11-01

### Added
- Initial release with InMemoryBroker implementation
- Channel-based observable queues for testing
- Pub/sub subscription support
- In-process message routing for integration tests
- No external dependencies required
