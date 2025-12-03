# ⚙️ Configuration - Settings and Options

[← Back to File Guide](FILE_GUIDE.md)

---

## Table of Contents

- [Overview](#overview)
- **Core Transport** (`HoneyDrunk.Transport/Configuration/`)
  - [TransportCoreOptions.cs](#transportcoreoptionscs)
  - [TransportOptions.cs](#transportoptionscs)
  - [RetryOptions.cs](#retryoptionscs)
  - [BackoffStrategy.cs](#backoffstrategycs)
  - [IErrorHandlingStrategy.cs](#ierrorhandlingstrategycs)
  - [ErrorHandlingAction.cs](#errorhandlingactioncs)
  - [ErrorHandlingDecision.cs](#errorhandlingdecisioncs)
  - [DefaultErrorHandlingStrategy.cs](#defaulterrorhandlingstrategycs)
  - [ConfigurableErrorHandlingStrategy.cs](#configurableerrorhandlingstrategycs)
  - [ExceptionTypeMapStrategy.cs](#exceptiontypemapstrategycs)
- **Azure Service Bus** (`HoneyDrunk.Transport.AzureServiceBus/Configuration/`)
  - [AzureServiceBusOptions.cs](#azureservicebusoptionscs)
  - [ServiceBusRetryOptions.cs](#servicebusretryoptionscs)
  - [ServiceBusRetryMode.cs](#servicebusretrymodecs)
  - [ServiceBusEntityType.cs](#servicebusentitytypecs)
  - [BlobFallbackOptions.cs](#blobfallbackoptionscs)
- **Azure Storage Queue** (`HoneyDrunk.Transport.StorageQueue/Configuration/`)
  - [StorageQueueOptions.cs](#storagequeueoptions)

---

## Overview

Configuration classes for controlling transport behavior including retry policies, backoff strategies, error handling, and transport-specific options.

**Locations:**

- Core: `HoneyDrunk.Transport/Configuration/`
- Azure Service Bus: `HoneyDrunk.Transport.AzureServiceBus/Configuration/`
- Azure Storage Queue: `HoneyDrunk.Transport.StorageQueue/Configuration/`

---

# Core Transport Configuration

> **Mental Model:** Core options (`TransportCoreOptions`) control cross-cutting behavior of the transport pipeline (logging, telemetry, correlation). Adapter options (`AzureServiceBusOptions`, `StorageQueueOptions`) control connection, concurrency, and broker-specific behavior.

---

## TransportCoreOptions.cs

Cross-cutting behavioral flags for the transport infrastructure.

```csharp
public sealed class TransportCoreOptions
{
    public bool EnableTelemetry { get; set; } = false;
    public bool EnableLogging { get; set; } = false;
    public bool EnableCorrelation { get; set; } = false;
}
```

### Usage Example

```csharp
services.AddHoneyDrunkTransportCore(options =>
{
    options.EnableTelemetry = true;   // OpenTelemetry integration
    options.EnableLogging = true;     // Structured logging
    options.EnableCorrelation = true; // Correlation ID propagation
});
```

[↑ Back to top](#table-of-contents)

---

## TransportOptions.cs

Base configuration for any transport endpoint. Transport-specific options (Azure Service Bus, Storage Queue) extend this class.

```csharp
public class TransportOptions
{
    public string EndpointName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public int MaxConcurrency { get; set; } = 1;
    public int PrefetchCount { get; set; }
    public RetryOptions Retry { get; set; } = new();
    public TimeSpan MessageLockDuration { get; set; } = TimeSpan.FromMinutes(5);
    public bool AutoComplete { get; set; } = true;
    public bool SessionEnabled { get; set; }
    public Dictionary<string, string> Properties { get; } = [];
}
```

### Property Details

| Property | Description |
|----------|-------------|
| `EndpointName` | Logical name for the endpoint (used in logging/telemetry) |
| `Address` | Physical address (queue name, topic name, etc.) |
| `MaxConcurrency` | Maximum concurrent message processors |
| `PrefetchCount` | Prefetch count for message retrieval optimization |
| `Retry` | Nested retry configuration |
| `MessageLockDuration` | How long the message is locked/invisible during processing |
| `AutoComplete` | Whether to automatically complete messages on success |
| `SessionEnabled` | Whether to use sessions for ordered processing |
| `Properties` | Additional transport-specific key-value properties |

### Usage Example

```csharp
// TransportOptions is typically configured through transport-specific options
services.AddHoneyDrunkServiceBusTransport(options =>
{
    // Base TransportOptions properties
    options.EndpointName = "OrderProcessor";
    options.Address = "orders-queue";
    options.MaxConcurrency = 10;
    options.PrefetchCount = 20;
    options.MessageLockDuration = TimeSpan.FromMinutes(3);
    options.AutoComplete = true;
    options.SessionEnabled = false;
    
    // Retry configuration
    options.Retry.MaxAttempts = 3;
    options.Retry.BackoffStrategy = BackoffStrategy.Exponential;
});
```

[↑ Back to top](#table-of-contents)

---

## RetryOptions.cs

Application-level retry configuration for the transport pipeline.

> **Note:** `RetryOptions` controls application-level retry behavior in the transport pipeline. `ServiceBusRetryOptions` controls SDK-level retries for Azure Service Bus operations. These are separate concerns.

```csharp
public sealed class RetryOptions
{
    public int MaxAttempts { get; set; } = 3;
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromMinutes(1);
    public BackoffStrategy BackoffStrategy { get; set; } = BackoffStrategy.Exponential;
}
```

### Usage Example

```csharp
services.AddHoneyDrunkTransportCore()
    .AddHoneyDrunkServiceBusTransport(/* ... */)
    .WithRetry(retry =>
    {
        retry.MaxAttempts = 5;
        retry.InitialDelay = TimeSpan.FromSeconds(2);
        retry.BackoffStrategy = BackoffStrategy.Exponential;
        retry.MaxDelay = TimeSpan.FromMinutes(2);
    });
```

[↑ Back to top](#table-of-contents)

---

## BackoffStrategy.cs

```csharp
public enum BackoffStrategy
{
    Fixed,        // Same delay each retry (1s, 1s, 1s)
    Linear,       // Increasing linearly (1s, 2s, 3s)
    Exponential   // Exponential growth (1s, 2s, 4s, 8s)
}
```

### Usage Example

```csharp
// Fixed: 1s, 1s, 1s, 1s
retry.BackoffStrategy = BackoffStrategy.Fixed;

// Linear: 1s, 2s, 3s, 4s
retry.BackoffStrategy = BackoffStrategy.Linear;

// Exponential: 1s, 2s, 4s, 8s (best for production)
retry.BackoffStrategy = BackoffStrategy.Exponential;
```

[↑ Back to top](#table-of-contents)

---

## IErrorHandlingStrategy.cs

Contract for error handling strategies that decide how to handle message processing failures.

> **Design Note:** Error handling strategies receive only the exception and delivery count—they do not see the full `MessageContext`. This keeps strategies focused on exception classification and retry policy, not message content.

```csharp
public interface IErrorHandlingStrategy
{
    Task<ErrorHandlingDecision> HandleErrorAsync(
        Exception exception,
        int deliveryCount,
        CancellationToken cancellationToken = default);
}
```

### Method Parameters

| Parameter | Description |
|-----------|-------------|
| `exception` | The exception that occurred during processing |
| `deliveryCount` | How many times this message has been delivered/retried |
| `cancellationToken` | Cancellation token for the operation |

### Implementation Notes

- Must be thread-safe (multiple messages may fail concurrently)
- Should return quickly (avoid blocking I/O in decision logic)
- Use `deliveryCount` to implement retry limits
- Return `ErrorHandlingDecision.DeadLetter()` for permanent failures

### Usage Example

```csharp
public class CustomErrorStrategy : IErrorHandlingStrategy
{
    public Task<ErrorHandlingDecision> HandleErrorAsync(
        Exception exception,
        int deliveryCount,
        CancellationToken cancellationToken = default)
    {
        // Permanent failure - dead letter immediately
        if (exception is ValidationException)
        {
            return Task.FromResult(ErrorHandlingDecision.DeadLetter("Invalid message"));
        }
        
        // Max retries exceeded
        if (deliveryCount >= 5)
        {
            return Task.FromResult(ErrorHandlingDecision.DeadLetter("Max retries exceeded"));
        }
        
        // Transient failure - retry with backoff
        var delay = TimeSpan.FromSeconds(Math.Pow(2, deliveryCount - 1));
        return Task.FromResult(ErrorHandlingDecision.Retry(delay));
    }
}

services.AddSingleton<IErrorHandlingStrategy, CustomErrorStrategy>();
```

[↑ Back to top](#table-of-contents)

---

## ErrorHandlingAction.cs

Enum defining the possible actions when handling a message processing error.

```csharp
public enum ErrorHandlingAction
{
    Retry,        // Retry with delay
    DeadLetter,   // Move to DLQ
    Abandon       // Return to queue (no delay)
}
```

### When to Use Each

| Action | Use When |
|--------|----------|
| `Retry` | Transient failures (network, timeout, rate limiting) |
| `DeadLetter` | Permanent failures (validation, bad data, business rule violations) |
| `Abandon` | Return the message to the queue immediately without applying backoff. Use rarely; most scenarios should prefer `Retry` with a delay. |

[↑ Back to top](#table-of-contents)

---

## ErrorHandlingDecision.cs

Immutable record representing the outcome of an error handling decision, including the action to take and optional metadata.

> **Pipeline Integration:** The message pipeline converts `ErrorHandlingDecision.Action` into a `MessageProcessingResult` that the transport adapter uses to complete, abandon, retry, or dead-letter the message. Handlers return `MessageProcessingResult` directly; `ErrorHandlingDecision` is used by `IErrorHandlingStrategy` implementations.

```csharp
public sealed record ErrorHandlingDecision
{
    public ErrorHandlingAction Action { get; }
    public TimeSpan? RetryDelay { get; }
    public string? Reason { get; }
    
    // Factory methods
    public static ErrorHandlingDecision Retry(TimeSpan delay, string? reason = null);
    public static ErrorHandlingDecision DeadLetter(string? reason = null);
    public static ErrorHandlingDecision Abandon(string? reason = null);
}
```

### Factory Methods

| Method | Description |
|--------|-------------|
| `Retry(delay, reason)` | Retry after the specified delay |
| `DeadLetter(reason)` | Move to dead-letter queue with reason |
| `Abandon(reason)` | Return to queue for immediate retry |

### Usage Example

```csharp
// Retry with exponential backoff
ErrorHandlingDecision.Retry(
    TimeSpan.FromSeconds(Math.Pow(2, deliveryCount)),
    "Transient network failure");

// Dead letter with diagnostic reason
ErrorHandlingDecision.DeadLetter("Message failed schema validation");

// Abandon (use rarely)
ErrorHandlingDecision.Abandon("Temporary resource lock contention");
```

[↑ Back to top](#table-of-contents)

---

## DefaultErrorHandlingStrategy.cs

Production-ready error handling strategy with built-in classification rules. Automatically classifies exceptions as transient or permanent and applies appropriate backoff.

```csharp
public sealed class DefaultErrorHandlingStrategy : IErrorHandlingStrategy
{
    public DefaultErrorHandlingStrategy(ILogger<DefaultErrorHandlingStrategy> logger);
    
    public Task<ErrorHandlingDecision> HandleErrorAsync(
        Exception exception,
        int deliveryCount,
        CancellationToken cancellationToken = default);
}
```

### Classification Rules

| Classification | Exception Types | Behavior |
|----------------|-----------------|----------|
| **Transient** | `TimeoutException`, `TaskCanceledException`, `OperationCanceledException`, `IOException`, `SocketException` | Retry with exponential backoff (1s → 30s max) |
| **Permanent** | `ArgumentException`, `InvalidOperationException`, `NotImplementedException` | Dead-letter immediately |
| **Unknown** | All others | Retry with linear backoff, dead-letter after 5 attempts |

### Usage Example

```csharp
// Register as the default strategy
services.AddSingleton<IErrorHandlingStrategy, DefaultErrorHandlingStrategy>();
```

[↑ Back to top](#table-of-contents)

---

## ConfigurableErrorHandlingStrategy.cs

Fluent API for building custom error handling rules. Allows mapping specific exception types to retry or dead-letter actions.

```csharp
public sealed class ConfigurableErrorHandlingStrategy : IErrorHandlingStrategy
{
    public ConfigurableErrorHandlingStrategy(int maxDeliveryCount = 5);
    
    // Fluent configuration
    public ConfigurableErrorHandlingStrategy RetryOn<TException>(TimeSpan? retryDelay = null)
        where TException : Exception;
    
    public ConfigurableErrorHandlingStrategy DeadLetterOn<TException>()
        where TException : Exception;
    
    public Task<ErrorHandlingDecision> HandleErrorAsync(
        Exception exception,
        int deliveryCount,
        CancellationToken cancellationToken = default);
}
```

### Behavior

1. If `deliveryCount >= maxDeliveryCount`, always dead-letter
2. Check exact exception type match first
3. Check base exception type match
4. Default: retry unknown exceptions with exponential backoff

### Usage Example

```csharp
var strategy = new ConfigurableErrorHandlingStrategy(maxDeliveryCount: 5)
    .RetryOn<TimeoutException>(TimeSpan.FromSeconds(5))
    .RetryOn<HttpRequestException>(TimeSpan.FromSeconds(10))
    .DeadLetterOn<ValidationException>()
    .DeadLetterOn<ArgumentNullException>();

services.AddSingleton<IErrorHandlingStrategy>(strategy);
```

[↑ Back to top](#table-of-contents)

---

## ExceptionTypeMapStrategy.cs

Dictionary-based error handling strategy with builder pattern. Maps exception types to `ErrorHandlingAction` values.

```csharp
public sealed class ExceptionTypeMapStrategy : IErrorHandlingStrategy
{
    public ExceptionTypeMapStrategy(
        Dictionary<Type, ErrorHandlingAction> typeMap,
        int maxDeliveryCount = 5,
        ErrorHandlingAction defaultAction = ErrorHandlingAction.Retry);
    
    public static Builder CreateBuilder();
    
    public Task<ErrorHandlingDecision> HandleErrorAsync(
        Exception exception,
        int deliveryCount,
        CancellationToken cancellationToken = default);
    
    // Nested builder class
    public sealed class Builder
    {
        public Builder MapToRetry<TException>() where TException : Exception;
        public Builder MapToDeadLetter<TException>() where TException : Exception;
        public Builder WithMaxDeliveryCount(int maxDeliveryCount);
        public Builder WithDefaultAction(ErrorHandlingAction defaultAction);
        public ExceptionTypeMapStrategy Build();
    }
}
```

### Usage Example

```csharp
var strategy = ExceptionTypeMapStrategy.CreateBuilder()
    .MapToRetry<TimeoutException>()
    .MapToRetry<HttpRequestException>()
    .MapToDeadLetter<ValidationException>()
    .MapToDeadLetter<ArgumentException>()
    .WithMaxDeliveryCount(10)
    .WithDefaultAction(ErrorHandlingAction.Retry)
    .Build();

services.AddSingleton<IErrorHandlingStrategy>(strategy);
```

[↑ Back to top](#table-of-contents)

---

# Azure Service Bus Configuration

---

## AzureServiceBusOptions.cs

Configuration options for Azure Service Bus transport. Extends `TransportOptions` with Service Bus-specific settings.

```csharp
public sealed class AzureServiceBusOptions : TransportOptions
{
    public string FullyQualifiedNamespace { get; set; } = string.Empty;
    public string? ConnectionString { get; set; }
    public ServiceBusEntityType EntityType { get; set; } = ServiceBusEntityType.Queue;
    public string? SubscriptionName { get; set; }
    public TimeSpan MaxWaitTime { get; set; } = TimeSpan.FromSeconds(60);
    public ServiceBusRetryOptions ServiceBusRetry { get; set; } = new();
    public bool EnableDeadLetterQueue { get; set; } = true;
    public int MaxDeliveryCount { get; set; } = 10;
    public BlobFallbackOptions BlobFallback { get; set; } = new();
}
```

### Authentication Options

| Option | Description |
|--------|-------------|
| `FullyQualifiedNamespace` | For managed identity (e.g., `mynamespace.servicebus.windows.net`) |
| `ConnectionString` | Alternative to managed identity |

### Usage Example

```csharp
// Using managed identity (recommended for production)
services.AddHoneyDrunkServiceBusTransport(options =>
{
    options.FullyQualifiedNamespace = "mynamespace.servicebus.windows.net";
    options.Address = "orders-queue";
    options.EntityType = ServiceBusEntityType.Queue;
    options.MaxConcurrency = 10;
    options.MaxDeliveryCount = 5;
    options.MaxWaitTime = TimeSpan.FromSeconds(30);
});

// Using Topic/Subscription
services.AddHoneyDrunkServiceBusTransport(options =>
{
    options.FullyQualifiedNamespace = "mynamespace.servicebus.windows.net";
    options.Address = "orders-topic";
    options.EntityType = ServiceBusEntityType.Topic;
    options.SubscriptionName = "order-processor";
});
```

[↑ Back to top](#table-of-contents)

---

## ServiceBusRetryOptions.cs

Service Bus-specific retry configuration for SDK-level operations.

```csharp
public sealed class ServiceBusRetryOptions
{
    public ServiceBusRetryMode Mode { get; set; } = ServiceBusRetryMode.Exponential;
    public int MaxRetries { get; set; } = 3;
    public TimeSpan Delay { get; set; } = TimeSpan.FromSeconds(0.8);
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromMinutes(1);
    public TimeSpan TryTimeout { get; set; } = TimeSpan.FromMinutes(1);
}
```

### Property Details

| Property | Description |
|----------|-------------|
| `Mode` | `Fixed` or `Exponential` retry mode |
| `MaxRetries` | Maximum number of SDK-level retry attempts |
| `Delay` | Initial delay between retries |
| `MaxDelay` | Maximum delay between retries |
| `TryTimeout` | Timeout for each SDK operation attempt |

### Usage Example

```csharp
services.AddHoneyDrunkServiceBusTransport(options =>
{
    options.ServiceBusRetry.Mode = ServiceBusRetryMode.Exponential;
    options.ServiceBusRetry.MaxRetries = 5;
    options.ServiceBusRetry.Delay = TimeSpan.FromSeconds(1);
    options.ServiceBusRetry.MaxDelay = TimeSpan.FromSeconds(30);
    options.ServiceBusRetry.TryTimeout = TimeSpan.FromSeconds(60);
});
```

[↑ Back to top](#table-of-contents)

---

## ServiceBusRetryMode.cs

Retry mode for Service Bus SDK operations.

```csharp
public enum ServiceBusRetryMode
{
    Fixed,       // Fixed delay between retries
    Exponential  // Exponential backoff between retries
}
```

[↑ Back to top](#table-of-contents)

---

## ServiceBusEntityType.cs

Entity type for Azure Service Bus messaging.

```csharp
public enum ServiceBusEntityType
{
    Queue,  // Point-to-point messaging
    Topic   // Publish-subscribe messaging
}
```

### When to Use Each

| Type | Use Case |
|------|----------|
| `Queue` | Single consumer, guaranteed delivery, work distribution |
| `Topic` | Multiple subscribers, event broadcasting, fan-out patterns |

[↑ Back to top](#table-of-contents)

---

## BlobFallbackOptions.cs

Options for Blob Storage fallback when publishing to Service Bus fails. Enables durability by persisting failed messages to Blob Storage for later replay.

> **When Used:** Blob fallback is used by the Service Bus publisher when messages exceed size limits or when transient publish failures exceed the configured retry policy. This is a *publish-side* durability mechanism, not a consume-side feature.

```csharp
public sealed class BlobFallbackOptions
{
    public bool Enabled { get; set; }
    public string? ConnectionString { get; set; }
    public string? AccountUrl { get; set; }
    public string ContainerName { get; set; } = "transport-fallback";
    public string? BlobPrefix { get; set; }
    public TimeSpan? TimeToLive { get; set; }
}
```

### Property Details

| Property | Description |
|----------|-------------|
| `Enabled` | Whether Blob fallback is enabled |
| `ConnectionString` | Blob Storage connection string |
| `AccountUrl` | Blob service URL for managed identity (e.g., `https://account.blob.core.windows.net`) |
| `ContainerName` | Container for storing fallback messages |
| `BlobPrefix` | Optional folder prefix for organizing blobs |
| `TimeToLive` | Informational TTL (external lifecycle policy use) |

### Usage Example

```csharp
services.AddHoneyDrunkServiceBusTransport(options =>
{
    options.FullyQualifiedNamespace = "mynamespace.servicebus.windows.net";
    options.Address = "critical-messages";
    
    // Enable Blob fallback for durability
    options.BlobFallback.Enabled = true;
    options.BlobFallback.AccountUrl = "https://myaccount.blob.core.windows.net";
    options.BlobFallback.ContainerName = "servicebus-fallback";
    options.BlobFallback.BlobPrefix = "failed-messages/";
    options.BlobFallback.TimeToLive = TimeSpan.FromDays(7);
});
```

[↑ Back to top](#table-of-contents)

---

# Azure Storage Queue Configuration

---

## StorageQueueOptions.cs

Configuration options for Azure Storage Queue transport. Extends `TransportOptions` with Storage Queue-specific settings and implements `IValidatableObject` for configuration validation.

```csharp
public sealed class StorageQueueOptions : TransportOptions, IValidatableObject
{
    // Authentication
    public string? ConnectionString { get; set; }
    public Uri? AccountEndpoint { get; set; }
    
    // Queue settings
    [Required] public string QueueName { get; set; } = string.Empty;
    public bool CreateIfNotExists { get; set; } = true;
    public string? PoisonQueueName { get; set; }
    
    // Message settings
    public TimeSpan? MessageTimeToLive { get; set; }
    public TimeSpan VisibilityTimeout { get; set; } = TimeSpan.FromSeconds(30);
    [Range(1, 100)] public int MaxDequeueCount { get; set; } = 5;
    
    // Performance tuning
    [Range(1, 32)] public int PrefetchMaxMessages { get; set; } = 16;
    [Range(1, 128)] public int? MaxBatchPublishConcurrency { get; set; }
    [Range(1, 32)] public int BatchProcessingConcurrency { get; set; } = 1;
    
    // Polling
    public TimeSpan EmptyQueuePollingInterval { get; set; } = TimeSpan.FromSeconds(1);
    public TimeSpan MaxPollingInterval { get; set; } = TimeSpan.FromSeconds(5);
    
    // Observability
    public bool EnableTelemetry { get; set; } = true;
    public bool EnableLogging { get; set; } = true;
    public bool EnableCorrelation { get; set; } = true;
    
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext);
}
```

### Concurrency Model

Storage Queue uses a two-level concurrency model:

- **MaxConcurrency**: Number of concurrent fetch loops (inherited from `TransportOptions`)
- **BatchProcessingConcurrency**: Concurrent messages processed per fetch loop

> **Effective concurrency = MaxConcurrency × BatchProcessingConcurrency**

**Example:** `MaxConcurrency=5, BatchProcessingConcurrency=4` = 20 concurrent messages.

### Validation Rules

The `Validate` method enforces:

- Either `ConnectionString` or `AccountEndpoint` must be set
- `MaxConcurrency` ≤ 100
- `BatchProcessingConcurrency` ≤ `PrefetchMaxMessages`
- `EmptyQueuePollingInterval` ≤ `MaxPollingInterval`
- `VisibilityTimeout` between 1 second and 7 days
- `MessageTimeToLive` between 1 second and 7 days (if specified)

### Usage Example

```csharp
services.AddHoneyDrunkStorageQueueTransport(options =>
{
    // Authentication (choose one)
    options.ConnectionString = "DefaultEndpointsProtocol=https;AccountName=...";
    // OR
    options.AccountEndpoint = new Uri("https://myaccount.queue.core.windows.net");
    
    // Queue settings
    options.QueueName = "orders-queue";
    options.CreateIfNotExists = true;
    options.PoisonQueueName = "orders-poison";  // Default: "{QueueName}-poison"
    
    // Message settings
    options.VisibilityTimeout = TimeSpan.FromMinutes(5);
    options.MessageTimeToLive = TimeSpan.FromDays(1);
    options.MaxDequeueCount = 5;
    
    // Performance (high throughput)
    options.MaxConcurrency = 10;
    options.PrefetchMaxMessages = 32;
    options.BatchProcessingConcurrency = 4;  // 10 × 4 = 40 concurrent
    options.MaxBatchPublishConcurrency = 32;
    
    // Polling (aggressive for low latency)
    options.EmptyQueuePollingInterval = TimeSpan.FromMilliseconds(500);
    options.MaxPollingInterval = TimeSpan.FromSeconds(2);
});
```

[↑ Back to top](#table-of-contents)

---

[← Back to File Guide](FILE_GUIDE.md) | [Azure Service Bus](AzureServiceBus.md) | [Storage Queue](StorageQueue.md) | [↑ Back to top](#table-of-contents)
