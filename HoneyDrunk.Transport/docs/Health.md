# ❤️ Health - Monitoring and Health Checks

[← Back to File Guide](FILE_GUIDE.md)

---

## Table of Contents

- [Overview](#overview)
- [ITransportHealthContributor.cs](#itransporthealthcontributorcs)
- [TransportHealthResult.cs](#transporthealthresultcs)
- [PublisherHealthContributor.cs](#publisherhealthcontributorcs)
- [OutboxHealthContributor.cs](#outboxhealthcontributorcs)

---

## Overview

Health monitoring abstractions for transport components. Integrates with Kernel health aggregation for Kubernetes readiness/liveness probes.

**Location:** `HoneyDrunk.Transport/Health/`

Transport health contributors are collected by `ITransportRuntime` and can be included in the node's composite health check using Kernel's contributor aggregation model.

A transport adapter may expose multiple health contributors (for example publisher, consumer, and outbox). `TransportRuntimeHost` simply aggregates whatever contributors are registered in DI.

---

## ITransportHealthContributor.cs

```csharp
public interface ITransportHealthContributor
{
    string Name { get; }
    Task<TransportHealthResult> CheckHealthAsync(
        CancellationToken cancellationToken = default);
}
```

### Usage Example

```csharp
public sealed class ServiceBusHealthContributor(ServiceBusClient client) 
    : ITransportHealthContributor
{
    public string Name => "Transport.ServiceBus";
    
    public async Task<TransportHealthResult> CheckHealthAsync(CancellationToken ct)
    {
        try
        {
            // Check connectivity
            await client.AcceptNextSessionAsync(cancellationToken: ct);
            return TransportHealthResult.Healthy("Connected");
        }
        catch (Exception ex)
        {
            return TransportHealthResult.Unhealthy(
                "Service Bus unreachable",
                ex);
        }
    }
}

// Register
services.AddSingleton<ITransportHealthContributor, ServiceBusHealthContributor>();
```

[↑ Back to top](#table-of-contents)

---

## TransportHealthResult.cs

```csharp
public sealed class TransportHealthResult
{
    public HealthStatus Status { get; }
    public string? Description { get; }
    public Exception? Exception { get; }
    public IReadOnlyDictionary<string, object>? Metadata { get; }
    
    public static TransportHealthResult Healthy(
        string? description = null,
        IReadOnlyDictionary<string, object>? metadata = null);
    
    public static TransportHealthResult Degraded(
        string? description = null,
        IReadOnlyDictionary<string, object>? metadata = null);
    
    public static TransportHealthResult Unhealthy(
        string? description = null,
        Exception? exception = null,
        IReadOnlyDictionary<string, object>? metadata = null);
}
```

### Usage Example

```csharp
// Healthy state
return TransportHealthResult.Healthy("All systems operational");

// Degraded (warning)
return TransportHealthResult.Degraded(
    "High message backlog",
    new Dictionary<string, object>
    {
        ["QueueDepth"] = 10000,
        ["WarningThreshold"] = 5000
    });

// Unhealthy (error)
return TransportHealthResult.Unhealthy(
    "Cannot connect to message broker",
    connectionException);
```

[↑ Back to top](#table-of-contents)

---

## PublisherHealthContributor.cs

This contributor does *not* publish a real message. It performs a lightweight transport-specific connectivity probe such as opening a management connection or pinging the namespace.

```csharp
public sealed class PublisherHealthContributor(ITransportPublisher publisher) 
    : ITransportHealthContributor
{
    public string Name => "Transport.Publisher";
    
    public async Task<TransportHealthResult> CheckHealthAsync(CancellationToken ct)
    {
        try
        {
            // Lightweight connectivity probe (transport-specific)
            // Does NOT publish a real message
            return TransportHealthResult.Healthy("Publisher operational");
        }
        catch (Exception ex)
        {
            return TransportHealthResult.Unhealthy(
                "Publisher unavailable",
                ex);
        }
    }
}
```

[↑ Back to top](#table-of-contents)

---

## OutboxHealthContributor.cs

Thresholds should be tuned per environment. In high-throughput nodes, consider bumping `CriticalThreshold` significantly higher.

```csharp
public sealed class OutboxHealthContributor(IOutboxStore store) 
    : ITransportHealthContributor
{
    private const int WarningThreshold = 1000;
    private const int CriticalThreshold = 10000;
    
    public string Name => "Transport.Outbox";
    
    public async Task<TransportHealthResult> CheckHealthAsync(CancellationToken ct)
    {
        try
        {
            var pending = await store.LoadPendingAsync(
                CriticalThreshold + 1,
                ct);
            
            var count = pending.Count();
            var metadata = new Dictionary<string, object>
            {
                ["PendingCount"] = count,
                ["WarningThreshold"] = WarningThreshold,
                ["CriticalThreshold"] = CriticalThreshold
            };
            
            if (count >= CriticalThreshold)
            {
                return TransportHealthResult.Unhealthy(
                    $"Outbox backlog critical: {count} pending messages",
                    metadata: metadata);
            }
            
            if (count >= WarningThreshold)
            {
                return TransportHealthResult.Degraded(
                    $"Outbox backlog elevated: {count} pending messages",
                    metadata: metadata);
            }
            
            return TransportHealthResult.Healthy(
                $"Outbox operational: {count} pending messages",
                metadata: metadata);
        }
        catch (Exception ex)
        {
            return TransportHealthResult.Unhealthy(
                "Outbox health check failed",
                ex);
        }
    }
}
```

### Usage Example

```csharp
// Outbox monitoring dashboard
public class OutboxMonitor(IEnumerable<ITransportHealthContributor> contributors)
{
    public async Task<Dictionary<string, TransportHealthResult>> CheckAllAsync(
        CancellationToken ct)
    {
        var results = new Dictionary<string, TransportHealthResult>();
        
        foreach (var contributor in contributors)
        {
            var result = await contributor.CheckHealthAsync(ct);
            results[contributor.Name] = result;
        }
        
        return results;
    }
}
```

[↑ Back to top](#table-of-contents)

---

## Summary

Transport health checks provide visibility into broker reachability, publisher readiness, and outbox durability. The `TransportRuntimeHost` aggregates all registered contributors, and Kernel surfaces them through node-level readiness and liveness probes. This separation ensures transport-specific failures never compromise the structure of the node itself.

---

[← Back to File Guide](FILE_GUIDE.md) | [↑ Back to top](#table-of-contents)
