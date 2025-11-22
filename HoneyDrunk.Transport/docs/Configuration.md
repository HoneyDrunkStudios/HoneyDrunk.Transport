# ⚙️ Configuration - Settings and Options

[← Back to File Guide](FILE_GUIDE.md)

---

## Overview

Configuration classes for controlling transport behavior including retry policies, backoff strategies, and error handling.

**Location:** `HoneyDrunk.Transport/Configuration/`

---

## TransportCoreOptions.cs

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

---

## RetryOptions.cs

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

---

## IErrorHandlingStrategy.cs

```csharp
public interface IErrorHandlingStrategy
{
    ErrorHandlingDecision Decide(
        Exception exception,
        MessageContext context,
        int attemptCount);
}
```

### Usage Example

```csharp
public class CustomErrorStrategy : IErrorHandlingStrategy
{
    public ErrorHandlingDecision Decide(
        Exception exception,
        MessageContext context,
        int attemptCount)
    {
        return exception switch
        {
            SqlException { Number: -2 } => // Timeout
                new ErrorHandlingDecision(
                    ErrorHandlingAction.Retry,
                    TimeSpan.FromSeconds(5)),
            
            ValidationException => // Invalid data
                new ErrorHandlingDecision(ErrorHandlingAction.DeadLetter),
            
            _ => attemptCount < 3
                ? new ErrorHandlingDecision(
                    ErrorHandlingAction.Retry,
                    TimeSpan.FromSeconds(attemptCount * 2))
                : new ErrorHandlingDecision(ErrorHandlingAction.DeadLetter)
        };
    }
}

services.AddSingleton<IErrorHandlingStrategy, CustomErrorStrategy>();
```

---

## ErrorHandlingAction.cs

```csharp
public enum ErrorHandlingAction
{
    Retry,        // Retry with delay
    DeadLetter,   // Move to DLQ
    Ignore        // Discard message
}
```

---

## ErrorHandlingDecision.cs

```csharp
public sealed class ErrorHandlingDecision
{
    public ErrorHandlingAction Action { get; }
    public TimeSpan? Delay { get; }
    
    public ErrorHandlingDecision(ErrorHandlingAction action, TimeSpan? delay = null)
    {
        Action = action;
        Delay = delay;
    }
}
```

---

[← Back to File Guide](FILE_GUIDE.md)
