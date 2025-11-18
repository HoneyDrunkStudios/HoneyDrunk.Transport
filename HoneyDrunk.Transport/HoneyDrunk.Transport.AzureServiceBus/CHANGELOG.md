# Changelog

All notable changes to HoneyDrunk.Transport.AzureServiceBus will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.2] - 2025-11-18

### Changed
- Updated NuGet package dependencies to latest versions
- Updated Azure.Identity to version 1.17.0
- Updated Azure.Messaging.ServiceBus to version 7.20.1
- Updated Azure.Storage.Blobs to version 12.26.0
- Updated HoneyDrunk.Standards to version 0.2.5
- Updated Microsoft.Extensions.Azure to version 1.13.0
- Updated Microsoft.Extensions.Logging.Abstractions to version 10.0.0
- Updated Microsoft.Extensions.Options to version 10.0.0
- Updated Microsoft.CodeAnalysis.NetAnalyzers to version 10.0.100

## [0.1.1] - 2025-11-15

### Added
- Optional blob storage support for failed messages in Azure Service Bus

## [0.1.0] - 2025-11-01

### Added
- Initial release with Azure Service Bus transport implementation
- Topic and subscription support
- Session handling for ordered message processing
- Distributed tracing integration
- Dead-letter queue support
- Thread-safe publisher and consumer implementations
