# Changelog

All notable changes to the HoneyDrunk.Transport repository are documented in this
file. The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

For detailed, per-package history (including breaking changes and migration notes),
see the package CHANGELOGs:

- [HoneyDrunk.Transport](HoneyDrunk.Transport/HoneyDrunk.Transport/CHANGELOG.md)
- [HoneyDrunk.Transport.AzureServiceBus](HoneyDrunk.Transport/HoneyDrunk.Transport.AzureServiceBus/CHANGELOG.md)
- [HoneyDrunk.Transport.InMemory](HoneyDrunk.Transport/HoneyDrunk.Transport.InMemory/CHANGELOG.md)
- [HoneyDrunk.Transport.StorageQueue](HoneyDrunk.Transport/HoneyDrunk.Transport.StorageQueue/CHANGELOG.md)

## Unreleased

## 0.7.1 - Sonar follow-up cleanup

### Changed

- Sonar follow-up cleanup (ADR-0011 D11). No public API changes; patch bump across all packages.

## 0.7.0 - Kernel adoption and Sonar cleanup

### Changed

- Removed the `EndpointAddress.Create(string, string)` 2-arg overload (Sonar S3427); the 7-arg overload remains.
- Bumped HoneyDrunk.Kernel dependencies and aligned the test stack.

## 0.6.0 - ServiceBus consumer consolidation

### Added

- Repository changelog and consolidated Azure Service Bus consumer.
- Typed tenant ids for Grid context (ADR-0026).

## 0.1.0 - Initial scaffold

### Added

- Transport-agnostic messaging abstraction (`ITransportPublisher`, `ITransportConsumer`, `ITransportEnvelope`).
- Azure Service Bus and in-memory transport implementations.
- Middleware pipeline, retry/backoff strategies, and transactional outbox contracts.
