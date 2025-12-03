# 🔌 DependencyInjection - Service Registration

[← Back to File Guide](FILE_GUIDE.md)

---

## Table of Contents

- [Overview](#overview)
- [ServiceCollectionExtensions.cs](#servicecollectionextensionscs)
- [ITransportBuilder.cs](#itransportbuildercs)
- [TransportBuilder.cs](#transportbuildercs)
- [JsonMessageSerializer.cs](#jsonmessageserializercs)
- [DelegateMessageHandler.cs](#delegatemessagehandlercs)
- [DelegateMessageMiddleware.cs](#delegatemessagemiddlewarecs)
- [NoOpMiddleware.cs](#noopmiddlewarecs)
- [Registration Patterns](#registration-patterns)
  - [Complete Setup Example](#complete-setup-example)
  - [Testing Configuration](#testing-configuration)
  - [Multiple Transports](#multiple-transports)

---

## Overview

Fluent service registration extensions for configuring transport services. Provides builder pattern for composable transport setup.

**Location:** `HoneyDrunk.Transport/DependencyInjection/`

---

## ServiceCollectionExtensions.cs

```csharp
public static class ServiceCollectionExtensions
{
    public static ITransportBuilder AddHoneyDrunkTransportCore(
        this IServiceCollection services,
        Action<TransportCoreOptions>? configure = null);
    
    public static IServiceCollection AddMessageHandler<TMessage, THandler>(
        this IServiceCollection services)
        where TMessage : class
        where THandler : class, IMessageHandler<TMessage>;
    
    public static IServiceCollection AddMessageHandler<TMessage>(
        this IServiceCollection services,
        MessageHandler<TMessage> handler)
        where TMessage : class;
    
    public static IServiceCollection AddMessageMiddleware<TMiddleware>(
        this IServiceCollection services)
        where TMiddleware : class, IMessageMiddleware;
    
    public static IServiceCollection AddMessageMiddleware(
        this IServiceCollection services,
        MessageMiddleware middleware);
}
```

`AddHoneyDrunkTransportCore` registers:

- The message pipeline (`IMessagePipeline`)
- Built-in middleware (conditionally via `TransportCoreOptions`)
- `JsonMessageSerializer` as the default `IMessageSerializer`
- The transport runtime host (`ITransportRuntime` / `IHostedService`)
- The transport builder (`ITransportBuilder`) for fluent configuration

```csharp
```

### Usage Example

```csharp
// Step 1: Register Kernel (required)
builder.Services.AddHoneyDrunkCoreNode(nodeDescriptor);

// Step 2: Register Transport core
builder.Services.AddHoneyDrunkTransportCore(options =>
{
    options.EnableTelemetry = true;
    options.EnableLogging = true;
    options.EnableCorrelation = true;
});

// Step 3: Register handlers
services.AddMessageHandler<OrderCreated, OrderCreatedHandler>();

// Delegate handler
services.AddMessageHandler<OrderCancelled>(async (message, context, ct) =>
{
    _logger.LogInformation("Order {OrderId} cancelled", message.OrderId);
    return MessageProcessingResult.Success;
});

// Custom middleware
services.AddMessageMiddleware<ValidationMiddleware>();
```

[↑ Back to top](#table-of-contents)

---

## ITransportBuilder.cs

Contract for fluent transport configuration.

```csharp
public interface ITransportBuilder
{
    IServiceCollection Services { get; }
}
```

### Usage Example

```csharp
public static class TransportBuilderExtensions
{
    public static ITransportBuilder WithRetry(
        this ITransportBuilder builder,
        Action<RetryOptions> configure)
    {
        builder.Services.Configure(configure);
        return builder;
    }
}

// Usage
services.AddHoneyDrunkTransportCore()
    .WithRetry(retry =>
    {
        retry.MaxAttempts = 5;
        retry.BackoffStrategy = BackoffStrategy.Exponential;
    });
```

> **Note:** Retry configuration typically belongs to transport adapters (ServiceBus, StorageQueue). Core transport exposes `RetryOptions` for shared behaviors such as pipeline-level retry middleware.

[↑ Back to top](#table-of-contents)

---

## TransportBuilder.cs

Default implementation of `ITransportBuilder`. Created internally by `AddHoneyDrunkTransportCore()`.

```csharp
internal sealed class TransportBuilder(IServiceCollection services) : ITransportBuilder
{
    public IServiceCollection Services { get; } = services;
}
```

This is an internal implementation detail. Transport adapter packages use the `ITransportBuilder` interface to chain configuration.

[↑ Back to top](#table-of-contents)

---

## JsonMessageSerializer.cs

Default JSON serializer implementation using `System.Text.Json`. Registered automatically by `AddHoneyDrunkTransportCore()`.

```csharp
internal sealed class JsonMessageSerializer : IMessageSerializer
{
    public string ContentType => "application/json";
    
    public ReadOnlyMemory<byte> Serialize<TMessage>(TMessage message)
        where TMessage : class;
    
    public TMessage Deserialize<TMessage>(ReadOnlyMemory<byte> payload)
        where TMessage : class;
    
    public object Deserialize(ReadOnlyMemory<byte> payload, Type messageType);
}
```

### Configuration

Uses camelCase property naming and non-indented output for compact payloads:

```csharp
private readonly JsonSerializerOptions _options = new()
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = false
};
```

### Replacing the Default Serializer

To use a custom serializer, register your implementation after `AddHoneyDrunkTransportCore()`:

```csharp
services.AddHoneyDrunkTransportCore();

// Replace with custom serializer (e.g., MessagePack, Protobuf)
services.AddSingleton<IMessageSerializer, MessagePackSerializer>();
```

[↑ Back to top](#table-of-contents)

---

## DelegateMessageHandler.cs

```csharp
public sealed class DelegateMessageHandler<TMessage>(
    MessageHandler<TMessage> handler) 
    : IMessageHandler<TMessage>
    where TMessage : class
{
    public Task<MessageProcessingResult> HandleAsync(
        TMessage message,
        MessageContext context,
        CancellationToken cancellationToken)
    {
        return handler(message, context, cancellationToken);
    }
}
```

### Usage Example

```csharp
// Inline handler without creating a class
services.AddMessageHandler<PaymentCompleted>(
    async (message, context, ct) =>
    {
        await _paymentService.CompleteAsync(message.PaymentId, ct);
        return MessageProcessingResult.Success;
    });
```

> **Note:** Delegate handlers are wrapped in `DelegateMessageHandler<TMessage>` and cached by `MessageHandlerInvoker` for efficient invocation.

[↑ Back to top](#table-of-contents)

---

## DelegateMessageMiddleware.cs

```csharp
public sealed class DelegateMessageMiddleware(MessageMiddleware middleware) 
    : IMessageMiddleware
{
    public Task InvokeAsync(
        ITransportEnvelope envelope,
        MessageContext context,
        Func<Task> next,
        CancellationToken cancellationToken)
    {
        return middleware(envelope, context, next, cancellationToken);
    }
}
```

### Usage Example

```csharp
// Inline middleware without creating a class
services.AddMessageMiddleware(async (envelope, context, next, ct) =>
{
    var sw = Stopwatch.StartNew();
    await next();
    _logger.LogInformation(
        "Processing took {ElapsedMs}ms",
        sw.ElapsedMilliseconds);
});
```

The delegate is wrapped in `DelegateMessageMiddleware` automatically.

[↑ Back to top](#table-of-contents)

---

## NoOpMiddleware.cs

Internal no-op middleware used for conditional registration. Passes through to the next middleware without any processing.

```csharp
internal sealed class NoOpMiddleware : IMessageMiddleware
{
    public Task InvokeAsync(
        ITransportEnvelope envelope,
        MessageContext context,
        Func<Task> next,
        CancellationToken cancellationToken = default)
    {
        return next();
    }
}
```

### Conditional Registration Behavior

| Option | When `false` |
|--------|-------------|
| `EnableLogging` | `LoggingMiddleware` is not registered |
| `EnableTelemetry` | `TelemetryMiddleware` is not registered |
| `EnableCorrelation` | `GridContextPropagationMiddleware` is not registered |

`NoOpMiddleware` may be used internally to maintain pipeline structure when features are conditionally disabled.

[↑ Back to top](#table-of-contents)

---

## Registration Patterns

### Complete Setup Example

```csharp
var builder = WebApplication.CreateBuilder(args);

// 1. Register Kernel (required first)
var nodeDescriptor = new NodeDescriptor
{
    NodeId = "order-service",
    Version = "1.0.0",
    Name = "Order Processing Service",
    Sector = "commerce",
    Cluster = "orders-cluster"
};
builder.Services.AddHoneyDrunkCoreNode(nodeDescriptor);

// 2. Register Transport core with options
builder.Services.AddHoneyDrunkTransportCore(options =>
{
    options.EnableTelemetry = true;
    options.EnableLogging = true;
    options.EnableCorrelation = true;
});

// 3. Add transport implementation
builder.Services
    .AddHoneyDrunkServiceBusTransport(options =>
    {
        options.ConnectionString = builder.Configuration["ServiceBus:ConnectionString"];
        options.TopicName = "orders";
    })
    .WithTopicSubscription("order-processor")
    .WithRetry(retry =>
    {
        retry.MaxAttempts = 5;
        retry.BackoffStrategy = BackoffStrategy.Exponential;
    });

/// 4. Register message handlers
builder.Services.AddMessageHandler<OrderCreated, OrderCreatedHandler>();
builder.Services.AddMessageHandler<OrderUpdated, OrderUpdatedHandler>();
builder.Services.AddMessageHandler<OrderCancelled, OrderCancelledHandler>();

// 5. Add custom middleware
builder.Services.AddMessageMiddleware<TenantResolutionMiddleware>();
builder.Services.AddMessageMiddleware<ValidationMiddleware>();

var app = builder.Build();
app.Run();
```

[↑ Back to top](#table-of-contents)

---

### Testing Configuration

```csharp
// Test project setup - use InMemory transport
public class TestStartup
{
    public void ConfigureServices(IServiceCollection services)
    {
        // Register Kernel
        services.AddHoneyDrunkCoreNode(TestNodeDescriptor);
        
        // Use InMemory transport for tests
        services.AddHoneyDrunkTransportCore()
            .AddHoneyDrunkInMemoryTransport();
        
        // Register test handlers
        services.AddMessageHandler<OrderCreated, OrderCreatedHandler>();
    }
}
```

[↑ Back to top](#table-of-contents)

---

### Multiple Transports

```csharp
// Primary transport (Service Bus)
builder.Services
    .AddHoneyDrunkServiceBusTransport(options =>
    {
        options.ConnectionString = config["ServiceBus:ConnectionString"];
        options.TopicName = "orders";
    });

// Secondary transport (Storage Queue for high-volume events)
builder.Services
    .AddHoneyDrunkStorageQueueTransport(
        config["StorageQueue:ConnectionString"]!,
        "notifications")
    .WithConcurrency(10);
```

[↑ Back to top](#table-of-contents)

---

### Removing or Overriding Middleware

To replace built-in middleware with a custom implementation:

```csharp
// Option 1: Disable via options and add your own
services.AddHoneyDrunkTransportCore(options =>
{
    options.EnableLogging = false;  // Don't register LoggingMiddleware
});
services.AddMessageMiddleware<MyCustomLoggingMiddleware>();

// Option 2: Replace after registration
services.Replace(
    ServiceDescriptor.Singleton<IMessageMiddleware, MyCustomMiddleware>());
```

> **Note:** Middleware order matters. Built-in middleware runs in this order: GridContextPropagation → Telemetry → Logging → Custom middleware → Handler.

[↑ Back to top](#table-of-contents)

---

[← Back to File Guide](FILE_GUIDE.md) | [↑ Back to top](#table-of-contents)
