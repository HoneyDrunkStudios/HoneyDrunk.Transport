# 📤 Outbox - Transactional Messaging Pattern

[← Back to File Guide](FILE_GUIDE.md)

---

## Table of Contents

- [Overview](#overview)
- [IOutboxStore.cs](#ioutboxstorecs)
- [IOutboxDispatcher.cs](#ioutboxdispatchercs)
- [IOutboxMessage.cs](#ioutboxmessagecs)
- [OutboxMessage.cs](#outboxmessagecs)
- [OutboxMessageState.cs](#outboxmessagestatecs)
- [OutboxDispatcherOptions.cs](#outboxdispatcheroptionscs)
- [DefaultOutboxDispatcher.cs](#defaultoutboxdispatchercs)
- [Complete Outbox Pattern Example](#complete-outbox-pattern-example)

---

## Overview

The transactional outbox pattern ensures exactly-once message delivery by storing messages in the database within the same transaction as business logic. A background dispatcher then publishes these messages reliably.

**Location:** `HoneyDrunk.Transport/Outbox/`

**Layering Note:**

- The `Outbox` folder in `HoneyDrunk.Transport` defines the abstractions and core dispatcher.
- Concrete stores live either:
  - In application code (like the `EfCoreOutboxStore` example below), or
  - In provider packages (for example: `HoneyDrunk.Transport.Outbox.SqlServer`) that integrate with `HoneyDrunk.Data` connection factories and conventions.

Transport itself does not depend on Entity Framework or any specific database library.

---

## IOutboxStore.cs

Storage abstraction for the transactional outbox pattern.

```csharp
public interface IOutboxStore
{
    Task SaveAsync(
        IEndpointAddress destination,
        ITransportEnvelope envelope,
        CancellationToken cancellationToken = default);
    
    Task SaveBatchAsync(
        IEnumerable<(IEndpointAddress destination, ITransportEnvelope envelope)> messages,
        CancellationToken cancellationToken = default);
    
    Task<IEnumerable<IOutboxMessage>> LoadPendingAsync(
        int batchSize = 100,
        CancellationToken cancellationToken = default);
    
    Task MarkDispatchedAsync(
        string outboxMessageId,
        CancellationToken cancellationToken = default);
    
    Task MarkFailedAsync(
        string outboxMessageId,
        string errorMessage,
        DateTimeOffset? retryAt = null,
        CancellationToken cancellationToken = default);
    
    Task MarkPoisonedAsync(
        string outboxMessageId,
        string errorMessage,
        CancellationToken cancellationToken = default);
    
    Task CleanupDispatchedAsync(
        DateTimeOffset olderThan,
        CancellationToken cancellationToken = default);
}
```

### Usage Example

```csharp
// Entity Framework implementation (application-level)
public class EfCoreOutboxStore(ApplicationDbContext db) : IOutboxStore
{
    public async Task SaveAsync(
        IEndpointAddress destination,
        ITransportEnvelope envelope,
        CancellationToken ct)
    {
        await db.OutboxMessages.AddAsync(new OutboxMessageEntity
        {
            Id = Ulid.NewUlid().ToString(),
            DestinationName = destination.Name,
            DestinationAddress = destination.Address,
            MessageId = envelope.MessageId,
            MessageType = envelope.MessageType,
            Payload = envelope.Payload.ToArray(),
            Headers = JsonSerializer.Serialize(envelope.Headers),
            State = OutboxMessageState.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        }, ct);
        
        // Don't call SaveChangesAsync - parent transaction handles it
    }
    
    public async Task<IEnumerable<IOutboxMessage>> LoadPendingAsync(
        int batchSize,
        CancellationToken ct)
    {
        return await db.OutboxMessages
            .Where(m => m.State == OutboxMessageState.Pending)
            .Where(m => m.ScheduledAt == null || m.ScheduledAt <= DateTimeOffset.UtcNow)
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize)
            .Select(m => new OutboxMessage
            {
                Id = m.Id,
                Destination = EndpointAddress.Create(m.DestinationName, m.DestinationAddress),
                Envelope = new TransportEnvelope(
                    messageId: m.MessageId,
                    messageType: m.MessageType,
                    payload: m.Payload,
                    headers: JsonSerializer.Deserialize<Dictionary<string, string>>(m.Headers)!),
                State = m.State,
                Attempts = m.Attempts,
                CreatedAt = m.CreatedAt,
                ProcessedAt = m.ProcessedAt,
                ScheduledAt = m.ScheduledAt,
                ErrorMessage = m.ErrorMessage
            })
            .ToListAsync(ct);
    }
    
    public async Task MarkDispatchedAsync(string outboxMessageId, CancellationToken ct)
    {
        var message = await db.OutboxMessages.FindAsync([outboxMessageId], ct);
        if (message != null)
        {
            message.State = OutboxMessageState.Dispatched;
            message.ProcessedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }
    
    public async Task MarkFailedAsync(
        string outboxMessageId,
        string errorMessage,
        DateTimeOffset? retryAt,
        CancellationToken ct)
    {
        var message = await db.OutboxMessages.FindAsync([outboxMessageId], ct);
        if (message != null)
        {
            message.State = OutboxMessageState.Failed;
            message.ProcessedAt = DateTimeOffset.UtcNow;
            message.ErrorMessage = errorMessage;
            message.Attempts++;
            message.ScheduledAt = retryAt;
            await db.SaveChangesAsync(ct);
        }
    }
    
    public async Task MarkPoisonedAsync(
        string outboxMessageId,
        string errorMessage,
        CancellationToken ct)
    {
        var message = await db.OutboxMessages.FindAsync([outboxMessageId], ct);
        if (message != null)
        {
            message.State = OutboxMessageState.Poisoned;
            message.ProcessedAt = DateTimeOffset.UtcNow;
            message.ErrorMessage = errorMessage;
            await db.SaveChangesAsync(ct);
        }
    }
    
    public async Task CleanupDispatchedAsync(DateTimeOffset olderThan, CancellationToken ct)
    {
        await db.OutboxMessages
            .Where(m => m.State == OutboxMessageState.Dispatched)
            .Where(m => m.ProcessedAt < olderThan)
            .ExecuteDeleteAsync(ct);
    }
}
```

[↑ Back to top](#table-of-contents)

---

## IOutboxDispatcher.cs

```csharp
public interface IOutboxDispatcher
{
    Task DispatchPendingAsync(
        int batchSize = 100,
        CancellationToken cancellationToken = default);
}
```

[↑ Back to top](#table-of-contents)

---

## IOutboxMessage.cs

Represents an outbox message pending dispatch. Contains both the envelope and its destination.

```csharp
public interface IOutboxMessage
{
    string Id { get; }
    IEndpointAddress Destination { get; }
    ITransportEnvelope Envelope { get; }
    OutboxMessageState State { get; }
    int Attempts { get; }
    DateTimeOffset CreatedAt { get; }
    DateTimeOffset? ProcessedAt { get; }
    DateTimeOffset? ScheduledAt { get; }
    string? ErrorMessage { get; }
}
```

[↑ Back to top](#table-of-contents)

---

## OutboxMessage.cs

Default implementation of `IOutboxMessage`.

```csharp
public sealed class OutboxMessage : IOutboxMessage
{
    public required string Id { get; init; }
    public required IEndpointAddress Destination { get; init; }
    public required ITransportEnvelope Envelope { get; init; }
    public OutboxMessageState State { get; init; }
    public int Attempts { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ProcessedAt { get; init; }
    public DateTimeOffset? ScheduledAt { get; init; }
    public string? ErrorMessage { get; init; }
}
```

[↑ Back to top](#table-of-contents)

---

## OutboxMessageState.cs

```csharp
public enum OutboxMessageState
{
    Pending,    // Waiting to be dispatched
    Dispatched, // Successfully published
    Failed,     // Temporary failure, will retry
    Poisoned    // Permanent failure after max retries
}
```

[↑ Back to top](#table-of-contents)

---

## OutboxDispatcherOptions.cs

Configuration for the built-in outbox dispatcher.

```csharp
public sealed class OutboxDispatcherOptions
{
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);
    public int BatchSize { get; set; } = 100;
    public int MaxRetryAttempts { get; set; } = 5;
    public TimeSpan BaseRetryDelay { get; set; } = TimeSpan.FromSeconds(1);
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan StartupDelay { get; set; } = TimeSpan.Zero;
    public TimeSpan ErrorDelay { get; set; } = TimeSpan.FromSeconds(30);
}
```

[↑ Back to top](#table-of-contents)

---

## DefaultOutboxDispatcher.cs

Background service that implements the outbox dispatch loop with exponential backoff retry.

```csharp
public sealed partial class DefaultOutboxDispatcher(
    IOutboxStore store,
    ITransportPublisher publisher,
    IOptions<OutboxDispatcherOptions> options,
    ILogger<DefaultOutboxDispatcher> logger) : BackgroundService, IOutboxDispatcher
{
    public async Task DispatchPendingAsync(
        int batchSize = 100,
        CancellationToken cancellationToken = default);
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken);
}
```

### How It Works

1. **Polls** the outbox store at configured intervals
2. **Loads** pending messages in batches
3. **Publishes** each message using `ITransportPublisher.PublishAsync(envelope, destination, ct)`
4. **Marks dispatched** on success
5. **Marks failed** with exponential backoff retry scheduling on failure
6. **Marks poisoned** after exceeding `MaxRetryAttempts`

### Registration

```csharp
// Register outbox store implementation (application or provider package)
services.AddSingleton<IOutboxStore, EfCoreOutboxStore>();

// Register dispatcher as hosted service
services.AddHostedService<DefaultOutboxDispatcher>();

// Configure options
services.Configure<OutboxDispatcherOptions>(options =>
{
    options.PollInterval = TimeSpan.FromSeconds(5);
    options.BatchSize = 100;
    options.MaxRetryAttempts = 5;
    options.BaseRetryDelay = TimeSpan.FromSeconds(1);
    options.MaxRetryDelay = TimeSpan.FromMinutes(5);
});
```

[↑ Back to top](#table-of-contents)

---

## Complete Outbox Pattern Example

```csharp
// Step 1: Service uses outbox within transaction
public class OrderService(
    ApplicationDbContext db,
    IOutboxStore outbox,
    EnvelopeFactory factory,
    IMessageSerializer serializer,
    IGridContext gridContext)
{
    public async Task CreateOrderAsync(Order order, CancellationToken ct)
    {
        using var transaction = await db.Database.BeginTransactionAsync(ct);
        
        try
        {
            // Save order to database
            db.Orders.Add(order);
            
            // Create message and envelope
            var message = new OrderCreated(order.Id, order.CustomerId);
            var payload = serializer.Serialize(message);
            var envelope = factory.CreateEnvelopeWithGridContext<OrderCreated>(
                payload,
                gridContext);
            
            // Create destination
            var destination = EndpointAddress.Create(
                name: "orders",
                address: "orders.created");
            
            // Save to outbox (same transaction)
            await outbox.SaveAsync(destination, envelope, ct);
            
            // Commit both operations atomically
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            
            // DefaultOutboxDispatcher will publish later
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }
}

// Step 2: Register outbox infrastructure
services.AddSingleton<IOutboxStore, EfCoreOutboxStore>();
services.AddHostedService<DefaultOutboxDispatcher>();
services.Configure<OutboxDispatcherOptions>(options =>
{
    options.PollInterval = TimeSpan.FromSeconds(5);
    options.BatchSize = 100;
});

// Step 3: Monitor outbox health
services.AddSingleton<ITransportHealthContributor, OutboxHealthContributor>();
```

[↑ Back to top](#table-of-contents)

---

[← Back to File Guide](FILE_GUIDE.md) | [↑ Back to top](#table-of-contents)
