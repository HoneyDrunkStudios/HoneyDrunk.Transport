# Changelog

All notable changes to HoneyDrunk.Transport.StorageQueue will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.3.0] - 2025-12-03

### Breaking Changes
- **Kernel v0.3.0 Upgrade**: Requires `HoneyDrunk.Kernel.Abstractions` v0.3.0
- **TenantId/ProjectId**: JSON envelope schema now includes `tenantId` and `projectId` fields
- **GridHeaderNames**: Uses Kernel's canonical header names

### Added
- **Multi-Tenancy Support**: Full `TenantId` and `ProjectId` propagation in JSON envelope
- **Two-Level Concurrency**: Explicit documentation of `MaxConcurrency` x `BatchProcessingConcurrency` model

### Changed
- **Documentation**: Clarified 64KB limit applies after Base64 encoding, not raw payload
- **Concurrency Description**: Clarified "parallel fetch loops" terminology

### Fixed
- **Header Standardization**: All Grid headers now use `GridHeaderNames` constants

## [0.2.0] - 2025-11-22

### Breaking Changes
- **Kernel Integration**: Requires HoneyDrunk.Kernel to be registered via `AddHoneyDrunkCoreNode` before calling `AddHoneyDrunkTransportStorageQueue`
- **Grid Context**: Envelope now includes `NodeId`, `StudioId`, `Environment` fields for Grid-aware context propagation

### Added
- **Grid Context Support**: Automatic propagation of Kernel Grid context across messages
- **Health Contributors**: Storage Queue connectivity and backlog health monitoring

### Changed
- **Dependency Updates**: Now depends on `HoneyDrunk.Kernel.Abstractions` v0.2.1
- **Envelope Serialization**: Includes Grid context fields in JSON serialization

## [0.1.1] - 2025-11-18

### Changed
- Updated NuGet package dependencies to latest versions
- Updated Azure.Storage.Queues to version 12.24.0
- Updated HoneyDrunk.Standards to version 0.2.5
- Updated Microsoft.Extensions.Hosting.Abstractions to version 10.0.0
- Updated Microsoft.Extensions.Logging.Abstractions to version 10.0.0
- Updated Microsoft.Extensions.Options to version 10.0.0
- Updated Microsoft.Extensions.Options.DataAnnotations to version 10.0.0
- Updated Microsoft.CodeAnalysis.NetAnalyzers to version 10.0.100

## [0.1.0] - 2025-11-01

### Added
- Initial release with Azure Storage Queue transport implementation
- Cost-effective, high-volume messaging support
- Automatic poison queue handling
- Configurable retry policies with exponential backoff
- Batch processing support
- Thread-safe lifecycle management
