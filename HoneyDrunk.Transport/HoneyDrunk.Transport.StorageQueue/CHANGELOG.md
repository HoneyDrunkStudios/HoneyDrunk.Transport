# Changelog

All notable changes to HoneyDrunk.Transport.StorageQueue will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
