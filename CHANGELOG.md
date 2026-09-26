# Changelog

## [0.7.2] - 2026-09-26

### Changed

- Refresh stable NuGet dependencies; preserve target frameworks and HoneyDrunk public contracts.

| Dependency | Previous | Updated |
| --- | --- | --- |
| Azure.Core | 1.53.0 | 1.63.0 |
| Azure.Messaging.ServiceBus | 7.20.1 | 7.21.0 |
| Azure.Storage.Blobs | 12.28.0 | 12.29.2 |
| Azure.Storage.Queues | 12.26.0 | 12.27.1 |
| Microsoft.CodeAnalysis.NetAnalyzers | 10.0.201 | 10.0.401 |
| Microsoft.Extensions.Azure | 1.14.0 | 1.14.1 |
| Microsoft.Extensions.Hosting | 10.0.8 | 10.0.12 |
| Microsoft.Extensions.Hosting.Abstractions | 10.0.8 | 10.0.12 |
| Microsoft.Extensions.Logging.Abstractions | 10.0.8 | 10.0.12 |
| Microsoft.Extensions.Logging.Console | 10.0.8 | 10.0.12 |
| Microsoft.Extensions.Options | 10.0.8 | 10.0.12 |
| Microsoft.Extensions.Options.DataAnnotations | 10.0.8 | 10.0.12 |


All notable changes to the HoneyDrunk.Transport repository are documented in this
file. The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

For detailed, per-package history (including breaking changes and migration notes),
see the package CHANGELOGs:

- [HoneyDrunk.Transport](HoneyDrunk.Transport/HoneyDrunk.Transport/CHANGELOG.md)
- [HoneyDrunk.Transport.AzureServiceBus](HoneyDrunk.Transport/HoneyDrunk.Transport.AzureServiceBus/CHANGELOG.md)
- [HoneyDrunk.Transport.InMemory](HoneyDrunk.Transport/HoneyDrunk.Transport.InMemory/CHANGELOG.md)
- [HoneyDrunk.Transport.StorageQueue](HoneyDrunk.Transport/HoneyDrunk.Transport.StorageQueue/CHANGELOG.md)


### Verified HoneyDrunk dependencies

- HoneyDrunk.Kernel: 0.8.0 -> 0.8.1 (verified on NuGet.org).
- HoneyDrunk.Kernel.Abstractions: 0.8.0 -> 0.8.1 (verified on NuGet.org).
- HoneyDrunk.Standards: 0.2.9 -> 0.3.0 (verified on NuGet.org).
- HoneyDrunk.Standards.Tests: 0.2.9 -> 0.3.0 (verified on NuGet.org).

## [Unreleased]

## [0.7.1] - 2026-05-27

### Changed

- Sonar follow-up cleanup (ADR-0011 D11). No public API changes; patch bump across all packages.

## [0.7.0] - 2026-05-26

### Changed

- Removed the `EndpointAddress.Create(string, string)` 2-arg overload (Sonar S3427); the 7-arg overload remains.
- Bumped HoneyDrunk.Kernel dependencies and aligned the test stack.

## [0.6.0] - 2026-05-18

### Added

- Repository changelog and consolidated Azure Service Bus consumer.
- Typed tenant ids for Grid context (ADR-0026).

## [0.1.0] - 2025-11-01

### Added

- Transport-agnostic messaging abstraction (`ITransportPublisher`, `ITransportConsumer`, `ITransportEnvelope`).
- Azure Service Bus and in-memory transport implementations.
- Middleware pipeline, retry/backoff strategies, and transactional outbox contracts.
