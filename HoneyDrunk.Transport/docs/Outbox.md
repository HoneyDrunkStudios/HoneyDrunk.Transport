# 📤 Outbox - Transactional Messaging Pattern

[← Back to File Guide](FILE_GUIDE.md)

---

## Table of Contents

- [Overview](#overview)
- [IOutboxStore.cs](#ioutboxstorecs)
- [IOutboxDispatcher.cs](#ioutboxdispatchercs)
- [IOutboxMessage.cs](#ioutboxmessagecs)
- [OutboxMessageState.cs](#outboxmessagestatecs)
- [Complete Outbox Pattern Example](#complete-outbox-pattern-example)

---

## Overview

The transactional outbox pattern ensures exactly-once message delivery by storing messages in the database within the same transaction as business logic. A background dispatcher then publishes these messages reliably.

**Location:** `HoneyDrunk.Transport/Outbox/`

---

## IOutboxStore.cs

```csharp
public interface IOutboxStore
{
    Task SaveAsync(
        IOutboxMessage message,
        CancellationToken cancellationToken = default);
    
    Task<IEnumerable<IOutboxMessage>> LoadPendingAsync(
        int batchSize,
        CancellationToken cancellationToken = default);
    
    Task MarkDispatchedAsync(
        string messageId,
        CancellationToken cancellationToken = default);
    
    Task MarkFailedAsync(
        string messageId,
        Exception exception,
        CancellationToken cancellationToken = default);
    
    Task MarkPoisonedAsync(
        string messageId,
        string reason,
        CancellationToken cancellationToken = default);
}
```

### Usage Example

```csharp
// Entity Framework implementation
public class EfCoreOutboxStore(ApplicationDbContext db) : IOutboxStore
{
    public async Task SaveAsync(IOutboxMessage message, CancellationToken ct)
    {
        await db.OutboxMessages.AddAsync(new OutboxMessageEntity
        {
            MessageId = message.MessageId,
            MessageType = message.MessageType,
            Payload = message.Payload.ToArray(),
            Headers = JsonSerializer.Serialize(message.Headers),
            State = OutboxMessageState.Pending,
            CreatedAt = DateTime.UtcNow
        }, ct);
        
        // Don't call SaveChangesAsync - parent transaction handles it
    }
    
    public async Task<IEnumerable<IOutboxMessage>> LoadPendingAsync(
        int batchSize,
        CancellationToken ct)
    {
        return await db.OutboxMessages
            .Where(m => m.State == OutboxMessageState.Pending)
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize)
            .Select(m => new OutboxMessage
            {
                MessageId = m.MessageId,
                MessageType = m.MessageType,
                Payload = m.Payload,
                Headers = JsonSerializer.Deserialize<Dictionary<string, string>>(m.Headers)!
            })
            .ToListAsync(ct);
    }
    
    public async Task MarkDispatchedAsync(string messageId, CancellationToken ct)
    {
        var message = await db.OutboxMessages.FindAsync([messageId], ct);
        if (message != null)
        {
            message.State = OutboxMessageState.Dispatched;
            message.DispatchedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }
    
    public async Task MarkFailedAsync(
        string messageId,
        Exception exception,
        CancellationToken ct)
    {
        var message = await db.OutboxMessages.FindAsync([messageId], ct);
        if (message != null)
        {
            message.State = OutboxMessageState.Failed;
            message.FailedAt = DateTime.UtcNow;
            message.Error = exception.ToString();
            message.RetryCount++;
            
            // Mark as poisoned after max retries
            if (message.RetryCount >= 5)
            {
                message.State = OutboxMessageState.Poisoned;
            }
            
            await db.SaveChangesAsync(ct);
        }
    }
    
    public async Task MarkPoisonedAsync(
        string messageId,
        string reason,
        CancellationToken ct)
    {
        var message = await db.OutboxMessages.FindAsync([messageId], ct);
        if (message != null)
        {
            message.State = OutboxMessageState.Poisoned;
            message.Error = reason;
            await db.SaveChangesAsync(ct);
        }
    }
}
```

---

## IOutboxDispatcher.cs

```csharp
public interface IOutboxDispatcher
{
    Task DispatchPendingAsync(CancellationToken cancellationToken = default);
}
```

### Usage Example

```csharp
public class OutboxDispatcherService(
    IOutboxStore store,
    ITransportPublisher publisher,
    ILogger<OutboxDispatcherService> logger) 
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var pending = await store.LoadPendingAsync(100, ct);
                
                foreach (var message in pending)
                {
                    try
                    {
                        await publisher.PublishAsync(message.Envelope, ct);
                        await store.MarkDispatchedAsync(message.MessageId, ct);
                        
                        logger.LogInformation(
                            "Dispatched outbox message {MessageId}",
                            message.MessageId);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex,
                            "Failed to dispatch outbox message {MessageId}",
                            message.MessageId);
                        
                        await store.MarkFailedAsync(message.MessageId, ex, ct);
                    }
                }
                
                // Poll interval
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in outbox dispatcher loop");
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
            }
        }
    }
}

// Register
services.AddHostedService<OutboxDispatcherService>();
```

---

## IOutboxMessage.cs

```csharp
public interface IOutboxMessage
{
    string MessageId { get; }
    string MessageType { get; }
    ReadOnlyMemory<byte> Payload { get; }
    IReadOnlyDictionary<string, string> Headers { get; }
    OutboxMessageState State { get; }
    DateTimeOffset CreatedAt { get; }
    DateTimeOffset? DispatchedAt { get; }
    ITransportEnvelope Envelope { get; }
}
```

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

---

## Complete Outbox Pattern Example

```csharp
// Step 1: Service uses outbox within transaction
public class OrderService(
    ApplicationDbContext db,
    IOutboxStore outbox,
    EnvelopeFactory factory,
    IMessageSerializer serializer)
{
    public async Task CreateOrderAsync(Order order, CancellationToken ct)
    {
        using var transaction = await db.Database.BeginTransactionAsync(ct);
        
        try
        {
            // Save order to database
            db.Orders.Add(order);
            
            // Save message to outbox (same transaction)
            var message = new OrderCreated(order.Id, order.CustomerId);
            var payload = serializer.Serialize(message);
            var envelope = factory.CreateEnvelope<OrderCreated>(payload);
            
            await outbox.SaveAsync(new OutboxMessage
            {
                MessageId = envelope.MessageId,
                MessageType = envelope.MessageType,
                Payload = envelope.Payload,
                Headers = envelope.Headers,
                State = OutboxMessageState.Pending,
                CreatedAt = DateTimeOffset.UtcNow,
                Envelope = envelope
            }, ct);
            
            // Commit both operations atomically
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            
            // Background dispatcher will publish later
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }
}

// Step 2: Background service dispatches messages
services.AddHostedService<OutboxDispatcherService>();

// Step 3: Monitor outbox health
services.AddSingleton<ITransportHealthContributor, OutboxHealthContributor>();
```

---

[← Back to File Guide](FILE_GUIDE.md) | [↑ Back to top](#table-of-contents)
