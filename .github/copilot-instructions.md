# HoneyDrunk.Transport Codebase Guide

## Architecture Overview

HoneyDrunk.Transport is a **transport-agnostic messaging library** for .NET 10.0 that provides a unified abstraction layer over different message brokers. The architecture follows a **middleware pipeline pattern** similar to ASP.NET Core and integrates with **HoneyDrunk.Kernel** for Grid-aware distributed context propagation.

### Core Projects Structure

```
HoneyDrunk.Transport/           # Core abstractions and middleware pipeline
├── Abstractions/               # Publisher/Consumer interfaces, envelope contracts
├── Pipeline/                   # Middleware execution engine
├── Configuration/              # Retry, backoff, error handling options
├── Context/                    # Grid context factory and propagation
├── DependencyInjection/        # Fluent builder pattern for service registration
├── Health/                     # Health contributor abstractions
├── Metrics/                    # Metrics collection abstractions
├── Outbox/                     # Transactional outbox pattern abstractions
└── Primitives/                 # EnvelopeFactory, TransportEnvelope implementations

HoneyDrunk.Transport.AzureServiceBus/   # Azure Service Bus transport implementation
HoneyDrunk.Transport.StorageQueue/      # Azure Storage Queue transport implementation
HoneyDrunk.Transport.InMemory/          # In-memory broker for testing
HoneyDrunk.Transport.Tests/             # xUnit test suite
```

## Key Concepts

### Transport Envelope Pattern
All messages are wrapped in `ITransportEnvelope` which provides:
- `MessageId`, `CorrelationId`, `CausationId` for distributed tracing
- `NodeId`, `StudioId`, `Environment` for Grid topology tracking
- `MessageType` (full type name string) for deserialization routing
- `Headers` dictionary for metadata and Grid baggage
- `Payload` as `ReadOnlyMemory<byte>` for zero-copy performance
- Immutable design with `WithHeaders()` and `WithGridContext()` methods

### Grid Context Integration
Transport is fully integrated with Kernel's `IGridContext` for distributed context propagation.

Message handlers can access Grid context:
```csharp
public class OrderCreatedHandler : IMessageHandler<OrderCreated>
{
    public async Task<MessageProcessingResult> HandleAsync(
        OrderCreated message,
        MessageContext context,
        CancellationToken ct)
    {
        // Access Grid context directly
        var gridContext = context.GridContext;
        
        _logger.LogInformation(
            "Processing order in Node {NodeId} with correlation {CorrelationId}",
            gridContext?.NodeId,
            gridContext?.CorrelationId);
        
        return MessageProcessingResult.Success;
    }
}
```

### Middleware Pipeline Architecture
Message processing follows an **onion-style middleware pattern**:
1. Built-in middleware (GridContext → Telemetry → Logging) executes first
2. Custom middleware can be registered via `ITransportBuilder`
3. Pipeline terminates at message handler invocation

**Built-in Middleware:**
- `GridContextPropagationMiddleware` - Extracts IGridContext from envelope and populates MessageContext
- `TelemetryMiddleware` - Distributed tracing via OpenTelemetry
- `LoggingMiddleware` - Structured logging of message processing

### Fluent Registration Pattern
Transport requires Kernel to be registered first:

```csharp
// Step 1: Register Kernel first
builder.Services.AddHoneyDrunkCoreNode(nodeDescriptor);

// Step 2: Register Transport (layers on top of Kernel)
builder.Services.AddHoneyDrunkTransportCore(options =>
{
    options.EnableTelemetry = true;
    options.EnableLogging = true;
})
.AddHoneyDrunkServiceBusTransport(options => { /* ... */ });
```

## Critical Dependencies

### HoneyDrunk.Kernel.Abstractions
Core abstractions package providing:
- `IGridContext` - Distributed context with correlation, causation, Node/Studio/Environment
- `CorrelationId` - ULID-based strongly-typed correlation IDs
- `TimeProvider` - .NET's built-in time abstraction for testability

**Dependency Strategy:**
- Transport core depends **ONLY** on `HoneyDrunk.Kernel.Abstractions` (zero runtime dependencies)
- Never reference the full `HoneyDrunk.Kernel` package from Transport libraries
- Applications must register Kernel via `AddHoneyDrunkCoreNode()` before calling `AddHoneyDrunkTransportCore()`

**Important**: Always use `EnvelopeFactory` to create envelopes. It integrates with:
- `TimeProvider` for deterministic timestamps
- `CorrelationId.NewId()` for ULID-based message IDs
- `IGridContext` for Grid-aware context propagation

## Implementation Patterns

### Transport Implementations
Each transport provider must implement:
1. `ITransportPublisher` - Sends envelopes to broker
2. `ITransportConsumer` - Receives messages and invokes pipeline
3. `ITransportHealthContributor` - Health checks for Kubernetes probes
4. Registration extension methods in `DependencyInjection/ServiceCollectionExtensions.cs`

Map Grid context fields (NodeId, StudioId, Environment) to broker-specific metadata.

### Health Contributors
Each transport should provide health monitoring:

```csharp
public sealed class ServiceBusHealthContributor : ITransportHealthContributor
{
    public string Name => "Transport.ServiceBus";
    
    public async Task<TransportHealthResult> CheckHealthAsync(CancellationToken ct)
    {
        try
        {
            // Check connectivity
            return TransportHealthResult.Healthy("Connected");
        }
        catch (Exception ex)
        {
            return TransportHealthResult.Unhealthy("Unreachable", ex);
        }
    }
}
```

### Retry & Error Handling
Three-tier error handling:
1. **Transport-level retries** - `RetryOptions` with `BackoffStrategy`
2. **Middleware retries** - `RetryMiddleware`
3. **Message processing results** - Return `MessageProcessingResult` enum:
   - `Success` - Complete successfully
   - `Retry` - Reprocess with backoff
   - `DeadLetter` - Move to DLQ, no retry

### Transactional Outbox Pattern
For exactly-once processing with database transactions:
1. Implement `IOutboxStore` against your database
2. Save messages via `SaveAsync()` within your unit-of-work transaction
3. Background `IOutboxDispatcher` polls `LoadPendingAsync()` and publishes

## Development Workflows

### Adding New Transport Providers
1. Create project `HoneyDrunk.Transport.{BrokerName}/`
2. Reference **ONLY** `HoneyDrunk.Kernel.Abstractions` (not full Kernel)
3. Reference `HoneyDrunk.Transport` core project
4. Implement `ITransportPublisher` and `ITransportConsumer`
5. Implement `ITransportHealthContributor` for health checks
6. Map Grid context fields (NodeId, StudioId, Environment) to broker metadata
7. Follow Azure Service Bus implementation as reference template

## Code Style Conventions

### Primary Constructors
Modern C# 14 syntax:
```csharp
public sealed class ServiceBusTransportPublisher(
    ServiceBusClient client,
    IOptions<AzureServiceBusOptions> options) : ITransportPublisher
{
    private readonly ServiceBusClient _client = client;
}
```

### Service Registration
- Use `TryAddSingleton/TryAddScoped` for overrideable defaults
- Middleware uses `AddEnumerable` to support multiple registrations
- Always register `TimeProvider.System` as default time provider

## Common Pitfalls

1. **Don't construct `TransportEnvelope` directly** - Use `EnvelopeFactory`
2. **Always register Kernel first** - Call `AddHoneyDrunkCoreNode()` before `AddHoneyDrunkTransportCore()`
3. **Use Abstractions only** - Depend on `HoneyDrunk.Kernel.Abstractions`, not full `HoneyDrunk.Kernel`
4. **Middleware order matters** - GridContextPropagation must run before telemetry
5. **Async disposal** - Transport implementations implement `IAsyncDisposable`
6. **Immutable envelopes** - Use `WithGridContext()` to create modified copies
7. **Grid context propagation** - Always map NodeId/StudioId/Environment across transports

## Thread-Safety & Concurrency Patterns

### Disposal Pattern (Critical)
Use thread-safe disposal:

```csharp
public async ValueTask DisposeAsync()
{
    if (Interlocked.Exchange(ref _disposed, true))
        return;
    
    await _initLock.WaitAsync();
    try
    {
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

### Collection Thread-Safety
Use `ImmutableList` for collections modified during enumeration:

```csharp
private readonly ConcurrentDictionary<string, ImmutableList<Handler>> _subscribers = new();

public void Subscribe(string address, Handler handler)
{
    _subscribers.AddOrUpdate(
        address,
        _ => [handler],
        (_, existing) => existing.Add(handler));
}
```

## Extension Points

- **Custom middleware**: Implement `IMessageMiddleware`
- **Custom serializers**: Implement `IMessageSerializer`
- **Error handling strategies**: Implement `IErrorHandlingStrategy`
- **Outbox stores**: Implement `IOutboxStore`
- **Health contributors**: Implement `ITransportHealthContributor`
- **Metrics collectors**: Implement `ITransportMetrics`
