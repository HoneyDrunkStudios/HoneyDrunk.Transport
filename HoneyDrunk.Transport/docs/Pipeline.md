# 🔄 Pipeline - Message Processing Chain

[← Back to File Guide](FILE_GUIDE.md)

---

## Table of Contents

- [Overview](#overview)
- **Core Pipeline** (`HoneyDrunk.Transport/Pipeline/`)
  - [IMessagePipeline.cs](#imessagepipelinecs)
  - [MessagePipeline.cs](#messagepipelinecs)
  - [IMessageMiddleware.cs](#imessagemiddlewarecs)
  - [MessageMiddleware.cs](#messagemiddlewarecs)
  - [MessageHandlerInvoker.cs](#messagehandlerinvokercs)
  - [TransportExecutionContext.cs](#transportexecutioncontextcs)
  - [MessageHandlerException.cs](#messagehandlerexceptioncs)
- **Built-in Middleware** (`HoneyDrunk.Transport/Pipeline/Middleware/`)
  - [GridContextPropagationMiddleware.cs](#gridcontextpropagationmiddlewarecs)
  - [LoggingMiddleware.cs](#loggingmiddlewarecs)
  - [RetryMiddleware.cs](#retrymiddlewarecs)
  - [CorrelationMiddleware.cs](#correlationmiddlewarecs) *(deprecated)*
- **Telemetry Middleware** (`HoneyDrunk.Transport/Telemetry/`)
  - [TelemetryMiddleware.cs](#telemetrymiddlewarecs)

---

## Overview

The middleware pipeline system that processes messages in an onion-style pattern. Each middleware layer wraps the next, enabling cross-cutting concerns like logging, telemetry, and validation.

**Location:** `HoneyDrunk.Transport/Pipeline/`

### Error Handling Flow

When a handler throws an exception, the pipeline invokes `IErrorHandlingStrategy.HandleErrorAsync(exception, deliveryCount, cancellationToken)` to decide how to handle the failure. The strategy returns an `ErrorHandlingDecision`, which the pipeline converts to a `MessageProcessingResult` for the transport adapter.

> **Note:** Error handling strategies only receive the exception and delivery count—they do not see the full `MessageContext`. The pipeline is responsible for passing `context.DeliveryCount` into the strategy and then mapping the resulting `ErrorHandlingDecision` to a `MessageProcessingResult`. See [Configuration → IErrorHandlingStrategy](Configuration.md#ierrorhandlingstrategycs) for strategy options.

### Default Middleware Order

When all features are enabled, the pipeline executes middleware in the following order:

1. `GridContextPropagationMiddleware` (correlation / Grid context)
2. `TelemetryMiddleware` (OpenTelemetry spans)
3. `LoggingMiddleware` (structured logging)
4. `RetryMiddleware` (max attempt guard)
5. Handler invocation (`MessageHandlerInvoker`)

Custom middleware is inserted between 1–4 depending on registration order.

---

# Core Pipeline

---

## IMessagePipeline.cs

Contract for the message processing pipeline.

```csharp
public interface IMessagePipeline
{
    Task<MessageProcessingResult> ProcessAsync(
        ITransportEnvelope envelope,
        MessageContext context,
        CancellationToken cancellationToken = default);
}
```

### Purpose

Entry point for message processing. Transport consumers call `ProcessAsync` when messages arrive, and the pipeline orchestrates middleware execution and handler invocation.

[↑ Back to top](#table-of-contents)

---

## MessagePipeline.cs

Default implementation of the message processing pipeline.

```csharp
public sealed class MessagePipeline : IMessagePipeline
{
    public MessagePipeline(
        IEnumerable<IMessageMiddleware> middlewares,
        IMessageSerializer serializer,
        IServiceProvider serviceProvider,
        ILogger<MessagePipeline> logger);
    
    public Task<MessageProcessingResult> ProcessAsync(
        ITransportEnvelope envelope,
        MessageContext context,
        CancellationToken cancellationToken = default);
}
```

### How It Works

1. **Middleware chain building**: Reverses middleware order to build an onion-style pipeline
2. **Pipeline execution**: Each middleware wraps the next, allowing pre/post processing
3. **Handler invocation**: At the core, the pipeline deserializes the message and invokes the registered handler
4. **Exception handling**:
   - `OperationCanceledException` → re-thrown for caller handling
   - `MessageHandlerException` → returns the exception's `Result`
   - Other exceptions → delegated to `IErrorHandlingStrategy.HandleErrorAsync(exception, context.DeliveryCount, cancellationToken)`. The resulting `ErrorHandlingDecision` is converted into a `MessageProcessingResult` for the transport adapter.

### Exception Handling Flow

```csharp
try
{
    await _composedMiddleware(envelope, context, cancellationToken);
    return MessageProcessingResult.Success;
}
catch (MessageHandlerException ex)
{
    return ex.Result;
}
catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
{
    var decision = await _errorStrategy.HandleErrorAsync(
        ex,
        context.DeliveryCount,
        cancellationToken);

    return decision.Action switch
    {
        ErrorHandlingAction.Retry      => MessageProcessingResult.Retry,
        ErrorHandlingAction.DeadLetter => MessageProcessingResult.DeadLetter,
        ErrorHandlingAction.Abandon    => MessageProcessingResult.Abandon,
        _                              => MessageProcessingResult.Retry
    };
}
```

### Usage Example

```csharp
// Registered automatically by AddHoneyDrunkTransportCore()
// Transport consumers use the pipeline internally:

public class ServiceBusConsumer : ITransportConsumer
{
    private readonly IMessagePipeline _pipeline;
    
    private async Task ProcessMessageAsync(ServiceBusReceivedMessage message)
    {
        var envelope = MapToTransportEnvelope(message);
        var context = new MessageContext
        {
            Envelope = envelope,
            Transaction = new ServiceBusTransaction(message),
            DeliveryCount = (int)message.DeliveryCount
        };
        
        var result = await _pipeline.ProcessAsync(envelope, context, ct);
        // Handle result...
    }
}
```

[↑ Back to top](#table-of-contents)

---

## IMessageMiddleware.cs

Contract for middleware components in the processing pipeline.

```csharp
public interface IMessageMiddleware
{
    Task InvokeAsync(
        ITransportEnvelope envelope,
        MessageContext context,
        Func<Task> next,
        CancellationToken cancellationToken);
}
```

### Usage Example

```csharp
public class TenantResolutionMiddleware(ITenantResolver resolver) 
    : IMessageMiddleware
{
    public async Task InvokeAsync(
        ITransportEnvelope envelope,
        MessageContext context,
        Func<Task> next,
        CancellationToken ct)
    {
        // Extract tenant from headers
        if (envelope.Headers.TryGetValue("TenantId", out var tenantId))
        {
            var tenant = await resolver.ResolveAsync(tenantId, ct);
            context.Properties["Tenant"] = tenant;
        }
        
        // Continue pipeline
        await next();
    }
}

services.AddMessageMiddleware<TenantResolutionMiddleware>();
```

[↑ Back to top](#table-of-contents)

---

## MessageMiddleware.cs

Delegate type for functional middleware (alternative to interface-based).

```csharp
public delegate Task MessageMiddleware(
    ITransportEnvelope envelope,
    MessageContext context,
    Func<Task> next,
    CancellationToken cancellationToken);
```

### Purpose

Lightweight delegate alternative to `IMessageMiddleware` interface. Useful for inline middleware or simple cross-cutting concerns.

> **DI Registration:** For registering delegate-based middleware with dependency injection, see `DelegateMessageMiddleware` in the [DependencyInjection](DependencyInjection.md) documentation.

### Usage Example

```csharp
// Inline functional middleware
MessageMiddleware validationMiddleware = async (envelope, context, next, ct) =>
{
    if (string.IsNullOrEmpty(envelope.MessageId))
        throw new MessageHandlerException("Missing MessageId", MessageProcessingResult.DeadLetter);
    
    await next();
};
```

[↑ Back to top](#table-of-contents)

---

## MessageHandlerInvoker.cs

High-performance handler invocation using compiled expression trees.

```csharp
internal sealed class MessageHandlerInvoker
{
    public MessageHandlerInvoker(IServiceProvider serviceProvider);
    
    public Task? InvokeHandlerAsync(
        object message,
        Type messageType,
        MessageContext context,
        CancellationToken cancellationToken);
}
```

### Purpose

Avoids reflection overhead on every message by caching compiled delegates per message type. This is an internal implementation detail of `MessagePipeline`.

### How It Works

1. **First invocation**: Compiles an expression tree for `IMessageHandler<T>.HandleAsync`
2. **Subsequent invocations**: Uses cached delegate for near-native performance
3. **Thread-safe**: Uses `ConcurrentDictionary` for delegate cache
4. **Null return**: Returns `null` if no handler is registered for the message type

[↑ Back to top](#table-of-contents)

---

## TransportExecutionContext.cs *(internal, v2-ready)*

Internal pipeline context for tracking broker-specific metadata and execution state.

```csharp
public sealed class TransportExecutionContext
{
    public required ITransportEnvelope Envelope { get; init; }
    public IGridContext? GridContext { get; set; }
    public required ITransportTransaction Transaction { get; init; }
    public Dictionary<string, object> BrokerProperties { get; init; } = [];
    public int RetryAttempt { get; set; }
    public TimeSpan ProcessingDuration { get; set; }
    public int DeliveryCount => RetryAttempt + 1;
    
    public MessageContext ToMessageContext();
}
```

### Purpose

`TransportExecutionContext` is used **internally** by the pipeline to track broker-specific metadata, retry attempts, and processing duration. The public pipeline surface still exposes `MessageContext` to middleware and handlers in v1.x.

> **Future:** In v2, middleware may receive `TransportExecutionContext` directly as a breaking change, enabling richer middleware scenarios without copying properties.

### Properties

| Property | Description |
|----------|-------------|
| `Envelope` | The transport envelope being processed |
| `GridContext` | Grid context (populated by GridContextPropagationMiddleware) |
| `Transaction` | Transaction context for commit/rollback |
| `BrokerProperties` | Adapter-specific metadata (lock tokens, etc.) |
| `RetryAttempt` | Current retry attempt (0 = first attempt) |
| `ProcessingDuration` | Cumulative processing time across retries |
| `DeliveryCount` | `RetryAttempt + 1` |

### v1.x Behavior

In v1.x, the pipeline:
1. Creates `TransportExecutionContext` internally with broker properties
2. Calls `ToMessageContext()` to create the public `MessageContext`
3. Copies select broker properties into `MessageContext.Properties`
4. Passes `MessageContext` to middleware and handlers

```csharp
// Transport adapters populate Properties that middleware can access:
public class VisibilityExtensionMiddleware : IMessageMiddleware
{
    public async Task InvokeAsync(
        ITransportEnvelope envelope,
        MessageContext context,
        Func<Task> next,
        CancellationToken ct)
    {
        // LockToken is copied into Properties by the transport adapter
        if (context.Properties.TryGetValue("LockToken", out var lockToken))
        {
            // Extend visibility if processing is taking too long
        }
        
        await next();
    }
}
```

[↑ Back to top](#table-of-contents)

---

## MessageHandlerException.cs

Exception for signaling specific processing outcomes from handlers.

```csharp
public sealed class MessageHandlerException : Exception
{
    public MessageProcessingResult Result { get; }
    
    public MessageHandlerException(
        string message,
        MessageProcessingResult result);
    
    public MessageHandlerException(
        string message,
        MessageProcessingResult result,
        Exception innerException);
}
```

### Purpose

Allows handlers and middleware to signal a specific `MessageProcessingResult` by throwing an exception. The pipeline catches this and returns the specified result.

### Usage Example

```csharp
public async Task<MessageProcessingResult> HandleAsync(
    OrderCreated message,
    MessageContext context,
    CancellationToken ct)
{
    if (message.OrderId <= 0)
    {
        throw new MessageHandlerException(
            "Invalid OrderId",
            MessageProcessingResult.DeadLetter);
    }
    
    return MessageProcessingResult.Success;
}
```

[↑ Back to top](#table-of-contents)

---

# Built-in Middleware

---

## GridContextPropagationMiddleware.cs

Propagates Grid context across Node boundaries.

```csharp
public sealed class GridContextPropagationMiddleware : IMessageMiddleware
{
    public GridContextPropagationMiddleware(IGridContextFactory gridContextFactory);
    
    public Task InvokeAsync(
        ITransportEnvelope envelope,
        MessageContext context,
        Func<Task> next,
        CancellationToken cancellationToken = default);
}
```

### Purpose

The canonical bridge between Kernel Grid context and Transport messaging. Extracts `IGridContext` from envelope metadata and populates `MessageContext.GridContext` for handlers.

### Context Propagation

Extracts and propagates:
- `CorrelationId` - Distributed tracing correlation
- `CausationId` - Message causation chain
- `NodeId` - Source Grid Node
- `StudioId` - HoneyDrunk Studio identifier
- `Environment` - Deployment environment
- `Baggage` - Custom key-value metadata

### Registration

```csharp
// Registered automatically when EnableCorrelation = true
services.AddHoneyDrunkTransportCore(options =>
{
    options.EnableCorrelation = true; // Enables GridContextPropagationMiddleware
});
```

[↑ Back to top](#table-of-contents)

---

## LoggingMiddleware.cs

Logs message receipt, processing, and outcomes with structured logging.

```csharp
public sealed class LoggingMiddleware : IMessageMiddleware
{
    public LoggingMiddleware(ILogger<LoggingMiddleware> logger);
    
    public Task InvokeAsync(
        ITransportEnvelope envelope,
        MessageContext context,
        Func<Task> next,
        CancellationToken ct);
}
```

### What It Logs

- **On receive**: Message type, message ID
- **On success**: Message type, elapsed time (ms)
- **On failure**: Exception, message type, elapsed time (ms)

### Registration

```csharp
// Registered automatically when EnableLogging = true
services.AddHoneyDrunkTransportCore(options =>
{
    options.EnableLogging = true; // Enables LoggingMiddleware
});
```

[↑ Back to top](#table-of-contents)

---

## RetryMiddleware.cs

Enforces maximum retry limits.

```csharp
public sealed class RetryMiddleware : IMessageMiddleware
{
    public RetryMiddleware(ILogger<RetryMiddleware> logger, int maxAttempts = 3);
    
    public Task InvokeAsync(
        ITransportEnvelope envelope,
        MessageContext context,
        Func<Task> next,
        CancellationToken cancellationToken = default);
}
```

### Behavior

- Checks `context.DeliveryCount` against `maxAttempts`
- If exceeded, throws `MessageHandlerException` with `MessageProcessingResult.DeadLetter`
- Otherwise, passes through to next middleware

> **Note:** `RetryMiddleware` is a simple guardrail on top of broker-level delivery attempts. It does **not** reschedule messages itself; it only forces a `DeadLetter` result once `DeliveryCount` exceeds `maxAttempts`, letting the transport adapter move the message to DLQ. Backoff delays are handled by `IErrorHandlingStrategy`, not this middleware.

### Usage Example

```csharp
// Configure max retry attempts
services.AddSingleton<IMessageMiddleware>(sp => 
    new RetryMiddleware(
        sp.GetRequiredService<ILogger<RetryMiddleware>>(),
        maxAttempts: 5));
```

[↑ Back to top](#table-of-contents)

---

## CorrelationMiddleware.cs

> **⚠️ DEPRECATED:** Use `GridContextPropagationMiddleware` for full Grid context propagation. This middleware remains for backward compatibility only and will be removed in v1.0.

> **Migration:** `CorrelationMiddleware` is **not added by default**. Only resolve it manually if you are migrating legacy code that depends on the `KernelContext` property key.

```csharp
[Obsolete("Use GridContextPropagationMiddleware for full Grid context propagation.")]
public sealed class CorrelationMiddleware : IMessageMiddleware
{
    public CorrelationMiddleware(IGridContextFactory gridContextFactory);
    
    public Task InvokeAsync(
        ITransportEnvelope envelope,
        MessageContext context,
        Func<Task> next,
        CancellationToken cancellationToken = default);
}
```

### Legacy Behavior

- Creates Grid context from envelope
- Stores in `context.Properties["KernelContext"]` (legacy key)
- Stores in `context.Properties["GridContext"]`
- Stores `CorrelationId` and `CausationId` separately

[↑ Back to top](#table-of-contents)

---

# Telemetry Middleware

---

## TelemetryMiddleware.cs

**Location:** `HoneyDrunk.Transport/Telemetry/TelemetryMiddleware.cs`

Creates OpenTelemetry spans for distributed tracing.

```csharp
public sealed class TelemetryMiddleware : IMessageMiddleware
{
    public Task InvokeAsync(
        ITransportEnvelope envelope,
        MessageContext context,
        Func<Task> next,
        CancellationToken cancellationToken = default);
}
```

### What It Records

- **Activity span**: `TransportTelemetry.StartProcessActivity(envelope, "consumer")`
- **Delivery count**: As span attribute
- **Outcome**: Success or error status
- **Exception**: On failure, records exception details

### Registration

```csharp
// Registered automatically when EnableTelemetry = true
services.AddHoneyDrunkTransportCore(options =>
{
    options.EnableTelemetry = true; // Enables TelemetryMiddleware
});
```

[↑ Back to top](#table-of-contents)

---

[← Back to File Guide](FILE_GUIDE.md) | [↑ Back to top](#table-of-contents)
