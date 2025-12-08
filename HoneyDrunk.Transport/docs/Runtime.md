# 🏃 Runtime - Lifecycle Orchestration

[← Back to File Guide](FILE_GUIDE.md)

---

## Table of Contents

- [Overview](#overview)
- [ITransportRuntime.cs](#itransportruntimecs)
- [TransportRuntimeHost.cs](#transportruntimehostcs)
- [Lifecycle Management](#lifecycle-management)
- [Health Integration](#health-integration)
- [Advanced Scenarios](#advanced-scenarios)

---

## Overview

The Runtime layer provides unified lifecycle orchestration for all transport consumers. It integrates with ASP.NET Core's hosting model via `IHostedService`, ensuring proper startup and graceful shutdown of message processing across all registered transport adapters.

**Location:** `HoneyDrunk.Transport/Runtime/`

**Key Responsibilities:**
- Coordinate startup of all registered transport consumers
- Manage graceful shutdown allowing in-flight messages to complete
- Expose transport health contributors for aggregation in ASP.NET Core health checks
- Integrate with ASP.NET Core hosting lifecycle via `IHostedService`

The runtime lives inside a HoneyDrunk node. Kernel owns node lifecycle, and `TransportRuntimeHost` plugs into that lifecycle to manage only the messaging side of the node.

---

## ITransportRuntime.cs

Contract for the unified runtime orchestrator.

```csharp
public interface ITransportRuntime
{
    /// <summary>
    /// Gets a value indicating whether the runtime is currently running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Starts all registered transport consumers.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops all transport consumers gracefully, allowing in-flight messages to complete.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all health contributors from registered transports for aggregation.
    /// Typically non-blocking; implementations usually return a cached collection.
    /// </summary>
    Task<IEnumerable<ITransportHealthContributor>> GetHealthContributorsAsync(
        CancellationToken cancellationToken = default);
}
```

### Purpose

Provides a single point of control for **transport consumers** inside a node. Application code can start or stop all consumers, or query their status, without knowing about individual transport adapters.

This interface does not control publishers or other infrastructure. It only manages components that implement `ITransportConsumer`.

### Usage Example

```csharp
public class RuntimeController(ITransportRuntime runtime) : ControllerBase
{
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(new { IsRunning = runtime.IsRunning });
    }
    
    [HttpPost("restart")]
    public async Task<IActionResult> RestartAsync(CancellationToken ct)
    {
        await runtime.StopAsync(ct);
        await runtime.StartAsync(ct);
        return Ok("Restarted");
    }
}
```

[↑ Back to top](#table-of-contents)

---

## TransportRuntimeHost.cs

Default implementation that orchestrates consumer lifecycle as an `IHostedService`.

```csharp
public sealed partial class TransportRuntimeHost(
    IEnumerable<ITransportConsumer> consumers,
    IEnumerable<ITransportHealthContributor> healthContributors,
    ILogger<TransportRuntimeHost> logger) : IHostedService, ITransportRuntime, IDisposable
{
    public bool IsRunning { get; }
    
    public Task StartAsync(CancellationToken cancellationToken = default);
    public Task StopAsync(CancellationToken cancellationToken = default);
    public Task<IEnumerable<ITransportHealthContributor>> GetHealthContributorsAsync(
        CancellationToken cancellationToken = default);
    public void Dispose();
}
```

**Implements:**

- `IHostedService` for ASP.NET Core integration
- `ITransportRuntime` for programmatic control
- `IDisposable` for cleanup of consumer resources

### Key Features

| Feature | Description |
|---------|-------------|
| **Automatic Startup** | Registered as `IHostedService`, starts with application |
| **Parallel Consumer Startup** | Starts all registered `ITransportConsumer` instances concurrently for faster initialization |
| **Graceful Shutdown** | Stops all consumers in parallel, allowing in-flight messages to complete |
| **Thread-Safe** | Uses `SemaphoreSlim` to prevent concurrent start/stop operations |
| **High-Performance Logging** | Uses `LoggerMessage` source generators for zero-allocation logging |
| **Idempotent Operations** | Multiple start/stop calls are safely handled |

### Registration

`TransportRuntimeHost` is registered automatically by `AddHoneyDrunkTransportCore` as both:

- A singleton `ITransportRuntime`
- An `IHostedService` that ASP.NET Core will start and stop with the node

```csharp
// Registered automatically by AddHoneyDrunkTransportCore()
services.AddHoneyDrunkTransportCore(options =>
{
    options.EnableTelemetry = true;
    options.EnableLogging = true;
});

// Conceptual registration (simplified):
// services.TryAddSingleton<ITransportRuntime, TransportRuntimeHost>();
// services.AddHostedService<TransportRuntimeHost>();
```

### How It Works

**Startup Flow:**

```
Application.Run()
    ↓
IHostedService.StartAsync()
    ↓
TransportRuntimeHost.StartAsync()
    ↓
┌─────────────────────────────────────────────┐
│  For each ITransportConsumer (in parallel): │
│    1. Log "Starting consumer {Type}"        │
│    2. consumer.StartAsync()                 │
│    3. Log "Consumer {Type} started"         │
└─────────────────────────────────────────────┘
    ↓
IsRunning = true
```

**Shutdown Flow:**

```
Application Shutdown Signal (SIGTERM, Ctrl+C)
    ↓
IHostedService.StopAsync()
    ↓
TransportRuntimeHost.StopAsync()
    ↓
┌─────────────────────────────────────────────┐
│  For each ITransportConsumer (in parallel): │
│    1. Log "Stopping consumer {Type}"        │
│    2. consumer.StopAsync()                  │
│    3. Log "Consumer {Type} stopped"         │
└─────────────────────────────────────────────┘
    ↓
IsRunning = false
```

### Error Handling

- **Startup failures**: If any consumer fails to start, the exception propagates and prevents application startup
- **Shutdown failures**: Individual consumer stop failures are logged but don't prevent other consumers from stopping

```csharp
// Startup - fail fast
private async Task StartConsumerAsync(ITransportConsumer consumer, CancellationToken ct)
{
    try
    {
        await consumer.StartAsync(ct);
    }
    catch (Exception ex)
    {
        LogConsumerStartFailed(logger, consumer.GetType().Name, ex);
        throw; // Propagate - prevents application startup
    }
}

// Shutdown - best effort
private async Task StopConsumerAsync(ITransportConsumer consumer, CancellationToken ct)
{
    try
    {
        await consumer.StopAsync(ct);
    }
    catch (Exception ex)
    {
        LogConsumerStopError(logger, consumer.GetType().Name, ex);
        // Don't rethrow - allow other consumers to stop
    }
}
```

[↑ Back to top](#table-of-contents)

---

## Lifecycle Management

### Typical Application Flow

```csharp
var builder = WebApplication.CreateBuilder(args);

// 1. Register Kernel (required before Transport)
builder.Services.AddHoneyDrunkCoreNode(nodeDescriptor);

// 2. Register Transport with transport adapter
builder.Services.AddHoneyDrunkTransportCore(options =>
{
    options.EnableTelemetry = true;
    options.EnableLogging = true;
})
.AddHoneyDrunkServiceBusTransport(options =>
{
    options.ConnectionString = "...";
});

// 3. Register message handlers
builder.Services.AddMessageHandler<OrderCreated, OrderCreatedHandler>();

var app = builder.Build();

// 4. TransportRuntimeHost starts automatically when app.Run() is called
app.Run();
```

In normal applications you do not call `ITransportRuntime` directly. The ASP.NET Core host will call `StartAsync` and `StopAsync` on `TransportRuntimeHost` when `app.Run()` is invoked and when the node shuts down.

### Manual Lifecycle Control

For scenarios requiring programmatic control over the runtime:

```csharp
public class MaintenanceService(ITransportRuntime runtime, ILogger<MaintenanceService> logger)
{
    public async Task EnterMaintenanceModeAsync(CancellationToken ct)
    {
        if (runtime.IsRunning)
        {
            logger.LogInformation("Entering maintenance mode, stopping consumers...");
            await runtime.StopAsync(ct);
            logger.LogInformation("Consumers stopped, maintenance mode active");
        }
    }
    
    public async Task ExitMaintenanceModeAsync(CancellationToken ct)
    {
        if (!runtime.IsRunning)
        {
            logger.LogInformation("Exiting maintenance mode, starting consumers...");
            await runtime.StartAsync(ct);
            logger.LogInformation("Consumers started, normal operation resumed");
        }
    }
}
```

### Graceful Shutdown Configuration

Configure shutdown timeout in `appsettings.json`:

```json
{
  "HostOptions": {
    "ShutdownTimeout": "00:00:30"
  }
}
```

Or programmatically:

```csharp
builder.Services.Configure<HostOptions>(options =>
{
    options.ShutdownTimeout = TimeSpan.FromSeconds(30);
});
```

[↑ Back to top](#table-of-contents)

---

## Health Integration

### Aggregating Health Contributors

The runtime collects health contributors from all registered transports:

> See [Health](Health.md) for the definition of `ITransportHealthContributor` and common contributors like `OutboxHealthContributor` and `PublisherHealthContributor`.

```csharp
public class TransportHealthCheck(ITransportRuntime runtime) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken ct = default)
    {
        if (!runtime.IsRunning)
        {
            return HealthCheckResult.Unhealthy("Transport runtime is not running");
        }
        
        // Each contributor is an ITransportHealthContributor from the Health folder
        var contributors = await runtime.GetHealthContributorsAsync(ct);
        var results = new Dictionary<string, object>();
        var worstStatus = HealthStatus.Healthy;
        
        foreach (var contributor in contributors)
        {
            var result = await contributor.CheckHealthAsync(ct);
            results[contributor.Name] = result.Status.ToString();
            
            if (result.Status == HealthStatus.Unhealthy)
                worstStatus = HealthStatus.Unhealthy;
            else if (result.Status == HealthStatus.Degraded && worstStatus != HealthStatus.Unhealthy)
                worstStatus = HealthStatus.Degraded;
        }
        
        return new HealthCheckResult(worstStatus, data: results);
    }
}
```

### Kubernetes Probes

```csharp
// Register health checks
builder.Services.AddHealthChecks()
    .AddCheck<TransportHealthCheck>("transport");

var app = builder.Build();

// Map health endpoints
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});
```

[↑ Back to top](#table-of-contents)

---

## Advanced Scenarios

### Multiple Transport Adapters

The runtime automatically discovers and manages all registered consumers:

```csharp
builder.Services.AddHoneyDrunkTransportCore(options => { })
    // Register multiple transport adapters
    .AddHoneyDrunkServiceBusTransport(sb =>
    {
        sb.ConnectionString = "...";
        sb.TopicName = "high-priority-events";
    })
    .AddHoneyDrunkStorageQueueTransport(sq =>
    {
        sq.ConnectionString = "...";
        sq.QueueName = "batch-processing";
    });

// TransportRuntimeHost will start both consumers and include both in health aggregation.
// It does not care whether they came from Service Bus, Storage Queue, or InMemory.
```

### Testing with InMemory Transport

```csharp
[Fact]
public async Task RuntimeStartsAndStopsConsumers()
{
    var services = new ServiceCollection();
    
    services.AddLogging();
    services.AddHoneyDrunkTransportCore(options => { })
        .AddHoneyDrunkInMemoryTransport();
    
    var provider = services.BuildServiceProvider();
    var runtime = provider.GetRequiredService<ITransportRuntime>();
    
    // Initially not running
    Assert.False(runtime.IsRunning);
    
    // Start
    await runtime.StartAsync();
    Assert.True(runtime.IsRunning);
    
    // Stop
    await runtime.StopAsync();
    Assert.False(runtime.IsRunning);
}
```

### Custom Consumer Implementation

When implementing a custom transport, ensure your consumer integrates with the runtime:

```csharp
public sealed class CustomTransportConsumer : ITransportConsumer, IAsyncDisposable
{
    private CancellationTokenSource? _cts;
    private Task? _processingTask;
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _processingTask = ProcessMessagesAsync(_cts.Token);
    }
    
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts is null)
        {
            return;
        }
        
        await _cts.CancelAsync();
        
        if (_processingTask is not null)
        {
            // Wait until either processing finishes or the host's shutdown timeout expires
            await Task.WhenAny(
                _processingTask,
                Task.Delay(Timeout.Infinite, cancellationToken));
        }
    }
    
    public async ValueTask DisposeAsync()
    {
        _cts?.Dispose();
    }
    
    private async Task ProcessMessagesAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            // Process messages...
        }
    }
}
```

Custom consumers must obey the cancellation token and return promptly from `StopAsync`. `TransportRuntimeHost` relies on this to honor the node's configured `ShutdownTimeout`.
```

[↑ Back to top](#table-of-contents)

---

[← Back to File Guide](FILE_GUIDE.md) | [↑ Back to top](#table-of-contents)
