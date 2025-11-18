# Changelog

All notable changes to HoneyDrunk.Transport will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.1] - 2025-01-18

### Changed
- Updated NuGet package dependencies to latest versions
- Updated HoneyDrunk.Kernel to version 0.1.2
- Updated HoneyDrunk.Standards to version 0.2.5
- Updated Microsoft.CodeAnalysis.NetAnalyzers to version 10.0.100

## [0.1.0] - 2025-01-01

### Added
- Initial release with core abstractions (ITransportPublisher, ITransportConsumer, ITransportEnvelope)
- Middleware pipeline architecture for message processing
- Retry and error handling strategies with configurable backoff
- Transactional outbox pattern support
- Dependency injection extensions for fluent configuration
- Support for distributed tracing and telemetry
- Message envelope pattern with correlation and causation tracking
