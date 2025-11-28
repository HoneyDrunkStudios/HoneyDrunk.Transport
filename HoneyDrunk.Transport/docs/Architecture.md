# HoneyDrunk Architecture - Transport Layer

[← Back to File Guide](FILE_GUIDE.md)

---

## Dependency Flow

The HoneyDrunk architecture follows a strict layered approach:

```
┌─────────────────────────────────────────┐
│         Application Nodes               │
│  (Order Service, Payment Service, etc)  │
└─────────────────────────────────────────┘
            ↓ depends on
┌─────────────────────────────────────────┐
│       HoneyDrunk.Transport.*            │
│  (Messaging, Outbox, Consumers)         │
└─────────────────────────────────────────┘
            ↓ depends on
┌─────────────────────────────────────────┐
│         HoneyDrunk.Data.*               │
│  (Storage primitives, conventions)      │
└─────────────────────────────────────────┘
            ↓ depends on
┌─────────────────────────────────────────┐
│      HoneyDrunk.Kernel.*                │
│  (Base primitives, IGridContext)        │
└─────────────────────────────────────────┘
```

### ✅ Allowed Dependencies

- **Transport → Data**: Transport uses Data's storage primitives for outbox
- **Transport → Kernel**: Transport uses IGridContext, correlation IDs
- **Data → Kernel**: Data uses base primitives
- **Application → Transport**: Apps publish/consume messages
- **Application → Data**: Apps persist domain entities
- **Application → Kernel**: Apps use Grid context

### ❌ Forbidden Dependencies

- **Data → Transport**: Data MUST NOT know Transport exists
- **Kernel → Data**: Kernel has no storage knowledge
- **Kernel → Transport**: Kernel has no messaging knowledge

---

## What Each Layer Provides

### 🔷 HoneyDrunk.Kernel

**Purpose**: Base primitives for distributed systems

**Exports**:
- `IGridContext` - Distributed context (correlation, tenant, node)
- `CorrelationId` - ULID-based strongly-typed IDs
- `TimeProvider` - Testable time abstraction
- `GridHeaderNames` - Canonical header name constants

**Does NOT Export**:
- Storage abstractions
- Messaging abstractions
- Broker-specific types

---

### 🔷 HoneyDrunk.Data

**Purpose**: Storage primitives and conventions

**Exports**:
- `ISqlConnectionFactory` - Provides database connections
- `IDbSession` / `IUnitOfWork` - Manages transactions
- `OutboxConventions` - Table/column naming conventions
  ```csharp
  public static class OutboxConventions
  {
      public static string GetTableName() => "Outbox";
      public static string GetSchemaName() => "messaging";
      public static string GetIdColumnName() => "Id";
      public static string GetPayloadColumnName() => "Payload";
      // etc...
  }
  ```
- `IMigrationContributor` - Hook for schema contributions

**Does NOT Export**:
- `ITransportPublisher` - That's Transport's job
- `IMessageHandler<T>` - That's Transport's job
- `IOutboxStore` - That's Transport's job
- Any messaging behavior

**Data's Role**: Provide the "database muscle" that Transport uses to implement messaging patterns.

---

### 🔷 HoneyDrunk.Transport

**Purpose**: Messaging and reliable delivery patterns

**Exports**:
- `ITransportPublisher` - Publishes envelopes to brokers
- `ITransportConsumer` - Consumes messages from brokers
- `IMessageHandler<T>` - Type-safe message processing
- `ITransportEnvelope` - Message wrapper with Grid context
- `EnvelopeFactory` - Creates envelopes from messages

**Depends On**:
- **Kernel**: For `IGridContext`, `CorrelationId`, `TimeProvider`
- **Data**: For storage primitives to implement outbox

**Implements Outbox Using Data**:
```csharp
// HoneyDrunk.Transport.SqlServerOutbox
public class SqlServerOutboxStore(
    ISqlConnectionFactory connectionFactory) : IOutboxStore
{
    public async Task SaveAsync(OutboxMessage message, CancellationToken ct)
    {
        // Use Data's connection factory
        await using var connection = await connectionFactory.CreateConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        
        // Use Data's conventions
        var tableName = OutboxConventions.GetFullTableName();
        var sql = $@"
            INSERT INTO {tableName} 
            ({OutboxConventions.GetIdColumnName()}, 
             {OutboxConventions.GetPayloadColumnName()})
            VALUES (@Id, @Payload)";
        
        await connection.ExecuteAsync(sql, message, transaction);
        await transaction.CommitAsync(ct);
    }
}
```

**Does NOT Export To Data**: Data never calls Transport APIs.

---

### 🔷 Application Nodes

**Purpose**: Business logic and domain services

**Uses**:
- **Kernel**: For `IGridContext`, correlation tracking
- **Data**: For persisting domain entities
- **Transport**: For publishing domain events, consuming messages

**Example**:
```csharp
public class OrderService(
    IOrderRepository repository,
    ITransportPublisher publisher,
    EnvelopeFactory envelopeFactory,
    IGridContext gridContext)
{
    public async Task CreateOrderAsync(Order order, CancellationToken ct)
    {
        // 1. Use Data to persist
        await repository.SaveAsync(order, ct);
        
        // 2. Use Transport to publish event
        var domainEvent = new OrderCreated(order.Id);
        var payload = JsonSerializer.SerializeToUtf8Bytes(domainEvent);
        var envelope = envelopeFactory.CreateEnvelopeWithGridContext<OrderCreated>(
            payload, gridContext);
        
        await publisher.PublishAsync(envelope, new EndpointAddress("orders.created"), ct);
    }
}
```

---

## Outbox Pattern: The Right Way

### ❌ Wrong Approach (Data → Transport)

```csharp
// WRONG: Data depending on Transport
namespace HoneyDrunk.Data
{
    public class OutboxRepository(ITransportPublisher publisher) // ❌ BAD!
    {
        public async Task DispatchAsync()
        {
            await publisher.PublishAsync(...); // Data calling Transport
        }
    }
}
```

**Why This Is Wrong**:
- Creates circular dependency potential
- Couples storage layer to messaging concerns
- Violates single responsibility
- Makes Data hard to test without Transport

---

### ✅ Correct Approach (Transport → Data)

```csharp
// CORRECT: Data exposes storage primitives
namespace HoneyDrunk.Data.SqlServer
{
    public interface ISqlConnectionFactory
    {
        Task<SqlConnection> CreateConnectionAsync(CancellationToken ct);
    }
    
    public static class OutboxConventions
    {
        public static string GetFullTableName() => "[messaging].[Outbox]";
        public static string GetIdColumnName() => "Id";
        public static string GetPayloadColumnName() => "Payload";
        public static string GetDestinationColumnName() => "Destination";
        public static string GetDispatchedAtColumnName() => "DispatchedAt";
    }
}

// CORRECT: Transport implements outbox using Data's primitives
namespace HoneyDrunk.Transport.SqlServerOutbox
{
    public class SqlServerOutboxStore(
        ISqlConnectionFactory connectionFactory) : IOutboxStore
    {
        public async Task SaveAsync(OutboxMessage message, CancellationToken ct)
        {
            // Use Data's connection factory
            var connection = await connectionFactory.CreateConnectionAsync(ct);
            
            // Use Data's conventions
            var table = OutboxConventions.GetFullTableName();
            var sql = $"INSERT INTO {table} ...";
            
            await connection.ExecuteAsync(sql, message);
        }
        
        public async Task<IEnumerable<OutboxMessage>> LoadPendingAsync(int limit, CancellationToken ct)
        {
            var connection = await connectionFactory.CreateConnectionAsync(ct);
            var table = OutboxConventions.GetFullTableName();
            var dispatchedCol = OutboxConventions.GetDispatchedAtColumnName();
            
            var sql = $"SELECT TOP {limit} * FROM {table} WHERE {dispatchedCol} IS NULL";
            return await connection.QueryAsync<OutboxMessage>(sql);
        }
    }
    
    public class OutboxDispatcherService(
        IOutboxStore store,
        ITransportPublisher publisher) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                var pending = await store.LoadPendingAsync(100, ct);
                
                foreach (var message in pending)
                {
                    var envelope = RecreateEnvelope(message);
                    await publisher.PublishAsync(envelope, message.Destination, ct);
                    await store.MarkDispatchedAsync(message.Id, ct);
                }
                
                await Task.Delay(TimeSpan.FromSeconds(1), ct);
            }
        }
    }
}
```

**Why This Is Correct**:
- Data provides "database muscle" (connections, conventions)
- Transport provides "messaging brain" (outbox logic, dispatch)
- Clean separation of concerns
- Each layer testable independently
- No circular dependencies

---

## What Data Should Expose

### 1. Connection/Transaction Primitives

```csharp
// Per-provider connection factory
public interface ISqlConnectionFactory
{
    Task<SqlConnection> CreateConnectionAsync(CancellationToken ct);
}

// Or unified session abstraction
public interface IDbSession : IAsyncDisposable
{
    Task<IDbTransaction> BeginTransactionAsync(CancellationToken ct);
    Task<int> ExecuteAsync(string sql, object? parameters = null);
    Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? parameters = null);
}
```

### 2. Naming Conventions

```csharp
public static class OutboxConventions
{
    public const string DefaultSchema = "messaging";
    public const string DefaultTableName = "Outbox";
    
    public static string GetFullTableName(string? schema = null, string? table = null)
        => $"[{schema ?? DefaultSchema}].[{table ?? DefaultTableName}]";
    
    public static string GetIdColumnName() => "Id";
    public static string GetPayloadColumnName() => "Payload";
    public static string GetMessageTypeColumnName() => "MessageType";
    public static string GetDestinationColumnName() => "Destination";
    public static string GetCreatedAtColumnName() => "CreatedAt";
    public static string GetDispatchedAtColumnName() => "DispatchedAt";
}
```

### 3. Migration Hooks (Optional)

```csharp
public interface IMigrationContributor
{
    string Name { get; }
    Task<IEnumerable<string>> GetMigrationScriptsAsync(CancellationToken ct);
}

// Transport provides the migration
public class OutboxMigrationContributor : IMigrationContributor
{
    public string Name => "Transport.Outbox";
    
    public Task<IEnumerable<string>> GetMigrationScriptsAsync(CancellationToken ct)
    {
        var scripts = new[]
        {
            $@"
            CREATE TABLE {OutboxConventions.GetFullTableName()} (
                {OutboxConventions.GetIdColumnName()} UNIQUEIDENTIFIER PRIMARY KEY,
                {OutboxConventions.GetPayloadColumnName()} VARBINARY(MAX) NOT NULL,
                {OutboxConventions.GetCreatedAtColumnName()} DATETIME2 NOT NULL,
                {OutboxConventions.GetDispatchedAtColumnName()} DATETIME2 NULL
            )"
        };
        
        return Task.FromResult<IEnumerable<string>>(scripts);
    }
}
```

---

## What Data Should NOT Expose

### ❌ Messaging Abstractions

```csharp
// WRONG - These belong in Transport, not Data
public interface IMessagePublisher { }      // ❌
public interface ITransportPublisher { }    // ❌
public interface IMessageHandler<T> { }     // ❌
public interface ITransportConsumer { }     // ❌
```

### ❌ Messaging Behavior

```csharp
// WRONG - Dispatch logic belongs in Transport
public class OutboxDispatcher { }           // ❌
public interface IOutboxStore { }           // ❌ (lives in Transport)
public class OutboxMessage { }              // ❌ (Transport's DTO)
```

### ❌ Broker Knowledge

```csharp
// WRONG - Data should not know about brokers
public interface IServiceBusClient { }      // ❌
public interface IStorageQueueClient { }    // ❌
public class EnvelopeFactory { }            // ❌
```

---

## Summary

| Layer | Provides | Depends On | Does NOT Provide |
|-------|----------|------------|------------------|
| **Kernel** | Base primitives (IGridContext, CorrelationId) | Nothing | Storage, Messaging |
| **Data** | Storage primitives, conventions | Kernel | Messaging, Outbox behavior |
| **Transport** | Messaging, Outbox implementation | Kernel, Data | Domain logic |
| **Application** | Business logic | Kernel, Data, Transport | Infrastructure |

**Golden Rule**: Data is the "database muscle", Transport is the "messaging brain" that uses that muscle.

**Key Insight**: Transport depends on Data for persistence, but Data never knows Transport exists.

---

[← Back to File Guide](FILE_GUIDE.md)
