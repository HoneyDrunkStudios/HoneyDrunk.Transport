# 🔄 Pipeline - Message Processing Chain

[← Back to File Guide](FILE_GUIDE.md)

---

## Overview

The middleware pipeline system that processes messages in an onion-style pattern. Each middleware layer wraps the next, enabling cross-cutting concerns like logging, telemetry, and validation.

**Location:** `HoneyDrunk.Transport/Pipeline/`

---

## IMessagePipeline.cs

```csharp
public interface IMessagePipeline
{
    Task<MessageProcessingResult> ExecuteAsync(
        ITransportEnvelope envelope,
        CancellationToken cancellationToken = default);
}
```

### Usage Example

```csharp
// Used internally by consumers - not typically called directly
// Pipeline automatically invoked when messages arrive
```

---

## IMessageMiddleware.cs

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

---

## Built-in Middleware

### GridContextPropagationMiddleware.cs

Extracts Grid context from envelope and populates MessageContext.

```csharp
public sealed class GridContextPropagationMiddleware(IGridContextFactory factory) 
    : IMessageMiddleware
{
    public async Task InvokeAsync(
        ITransportEnvelope envelope,
        MessageContext context,
        Func<Task> next,
        CancellationToken ct)
    {
        // Extract Grid context from envelope
        context.GridContext = factory.CreateFromEnvelope(envelope, ct);
        
        await next();
    }
}
```

### Usage Example

```csharp
// Registered automatically when EnableCorrelation = true
services.AddHoneyDrunkTransportCore(options =>
{
    options.EnableCorrelation = true; // Enables GridContextPropagationMiddleware
});
```

---

### LoggingMiddleware.cs

Logs message receipt, processing, and outcomes.

```csharp
public sealed class LoggingMiddleware(ILogger<LoggingMiddleware> logger) 
    : IMessageMiddleware
{
    public async Task InvokeAsync(
        ITransportEnvelope envelope,
        MessageContext context,
        Func<Task> next,
        CancellationToken ct)
    {
        logger.LogInformation(
            "Received message {MessageType} with ID {MessageId}",
            envelope.MessageType,
            envelope.MessageId);
        
        var sw = Stopwatch.StartNew();
        
        try
        {
            await next();
            logger.LogInformation(
                "Successfully processed {MessageType} in {ElapsedMs}ms",
                envelope.MessageType,
                sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to process {MessageType} after {ElapsedMs}ms",
                envelope.MessageType,
                sw.ElapsedMilliseconds);
            throw;
        }
    }
}
```

### Usage Example

```csharp
// Registered automatically when EnableLogging = true
services.AddHoneyDrunkTransportCore(options =>
{
    options.EnableLogging = true; // Enables LoggingMiddleware
});
```

---

### TelemetryMiddleware.cs

Creates OpenTelemetry spans for distributed tracing.

```csharp
public sealed class TelemetryMiddleware : IMessageMiddleware
{
    public async Task InvokeAsync(
        ITransportEnvelope envelope,
        MessageContext context,
        Func<Task> next,
        CancellationToken ct)
    {
        using var activity = TransportTelemetry.StartConsumeActivity(envelope);
        
        try
        {
            await next();
            TransportTelemetry.RecordOutcome(activity, MessageProcessingResult.Success);
        }
        catch (Exception ex)
        {
            TransportTelemetry.RecordError(activity, ex);
            throw;
        }
    }
}
```

### Usage Example

```csharp
// Registered automatically when EnableTelemetry = true
services.AddHoneyDrunkTransportCore(options =>
{
    options.EnableTelemetry = true; // Enables TelemetryMiddleware
});
```

---

## MessageHandlerException.cs

```csharp
public sealed class MessageHandlerException : Exception
{
    public MessageProcessingResult Result { get; }
    
    public MessageHandlerException(
        string message,
        MessageProcessingResult result)
        : base(message)
    {
        Result = result;
    }
}
```

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

---

[← Back to File Guide](FILE_GUIDE.md)
