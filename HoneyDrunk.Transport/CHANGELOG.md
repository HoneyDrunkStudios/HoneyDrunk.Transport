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

## [0.7.0] - 2026-05-26

### Changed (breaking)

- **`EndpointAddress.Create(string name, string address)` 2-arg overload removed** (Sonar S3427 blocker — overlapped with the 7-arg overload that defaults the optional metadata). **Migration:** direct positional invocations like `EndpointAddress.Create(name, address)` continue to compile against the remaining 7-arg `Create(name, address, sessionId?, partitionKey?, ...)` overload, but method-group/delegate assignments that specifically captured the 2-arg shape (e.g. `Func<string,string,IEndpointAddress> f = EndpointAddress.Create;`) no longer compile and must switch to `(n, a) => EndpointAddress.Create(n, a)` or the explicit 7-arg call. This is also a binary-incompatible change for already-compiled consumers — downstream packages must be rebuilt against 0.7.0.
- **`MessageHandlerInvoker.InvokeHandlerAsync` (internal) renamed to `TryInvokeHandler`** with a `bool` return + `out Task handlerTask` — eliminates the `Task?` null sentinel (Sonar S4144). Internal type, only Transport runtime + tests consume it.
- **`ServiceBusReceivedMessageContext` private ctor + `Create` overloads reorder `CancellationToken` to be last** (CA1068). All in-file.
- **Package versions bumped** to `HoneyDrunk.Transport* 0.7.0` per pre-1.0 semver.

### Internal

- Triaged the initial SonarQube Cloud findings against Transport (ADR-0011 D11 gate-cleanup). Refactored `ServiceBusTransportPublisher` (cognitive complexity 23/36 → under 15 via `ApplyEndpointMetadata` / `TryFallbackToBlobAsync` / `TryFallbackBatchToBlobAsync` / `LogIfEnabled` helpers), `StorageQueueProcessor` (27/41 → under 15 via `RunReceiveAndProcessIterationAsync` / `ProcessBatchAsync` / `HandlePipelineResultAsync` / `CompleteSuccessAsync` / `PoisonAsync` / `ScheduleRetryOrPoisonAsync` helpers), `RunNormalModeAsync` (24 → under 15 via `VerifyInvariants` + per-invariant `AssertCorrelationId` / `AssertInitialized` / `AssertInstanceIdentity` / `AssertAccessor`), and `RunNegativeModeAsync` (20 → under 15 via per-test `NegativeTest1..4` helpers + a shared `AddTestKernel`). Extracted nested ternary in `DefaultBlobFallbackStore.SaveAsync` into a `CreateBlobServiceClient` helper. Tightened `EnvelopeMapper.FromServiceBusMessage` timestamp parsing with `CultureInfo.InvariantCulture` + `DateTimeStyles.RoundtripKind`. Switched `InMemoryBroker.GetOrCreateQueue` to use the lambda parameter (no capture). Converted `MessageHandlerInvoker.InvokeHandlerAsync` to a `bool TryInvokeHandler(..., out Task)` pattern so the method no longer returns a null `Task`. Converted `GridContextFactory` to a primary constructor. Moved `InvariantVerificationResult` from the global namespace (the sandbox `Program.cs` top-level statements file) into `HoneyDrunk.Transport.SandboxNode` (`InvariantVerificationResult.cs`). Extracted the 79-char banner literals used in `SandboxNode/Program.cs` and `SampleMessageHandler.cs` into `Banners.Heavy` / `Banners.Light` consts. Added `Record.Exception` / `Record.ExceptionAsync` (or follow-up state assertions) to ~25 previously assertion-free test methods across `MessageContextTests`, `NoOpTransportMetricsTests`, `ServiceBusTransportConsumerMessageProcessingTests`, `InMemoryBrokerAdditionalTests`, `InMemoryBrokerTests`, `InMemoryTransportConsumerTests`, `InMemoryTransportLifecycleAdditionalTests`, `InMemoryTransportTests`, `QueueClientFactoryTests`, and `StorageQueueSenderTests`. Fixed the broken `Assert.Equal(true, ...)` assertion in `TransportHealthResultTests` to `Assert.True((bool)...)`. Simplified the `Validate(StorageQueueOptions)` test helper to a collection expression.
- Bumped `HoneyDrunk.Kernel.Abstractions` / `HoneyDrunk.Kernel` `0.7.0` → `0.8.0`. Transport only uses `GridContextSnapshot` via named arguments, so the Kernel param-order break doesn't affect call sites. Refreshed `Microsoft.Extensions.{Logging.Abstractions, Hosting.Abstractions, Hosting, Logging.Console, Options, Options.DataAnnotations}` to 10.0.8, `Microsoft.Extensions.Azure` to 1.14.0, `Azure.Storage.Blobs` to 12.28.0, and `Azure.Storage.Queues` to 12.26.0.
- Onboarded Transport to SonarQube Cloud (ADR-0011 D11). Wired a `sonarcloud` job in `pr.yml` that calls `HoneyDrunkStudios/HoneyDrunk.Actions/.github/workflows/job-sonarcloud.yml` on both `pull_request` (after `pr-core` succeeds) and `push` to `main` (standalone). PR analysis gates the merge on new-code findings; main-branch analysis populates the SonarCloud Overview dashboard and the leak-period baseline. Per-project source/test classification is discovered automatically from MSBuild `IsTestProject` properties; per-repo Sonar overrides can be added later via `Directory.Build.props` `<SonarQubeSetting>` items or as new inputs to `job-sonarcloud.yml`. Branch-protection requirement added separately after the first successful run lands.

### Changed

- Enabled ADR-0044 OpenClaw/Codex Grid Review Runner request generation for Transport PRs.
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
