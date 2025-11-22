# 🔌 DependencyInjection - Service Registration

[← Back to File Guide](FILE_GUIDE.md)

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
}
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

---

## ITransportBuilder.cs

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

// 4. Register message handlers
builder.Services.AddMessageHandler<OrderCreated, OrderCreatedHandler>();
builder.Services.AddMessageHandler<OrderUpdated, OrderUpdatedHandler>();
builder.Services.AddMessageHandler<OrderCancelled, OrderCancelledHandler>();

// 5. Add custom middleware
builder.Services.AddMessageMiddleware<TenantResolutionMiddleware>();
builder.Services.AddMessageMiddleware<ValidationMiddleware>();

var app = builder.Build();
app.Run();
```

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

---

[← Back to File Guide](FILE_GUIDE.md)
