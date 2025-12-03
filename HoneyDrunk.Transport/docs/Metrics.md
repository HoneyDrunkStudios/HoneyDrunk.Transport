# 📈 Metrics - Telemetry and Observability

[← Back to File Guide](FILE_GUIDE.md)

---

## Table of Contents

- [Overview](#overview)
- [ITransportMetrics.cs](#itransportmetricscs)
- [NoOpTransportMetrics.cs](#nooptransportmetricscs)
- [OpenTelemetryTransportMetrics.cs](#opentelemetrytransportmetricscs)
- [KernelTransportMetrics.cs](#kerneltransportmetricscs)
- [TransportTelemetry.cs](#transporttelemetrycs)

---

## Overview

Metrics collection abstractions for recording transport operations. Integrates with Kernel metrics and OpenTelemetry.

**Locations:**

- **Metrics:** `HoneyDrunk.Transport/Metrics/`
  - `ITransportMetrics.cs` - Metrics abstraction
  - `NoOpTransportMetrics.cs` - Default no-op implementation

- **Telemetry:** `HoneyDrunk.Transport/Telemetry/`
  - `TransportTelemetry.cs` - ActivitySource utilities
  - `TelemetryMiddleware.cs` - Pipeline integration

---

## ITransportMetrics.cs

```csharp
public interface ITransportMetrics
{
    void RecordMessagePublished(
        string messageType,
        string destination);
    
    void RecordMessageConsumed(
        string messageType,
        TimeSpan processingDuration,
        MessageProcessingResult result);
    
    void RecordPublishError(
        string messageType,
        string destination,
        Exception exception);
    
    void RecordConsumeError(
        string messageType,
        Exception exception);
}
```

### Usage Example

```csharp
public class MetricsCollectingPublisher(
    ITransportPublisher inner,
    ITransportMetrics metrics) 
    : ITransportPublisher
{
    public async Task PublishAsync(
        ITransportEnvelope envelope,
        IEndpointAddress destination,
        CancellationToken ct)
    {
        try
        {
            await inner.PublishAsync(envelope, destination, ct);
            metrics.RecordMessagePublished(
                envelope.MessageType,
                destination.Address);
        }
        catch (Exception ex)
        {
            metrics.RecordPublishError(
                envelope.MessageType,
                destination.Address,
                ex);
            throw;
        }
    }
}
```

### Recommended Dimensions

Implementations of `ITransportMetrics` should attach a consistent set of tags so metrics can be aggregated across the Grid. Typical tags:

| Tag | Source |
|-----|--------|
| `message_type` | `envelope.MessageType` |
| `destination` | `destination.Address` or endpoint name |
| `transport` | Adapter identifier, e.g. `"servicebus"` or `"storagequeue"` |
| `node_id` | Current `NodeId` from Kernel |
| `environment` | `Environment` from Kernel |
| `tenant_id` | When available from Grid context |
| `project_id` | When available from Grid context |
| `result` | `MessageProcessingResult` (`Success`, `Retry`, `DeadLetter`, `Abandon`) |

You do not need to bake these into the interface. Implementations can pull them from `IGridContext`, `ITransportTopology`, or options.

[↑ Back to top](#table-of-contents)

---

## NoOpTransportMetrics.cs

Default implementation with zero overhead. Registered automatically by `AddHoneyDrunkTransportCore()`.

```csharp
public sealed class NoOpTransportMetrics : ITransportMetrics
{
    public void RecordMessagePublished(string messageType, string destination) { }
    public void RecordMessageConsumed(
        string messageType,
        TimeSpan processingDuration,
        MessageProcessingResult result) { }
    public void RecordPublishError(
        string messageType,
        string destination,
        Exception exception) { }
    public void RecordConsumeError(string messageType, Exception exception) { }
}
```

### Usage Example

```csharp
// Default (no-op, zero overhead)
services.AddHoneyDrunkTransportCore();  // Uses NoOpTransportMetrics

// Swap with OpenTelemetry or Kernel-backed implementation
services.AddSingleton<ITransportMetrics, OpenTelemetryTransportMetrics>();
// or
services.AddSingleton<ITransportMetrics, KernelTransportMetrics>();
```

[↑ Back to top](#table-of-contents)

---

## OpenTelemetryTransportMetrics.cs

A concrete implementation using `IMeterFactory` from .NET's OpenTelemetry integration. This can be copied directly into your node.

```csharp
public sealed class OpenTelemetryTransportMetrics : ITransportMetrics
{
    private readonly Counter<long> _messagesPublished;
    private readonly Counter<long> _messagesConsumed;
    private readonly Histogram<double> _processingDuration;
    private readonly Counter<long> _errors;

    public OpenTelemetryTransportMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("HoneyDrunk.Transport");

        _messagesPublished = meter.CreateCounter<long>(
            "transport.messages.published",
            description: "Number of messages published");

        _messagesConsumed = meter.CreateCounter<long>(
            "transport.messages.consumed",
            description: "Number of messages consumed");

        _processingDuration = meter.CreateHistogram<double>(
            "transport.processing.duration.ms",
            unit: "ms",
            description: "Message processing duration");

        _errors = meter.CreateCounter<long>(
            "transport.errors",
            description: "Number of transport errors");
    }

    public void RecordMessagePublished(string messageType, string destination)
    {
        _messagesPublished.Add(1,
            new KeyValuePair<string, object?>("message_type", messageType),
            new KeyValuePair<string, object?>("destination", destination));
    }

    public void RecordMessageConsumed(
        string messageType,
        TimeSpan processingDuration,
        MessageProcessingResult result)
    {
        _messagesConsumed.Add(1,
            new KeyValuePair<string, object?>("message_type", messageType),
            new KeyValuePair<string, object?>("result", result.ToString()));

        _processingDuration.Record(
            processingDuration.TotalMilliseconds,
            new KeyValuePair<string, object?>("message_type", messageType));
    }

    public void RecordPublishError(string messageType, string destination, Exception exception)
    {
        _errors.Add(1,
            new KeyValuePair<string, object?>("message_type", messageType),
            new KeyValuePair<string, object?>("destination", destination),
            new KeyValuePair<string, object?>("error_type", exception.GetType().Name));
    }

    public void RecordConsumeError(string messageType, Exception exception)
    {
        _errors.Add(1,
            new KeyValuePair<string, object?>("message_type", messageType),
            new KeyValuePair<string, object?>("error_type", exception.GetType().Name));
    }
}
```

### Registration

```csharp
services.AddSingleton<ITransportMetrics, OpenTelemetryTransportMetrics>();
```

[↑ Back to top](#table-of-contents)

---

## KernelTransportMetrics.cs

An implementation that adapts to Kernel's `IMetricsCollector` for centralized metrics aggregation across the Grid.

```csharp
public sealed class KernelTransportMetrics(IMetricsCollector metrics) : ITransportMetrics
{
    public void RecordMessagePublished(string messageType, string destination)
    {
        metrics.Increment(
            "transport.messages.published",
            tags: new()
            {
                ["message_type"] = messageType,
                ["destination"] = destination
            });
    }

    public void RecordMessageConsumed(
        string messageType,
        TimeSpan processingDuration,
        MessageProcessingResult result)
    {
        metrics.Increment(
            "transport.messages.consumed",
            tags: new()
            {
                ["message_type"] = messageType,
                ["result"] = result.ToString()
            });

        metrics.Observe(
            "transport.processing.duration.ms",
            processingDuration.TotalMilliseconds,
            tags: new()
            {
                ["message_type"] = messageType
            });
    }

    public void RecordPublishError(string messageType, string destination, Exception exception)
    {
        metrics.Increment(
            "transport.errors",
            tags: new()
            {
                ["message_type"] = messageType,
                ["destination"] = destination,
                ["error_type"] = exception.GetType().Name
            });
    }

    public void RecordConsumeError(string messageType, Exception exception)
    {
        metrics.Increment(
            "transport.errors",
            tags: new()
            {
                ["message_type"] = messageType,
                ["error_type"] = exception.GetType().Name
            });
    }
}
```

### Registration

```csharp
// Swap default implementation with Kernel-backed metrics
services.AddSingleton<ITransportMetrics, KernelTransportMetrics>();
```

[↑ Back to top](#table-of-contents)

---

## TransportTelemetry.cs

**Location:** `HoneyDrunk.Transport/Telemetry/TransportTelemetry.cs`

OpenTelemetry integration for distributed tracing. The `ActivitySource` name (`"HoneyDrunk.Transport"`) is stable and used for log correlation.

```csharp
public static class TransportTelemetry
{
    public static readonly ActivitySource ActivitySource = 
        new("HoneyDrunk.Transport");
    
    public static Activity? StartPublishActivity(
        ITransportEnvelope envelope,
        IEndpointAddress destination);
    
    public static Activity? StartConsumeActivity(ITransportEnvelope envelope);
    
    public static void RecordOutcome(
        Activity? activity,
        MessageProcessingResult result);
    
    public static void RecordError(Activity? activity, Exception exception);
}
```

> **Note:** In most scenarios you do not call `TransportTelemetry` directly. `TelemetryMiddleware` uses these helpers when `TransportCoreOptions.EnableTelemetry` is `true`. The API remains public to support custom transports and advanced scenarios.

### Usage Example

```csharp
// In publisher (custom transport implementations)
using var activity = TransportTelemetry.StartPublishActivity(envelope, destination);
try
{
    await SendAsync(envelope, destination, ct);
    TransportTelemetry.RecordOutcome(activity, MessageProcessingResult.Success);
}
catch (Exception ex)
{
    TransportTelemetry.RecordError(activity, ex);
    throw;
}

// In consumer (custom transport implementations)
using var activity = TransportTelemetry.StartConsumeActivity(envelope);
try
{
    var result = await ProcessAsync(envelope, ct);
    TransportTelemetry.RecordOutcome(activity, result);
}
catch (Exception ex)
{
    TransportTelemetry.RecordError(activity, ex);
    throw;
}
```

[↑ Back to top](#table-of-contents)

---

[← Back to File Guide](FILE_GUIDE.md) | [↑ Back to top](#table-of-contents)
