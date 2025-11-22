# 📈 Metrics - Telemetry and Observability

[← Back to File Guide](FILE_GUIDE.md)

---

## Overview

Metrics collection abstractions for recording transport operations. Integrates with Kernel IMetricsCollector for centralized metrics aggregation.

**Location:** `HoneyDrunk.Transport/Metrics/`

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

---

## NoOpTransportMetrics.cs

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

// Replace with Kernel-backed implementation
services.AddSingleton<ITransportMetrics, KernelTransportMetrics>();
```

---

## TransportTelemetry.cs

OpenTelemetry integration for distributed tracing.

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

### Usage Example

```csharp
// In publisher
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

// In consumer
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

---

## Metrics Dashboard Example

```csharp
public class TransportMetricsDashboard
{
    private readonly Counter<long> _messagesPublished;
    private readonly Counter<long> _messagesConsumed;
    private readonly Histogram<double> _processingDuration;
    private readonly Counter<long> _errors;
    
    public TransportMetricsDashboard(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("HoneyDrunk.Transport");
        
        _messagesPublished = meter.CreateCounter<long>(
            "transport.messages.published",
            description: "Number of messages published");
        
        _messagesConsumed = meter.CreateCounter<long>(
            "transport.messages.consumed",
            description: "Number of messages consumed");
        
        _processingDuration = meter.CreateHistogram<double>(
            "transport.processing.duration",
            unit: "ms",
            description: "Message processing duration");
        
        _errors = meter.CreateCounter<long>(
            "transport.errors",
            description: "Number of transport errors");
    }
    
    public void RecordPublished(string messageType, string destination)
    {
        _messagesPublished.Add(1,
            new KeyValuePair<string, object?>("message_type", messageType),
            new KeyValuePair<string, object?>("destination", destination));
    }
    
    public void RecordConsumed(
        string messageType,
        TimeSpan duration,
        MessageProcessingResult result)
    {
        _messagesConsumed.Add(1,
            new KeyValuePair<string, object?>("message_type", messageType),
            new KeyValuePair<string, object?>("result", result.ToString()));
        
        _processingDuration.Record(duration.TotalMilliseconds,
            new KeyValuePair<string, object?>("message_type", messageType));
    }
}
```

---

[← Back to File Guide](FILE_GUIDE.md)
