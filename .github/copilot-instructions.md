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

## Thread-Safety & Concurrency Patterns

### Disposal Pattern (Critical)
All transport implementations MUST use thread-safe disposal to prevent race conditions:

```csharp
public async ValueTask DisposeAsync()
{
    // 1. Atomic flag prevents double-disposal
    if (Interlocked.Exchange(ref _disposed, true))
        return;
    
    // 2. Acquire lock to synchronize with active operations
    await _initLock.WaitAsync();
    try
    {
        // 3. Dispose resources
        if (_sender != null)
        {
            await _sender.DisposeAsync();
            _sender = null;
        }
    }
    finally
    {
        _initLock.Release();
        _initLock.Dispose();
    }
}
```

**Why this pattern:**
- `Interlocked.Exchange` provides atomic test-and-set for disposal flag
- Lock acquisition ensures no operations in progress during disposal
- Try-finally guarantees lock cleanup even on exceptions
- Setting resource to null after disposal prevents use-after-dispose

### Lifecycle Synchronization
Start/Stop/Dispose operations MUST be synchronized using `SemaphoreSlim`:

```csharp
private readonly SemaphoreSlim _initLock = new(1, 1);
private bool _disposed;

public async Task StartAsync(CancellationToken cancellationToken)
{
    ObjectDisposedException.ThrowIf(_disposed, this);  // Fast-path check
    
    await _initLock.WaitAsync(cancellationToken);
    try
    {
        ObjectDisposedException.ThrowIf(_disposed, this);  // Double-check inside lock
        
        if (_processor != null)
            throw new InvalidOperationException("Already started");
        
        // Initialize resources
        _processor = CreateProcessor();
    }
    finally
    {
        _initLock.Release();
    }
}
```

**Pattern requirements:**
- Check disposal flag before AND after acquiring lock (double-check pattern)
- Validate state inside lock to prevent TOCTOU (Time-Of-Check-Time-Of-Use) races
- Use try-finally to guarantee lock release
- Prefer `ObjectDisposedException.ThrowIf()` for consistent error messages

### Resource Management Best Practices

**Guaranteed Cleanup Pattern:**
```csharp
// Initialize resource BEFORE try block for guaranteed cleanup
ServiceBusMessageBatch? currentBatch = await CreateMessageBatchAsync(ct);

try
{
    foreach (var message in messages)
    {
        if (!currentBatch.TryAddMessage(message))
        {
            // Send and dispose old batch
            if (currentBatch.Count > 0)
                await SendMessagesAsync(currentBatch, ct);
            
            currentBatch.Dispose();
            currentBatch = await CreateMessageBatchAsync(ct);
            
            // Validate message fits
            if (!currentBatch.TryAddMessage(message))
                throw new InvalidOperationException("Message too large");
        }
    }
}
finally
{
    currentBatch.Dispose();  // Always non-null, guaranteed cleanup
}
```

**Try-Finally for Complex Disposal:**
```csharp
private async Task StopAndDisposeProcessorsAsync(CancellationToken ct)
{
    if (_processor != null)
    {
        try
        {
            await _processor.StopProcessingAsync(ct);
        }
        finally
        {
            await _processor.DisposeAsync();  // Guaranteed even if Stop throws
            _processor = null;
        }
    }
}
```

### Collection Thread-Safety

**Immutable Collections for Concurrent Access:**
```csharp
// Use ImmutableList for collections modified during enumeration
private readonly ConcurrentDictionary<string, ImmutableList<Handler>> _subscribers = new();

public void Subscribe(string address, Handler handler)
{
    _subscribers.AddOrUpdate(
        address,
        _ => [handler],  // Create new list
        (_, existing) => existing.Add(handler));  // Returns new list
}

// Safe enumeration - no locking needed
if (_subscribers.TryGetValue(address, out var handlers))
{
    foreach (var handler in handlers)  // Iterating immutable snapshot
        await handler(envelope, ct);
}
```

**Why ImmutableList:**
- Thread-safe for concurrent reads and writes
- No locking required during enumeration
- Structural sharing minimizes memory overhead
- Prevents collection-modified-during-enumeration exceptions

**When to use ConcurrentDictionary:**
- Frequent lookups with occasional updates
- Multiple threads accessing different keys
- Need atomic GetOrAdd/AddOrUpdate operations

### Code Quality Patterns

**Explicit LINQ Filtering:**
```csharp
// ❌ BAD: Implicit filtering in foreach
foreach (var property in message.ApplicationProperties)
{
    if (property.Key != "Reserved1" && property.Key != "Reserved2")
        headers[property.Key] = property.Value;
}

// ✅ GOOD: Explicit Where clause
var reservedProperties = new HashSet<string> { "Reserved1", "Reserved2" };
var headers = message.ApplicationProperties
    .Where(p => !reservedProperties.Contains(p.Key))
    .ToDictionary(p => p.Key, p => p.Value);
```

**Credential Caching:**
```csharp
// ✅ Register credential as singleton for reuse
services.TryAddSingleton<Azure.Core.TokenCredential>(
    sp => new Azure.Identity.DefaultAzureCredential());

// Use in factory
services.AddSingleton<ServiceBusClient>(sp =>
{
    var credential = sp.GetRequiredService<Azure.Core.TokenCredential>();
    return new ServiceBusClient(namespace, credential);
});
```

**Error Handling for Critical Operations:**
```csharp
// ✅ Validate oversized messages explicitly
if (!currentBatch.TryAddMessage(message))
{
    // Create new batch
    currentBatch.Dispose();
    currentBatch = await CreateMessageBatchAsync(ct);
    
    // Check if message is fundamentally too large
    if (!currentBatch.TryAddMessage(message))
    {
        var error = new InvalidOperationException(
            $"Message {message.MessageId} exceeds maximum batch size");
        _logger.LogError(error, "Oversized message");
        throw error;  // Prevent silent data loss
    }
}
```

### Testing Disposal Safety

**Verify thread-safe disposal:**
```csharp
[Fact]
public async Task DisposeAsync_CalledConcurrently_DisposesOnlyOnce()
{
    var publisher = CreatePublisher();
    var disposeCount = 0;
    
    // Simulate concurrent disposal
    var tasks = Enumerable.Range(0, 10)
        .Select(_ => Task.Run(async () =>
        {
            await publisher.DisposeAsync();
            Interlocked.Increment(ref disposeCount);
        }));
    
    await Task.WhenAll(tasks);
    
    // Only first disposal should proceed
    Assert.Equal(1, ActualDisposalCount);
}
```

## Extension Points

- **Custom middleware**: Implement `IMessageMiddleware` and register with `services.AddEnumerable()`
- **Custom serializers**: Implement `IMessageSerializer` and register as singleton
- **Error handling strategies**: Implement `IErrorHandlingStrategy` for custom retry logic
- **Outbox stores**: Implement `IOutboxStore` for database-specific transactional outbox
