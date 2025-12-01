# HoneyDrunk Architecture - Transport Layer

[← Back to File Guide](FILE_GUIDE.md)

---

## Table of Contents

- [Dependency Flow](#dependency-flow)
- [What Each Layer Provides](#what-each-layer-provides)
  - [HoneyDrunk.Kernel](#-honeydrunkkernel)
  - [HoneyDrunk.Data](#-honeydrunkdata)
  - [HoneyDrunk.Transport](#-honeydrunktransport)
  - [Application Nodes](#-application-nodes)
- [Outbox Pattern: The Right Way](#outbox-pattern-the-right-way)
  - [Wrong Approach (Data → Transport)](#-wrong-approach-data--transport)
  - [Correct Approach (Transport → Data)](#-correct-approach-transport--data)
- [What Data Should Expose](#what-data-should-expose)
- [What Data Should NOT Expose](#what-data-should-not-expose)
- [Summary](#summary)

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
- **Data → Transport.Abstractions**: Data can use `IMessagePublisher` (high-level messaging API)
- **Application → Transport**: Apps publish/consume messages
- **Application → Data**: Apps persist domain entities
- **Application → Kernel**: Apps use Grid context

### ❌ Forbidden Dependencies

- **Data → Transport Implementation**: Data MUST NOT depend on transport implementations (Azure Service Bus, Storage Queue, etc.)
- **Data defining messaging abstractions**: Data MUST NOT define `IMessagePublisher`, `ITransportPublisher`, etc.
- **Kernel → Data**: Kernel has no storage knowledge
- **Kernel → Transport**: Kernel has no messaging knowledge

**Clarification**: The key distinction is:
- ✅ **Data using Transport abstractions** (`IMessagePublisher`) = **Allowed**
- ❌ **Data defining messaging abstractions** = **Forbidden**
- ❌ **Data depending on transport implementations** = **Forbidden**

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
    Task<int> ExecuteAsync(String sql, Object? parameters = null);
    Task<T?> QuerySingleOrDefaultAsync<T>(String sql, Object? parameters = null);
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

### ❌ Messaging Abstractions - Definition vs Usage

**Important Distinction**: Data should **NOT define** messaging abstractions, but it **CAN use** Transport's abstractions.

```csharp
// ❌ WRONG - Data defining its own messaging abstractions
namespace HoneyDrunk.Data
{
    public interface IMessagePublisher { }      // ❌ Don't define in Data
    public interface ITransportPublisher { }    // ❌ Don't define in Data
    public interface IMessageHandler<T> { }     // ❌ Don't define in Data
    public interface ITransportConsumer { }     // ❌ Don't define in Data
}

// ✅ CORRECT - Data using Transport's abstractions
namespace HoneyDrunk.Data.Repositories
{
    using HoneyDrunk.Transport.Abstractions; // ✅ OK to depend on Transport
    
    public class OrderRepository(
        IDbSession session,
        IMessagePublisher publisher) // ✅ OK - using Transport's abstraction
    {
        public async Task SaveOrderAsync(Order order, IGridContext context, CancellationToken ct)
        {
            // 1. Save to database
            await session.ExecuteAsync("INSERT INTO Orders ...", order);
            
            // 2. Publish domain event using Transport's high-level API
            await publisher.PublishAsync(
                "orders.created",
                new OrderCreated(order.Id),
                context,
                ct);
        }
    }
}
```

**Design Rationale**:
- `IMessagePublisher` is Transport's **high-level abstraction** for Grid-context-aware publishing
- Data and Application layers **should use** `IMessagePublisher` (not `ITransportPublisher`)
- `ITransportPublisher` is Transport's **low-level implementation detail** for envelope-based messaging
- Data should **never define** its own competing messaging abstractions

---

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
| **Data** | Storage primitives, conventions | Kernel, **Transport (via IMessagePublisher)** | Messaging **abstractions** (but can use Transport's) |
| **Transport** | Messaging abstractions (IMessagePublisher, ITransportPublisher), Outbox implementation | Kernel, Data | Domain logic |
| **Application** | Business logic | Kernel, Data, Transport | Infrastructure |

**Golden Rule**: Data is the "database muscle", Transport is the "messaging brain" that uses that muscle.

**Key Insights**:
1. Transport depends on Data for persistence, but Data never knows Transport's **implementation details**
2. Data **can use** Transport's high-level abstractions (`IMessagePublisher`) for publishing domain events
3. `IMessagePublisher` is designed for Data/Application layers; `ITransportPublisher` is Transport's internal detail
4. Data should **never define** its own messaging abstractions - always use Transport's

---

[← Back to File Guide](FILE_GUIDE.md) | [↑ Back to top](#table-of-contents)
