# HoneyDrunk.Transport Codebase Guide

## Architecture Overview

HoneyDrunk.Transport is a **transport-agnostic messaging library** for .NET 10.0 that provides a unified abstraction layer over different message brokers. The architecture follows a **middleware pipeline pattern** similar to ASP.NET Core.

### Core Projects Structure

```
HoneyDrunk.Transport/           # Core abstractions and middleware pipeline
├── Abstractions/               # Publisher/Consumer interfaces, envelope contracts
├── Pipeline/                   # Middleware execution engine
├── Configuration/              # Retry, backoff, error handling options
├── DependencyInjection/        # Fluent builder pattern for service registration
├── Outbox/                     # Transactional outbox pattern abstractions
└── Primitives/                 # EnvelopeFactory, TransportEnvelope implementations

HoneyDrunk.Transport.AzureServiceBus/   # Azure Service Bus transport implementation
HoneyDrunk.Transport.InMemory/          # In-memory broker for testing
HoneyDrunk.Transport.Tests/             # xUnit test suite
```

## Key Concepts

### Transport Envelope Pattern
All messages are wrapped in `ITransportEnvelope` which provides:
- `MessageId`, `CorrelationId`, `CausationId` for distributed tracing
- `MessageType` (full type name string) for deserialization routing
- `Headers` dictionary for metadata
- `Payload` as `ReadOnlyMemory<byte>` for zero-copy performance
- Immutable design with `WithHeaders()` and `WithCorrelation()` methods

Example from `Primitives/TransportEnvelope.cs`:
```csharp
public ITransportEnvelope WithHeaders(IDictionary<string, string> additionalHeaders)
{
    var combined = new Dictionary<string, string>(Headers);
    foreach (var kvp in additionalHeaders)
        combined[kvp.Key] = kvp.Value;
    
    return new TransportEnvelope { /* copy all properties */ };
}
```

### Middleware Pipeline Architecture
Message processing follows an **onion-style middleware pattern**:
1. Built-in middleware (Correlation → Telemetry → Logging) executes first
2. Custom middleware can be registered via `ITransportBuilder`
3. Pipeline terminates at message handler invocation
4. Middleware registered in `ServiceCollectionExtensions.cs` using `IMessageMiddleware`

Key implementation in `Pipeline/MessagePipeline.cs`:
```csharp
// Middleware wraps handlers in reverse order (LIFO)
foreach (var middleware in _middlewares.Reverse())
{
    var next = handler;
    handler = () => middleware.InvokeAsync(envelope, context, next, cancellationToken);
}
```

### Fluent Registration Pattern
All transport configuration uses **builder pattern returning `ITransportBuilder`**:

```csharp
services.AddHoneyDrunkTransportCore()
    .AddHoneyDrunkServiceBusTransport(options => { /* ... */ })
    .WithTopicSubscription("my-subscription")
    .WithSessions()
    .WithRetry(retry => retry.MaxAttempts = 5);
```

See `DependencyInjection/ServiceCollectionExtensions.cs` for the pattern.

### Message Handler Registration
Handlers implement `IMessageHandler<TMessage>`:
```csharp
services.AddMessageHandler<MyMessage, MyMessageHandler>();
// OR delegate-based:
services.AddMessageHandler<MyMessage>(async (msg, ctx, ct) => { /* ... */ });
```

## Critical Dependencies

### HoneyDrunk.Kernel
Core infrastructure package providing:
- `IIdGenerator` - Message ID generation
- `IClock` - Timestamp abstraction for testability
- `IContextAccessor` - Ambient context propagation
- Registered via `services.AddKernelDefaults()` in core setup

**Important**: Always use `EnvelopeFactory` (depends on Kernel) to create envelopes, never construct `TransportEnvelope` directly in production code.

### Documentation Generation
All projects have `<GenerateDocumentationFile>true</GenerateDocumentationFile>` enabled. **Always add XML doc comments** to public APIs.

## Implementation Patterns

### Transport Implementations
Each transport provider must implement:
1. `ITransportPublisher` - Sends envelopes to broker
2. `ITransportConsumer` - Receives messages and invokes pipeline
3. Registration extension methods in `DependencyInjection/ServiceCollectionExtensions.cs`

Example from `AzureServiceBus/ServiceBusTransportPublisher.cs`:
- Use `TransportTelemetry.StartPublishActivity()` for distributed tracing
- Call `EnvelopeMapper.ToServiceBusMessage()` for format conversion
- Always record outcome with `TransportTelemetry.RecordOutcome()`

### Retry & Error Handling
Three-tier error handling:
1. **Transport-level retries** - `RetryOptions` with `BackoffStrategy` (Fixed/Linear/Exponential)
2. **Middleware retries** - `RetryMiddleware` (currently in Middleware/ folder)
3. **Message processing results** - Return `MessageProcessingResult` enum:
   - `Success` - Complete successfully
   - `Retry` - Reprocess with backoff
   - `DeadLetter` - Move to DLQ, no retry

Example from `Pipeline/MessagePipeline.cs`:
```csharp
catch (MessageHandlerException ex)
{
    return ex.Result; // Handler controls retry behavior
}
catch (Exception ex)
{
    return MessageProcessingResult.Retry; // Default to retry on unexpected errors
}
```

### Transactional Outbox Pattern
For exactly-once processing with database transactions:
1. Implement `IOutboxStore` against your database
2. Save messages via `SaveAsync()` within your unit-of-work transaction
3. Background `IOutboxDispatcher` polls `LoadPendingAsync()` and publishes
4. Track state with `OutboxMessageState` (Pending → Dispatched/Failed/Poisoned)

See `Outbox/IOutboxStore.cs` for the contract.

## Development Workflows

### Building & Testing
```powershell
dotnet build HoneyDrunk.Transport.slnx
dotnet test HoneyDrunk.Transport.Tests/HoneyDrunk.Transport.Tests.csproj
```

### Adding New Transport Providers
1. Create project `HoneyDrunk.Transport.{BrokerName}/`
2. Reference `HoneyDrunk.Transport` core project
3. Implement `ITransportPublisher` and `ITransportConsumer`
4. Create `Configuration/{BrokerName}Options.cs` for broker-specific settings
5. Add extension methods: `AddHoneyDrunk{BrokerName}Transport(this IServiceCollection)`
6. Follow Azure Service Bus implementation as reference template

### Testing with InMemory Transport
Use `HoneyDrunk.Transport.InMemory` for integration tests:
- `InMemoryBroker` provides observable queues via `Channel<T>`
- Supports both queue-based consumption and pub/sub subscriptions
- No external dependencies, runs in-process

## Code Style Conventions

### Nullable Reference Types
Project uses `<Nullable>enable</Nullable>`. **Always annotate**:
- Use `?` for nullable parameters and properties
- Use `!` null-forgiving operator only when justified with comment
- Prefer `ArgumentNullException.ThrowIfNull()` for guard clauses

### Primary Constructors
Modern C# 12 syntax used throughout:
```csharp
public sealed class ServiceBusTransportPublisher(
    ServiceBusClient client,
    IOptions<AzureServiceBusOptions> options,
    ILogger<ServiceBusTransportPublisher> logger) : ITransportPublisher
{
    private readonly ServiceBusClient _client = client;
    // ...
}
```

### Service Registration
- Use `TryAddSingleton/TryAddScoped` for overrideable defaults
- Use `AddSingleton` when replacement should be explicit
- Middleware uses `AddEnumerable` to support multiple registrations

### Telemetry Integration
**Always wrap operations** with telemetry activities:
```csharp
using var activity = TransportTelemetry.StartPublishActivity(envelope, destination);
try 
{
    // operation
    TransportTelemetry.RecordOutcome(activity, MessageProcessingResult.Success);
}
catch (Exception ex)
{
    TransportTelemetry.RecordError(activity, ex);
    throw;
}
```

## Common Pitfalls

1. **Don't construct `TransportEnvelope` directly** - Use `EnvelopeFactory` which provides `MessageId` and `Timestamp` from Kernel services
2. **Message type resolution** - `MessageType` must be fully qualified type name resolvable via `Type.GetType()`
3. **Middleware order matters** - Correlation must run before telemetry to populate trace context
4. **Async disposal** - Transport implementations like `ServiceBusTransportPublisher` implement `IAsyncDisposable` for cleanup
5. **Immutable envelopes** - Use `With*()` methods to create modified copies, never mutate

## Extension Points

- **Custom middleware**: Implement `IMessageMiddleware` and register with `services.AddEnumerable()`
- **Custom serializers**: Implement `IMessageSerializer` and register as singleton
- **Error handling strategies**: Implement `IErrorHandlingStrategy` for custom retry logic
- **Outbox stores**: Implement `IOutboxStore` for database-specific transactional outbox

## Related Documentation

- See `Configuration/TransportCoreOptions.cs` for available toggles (telemetry, logging, correlation)
- Check `Abstractions/MessageProcessingResult.cs` for message outcome enumeration
- Review `AzureServiceBus/Mapping/EnvelopeMapper.cs` for transport format conversion patterns
