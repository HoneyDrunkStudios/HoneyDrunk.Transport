# HoneyDrunk Architecture - Transport Layer

[← Back to File Guide](FILE_GUIDE.md)

---

## Table of Contents

- [Dependency Flow](#dependency-flow)
- [What Each Layer Provides](#what-each-layer-provides)
  - [HoneyDrunk.Kernel](#-honeydrunkkernel)
  - [HoneyDrunk.Data](#-honeydrunkdata)
  - [HoneyDrunk.Transport](#-honeydrunktransport)
  - [Bridge Packages](#-bridge-packages)
  - [Application Nodes](#-application-nodes)
- [Outbox Pattern: The Right Way](#outbox-pattern-the-right-way)
  - [Wrong Approach (Data → Transport)](#-wrong-approach-data--transport)
  - [Correct Approach (Bridge Package)](#-correct-approach-bridge-package-transport-outbox--data-provider)
- [What Data Should Expose](#what-data-should-expose)
- [What Data Should NOT Expose](#what-data-should-not-expose)
- [Summary](#summary)

---

## Dependency Flow

At the package level, the HoneyDrunk architecture follows this model:

```text
┌─────────────────────────────────────────┐
│         Application Nodes               │
│  (Order Service, Payment Service, etc)  │
└─────────────────────────────────────────┘
   ↑             ↑                 ↑
   │             │                 │
   │             │                 │
┌──────────────┐ ┌──────────────┐ ┌──────────────┐
│ HoneyDrunk.  │ │ HoneyDrunk.  │ │ HoneyDrunk.  │
│  Transport   │ │    Data      │ │   Kernel     │
└──────────────┘ └──────────────┘ └──────────────┘
       ↑               ↑                 ↑
       │               │                 │
       └───────────────┴─────────────────┘
                       │
              (All depend on Kernel)
```

Outbox implementations that integrate Data providers live either in application-level infrastructure projects or in dedicated bridge packages that depend on both Transport.Abstractions and the chosen Data provider.

```text
Example bridge package structure:
  depends on:
    - HoneyDrunk.Transport.Abstractions
    - HoneyDrunk.Data.SqlServer (or other provider)
```

In the future this may be extracted to a reusable package such as `HoneyDrunk.Transport.Outbox.SqlServer` once the pattern stabilizes.

### ✅ Allowed Package Dependencies

- **Transport → Kernel**: Transport uses `IGridContext`, correlation IDs, time abstractions
- **Data → Kernel**: Data uses base primitives
- **Outbox bridge packages → Transport.Abstractions + Data provider**: Bridge packages integrate both
- **Application Nodes → Kernel, Data, Transport**: Apps orchestrate all layers

### ❌ Forbidden Package Dependencies

- **Core Transport → Data**: Core transport stays storage-agnostic
- **Core Data → Transport**: Data has no messaging knowledge
- **Kernel → Data**: Kernel has no storage knowledge
- **Kernel → Transport**: Kernel has no messaging knowledge

**Key Principle**: Core Transport and core Data never reference each other directly. Integration happens either in application code or in dedicated bridge packages that depend on both.

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

**Exports (examples)**:
- Connection and session abstractions
  - `IConnectionFactory` / provider-specific factories
  - `IDbSession` / `IUnitOfWork` style abstraction
- Schema conventions
  - Optional helpers like `OutboxTableConventions` that define table and column names for shared infrastructure tables
- Migration hooks
  - `IMigrationContributor` for components that want to contribute DDL

**Does NOT Export**:
- Messaging abstractions (`IMessagePublisher`, `ITransportPublisher`, `IMessageHandler<T>`)
- Messaging behavior (`IOutboxStore`, dispatchers, handlers)
- Broker-specific knowledge

**Data's Role**: Provide the "database muscle" that other layers (including outbox bridge packages) can use to persist state and infrastructure tables.

**Example - Pure Storage Repository**:
```csharp
// Data layer - storage only, no messaging
namespace HoneyDrunk.Data.Repositories
{
    public class OrderRepository(IDbSession session) : IOrderRepository
    {
        public Task SaveAsync(Order order, CancellationToken ct) =>
            session.ExecuteAsync("INSERT INTO Orders ...", order);
        
        public Task<Order?> GetByIdAsync(Guid id, CancellationToken ct) =>
            session.QuerySingleOrDefaultAsync<Order>(
                "SELECT * FROM Orders WHERE Id = @Id", new { Id = id });
    }
}
```

---

### 🔷 HoneyDrunk.Transport

**Purpose**: Messaging and reliable delivery patterns

**Core Exports**:
- `ITransportPublisher` - Envelope-based publisher abstraction
- `ITransportConsumer` - Message consumer abstraction
- `IMessageHandler<T>` - Type-safe message processing
- `ITransportEnvelope` - Message wrapper with Grid context
- `EnvelopeFactory` - Envelope creation helpers
- `IOutboxStore`, `IOutboxDispatcher` - Contracts for transactional outbox

**Depends On**:
- **Kernel**: `IGridContext`, correlation IDs, time abstractions
- **No direct dependency on Data**: Core transport stays storage-agnostic

**Does NOT Export To Data**: Data never calls Transport APIs.

---

### 🔷 Bridge Packages / Application Infrastructure

**Purpose**: Integrate Transport and Data without coupling core packages

Outbox implementations that integrate Data providers live either in application-level infrastructure projects or in dedicated bridge packages that depend on both Transport.Abstractions and the chosen Data provider.

**Example Dependency Structure**:
```text
Application infrastructure (or future bridge package):
  depends on:
    - HoneyDrunk.Transport.Abstractions
    - HoneyDrunk.Data.SqlServer (or other provider)
```

These implementations provide `IOutboxStore` using Data's connection factories and conventions. In the future this pattern may be extracted to reusable packages once it stabilizes.

**Example Implementation** (lives in application infrastructure or bridge package, not core Transport):
```csharp
// Application.Infrastructure or future HoneyDrunk.Transport.Outbox.SqlServer
namespace MyApp.Infrastructure.Outbox
{
    public class SqlServerOutboxStore(
        ISqlConnectionFactory connectionFactory) : IOutboxStore
    {
        public async Task SaveAsync(OutboxMessage message, CancellationToken ct)
        {
            // Use Data's connection factory
            await using var connection = await connectionFactory.CreateConnectionAsync(ct);
            await using var transaction = await connection.BeginTransactionAsync(ct);
            
            // Use Data's conventions
            var tableName = OutboxTableConventions.GetFullTableName();
            var sql = $@"
                INSERT INTO {tableName} 
                ({OutboxTableConventions.GetIdColumnName()}, 
                 {OutboxTableConventions.GetPayloadColumnName()})
                VALUES (@Id, @Payload)";
            
            await connection.ExecuteAsync(sql, message, transaction);
            await transaction.CommitAsync(ct);
        }
    }
}
```

---

### 🔷 Application Nodes

**Purpose**: Business logic and domain services

**Uses**:
- **Kernel**: For `IGridContext`, correlation tracking
- **Data**: For persisting domain entities (via repositories)
- **Transport**: For publishing domain events, consuming messages

**Key Responsibility**: Application services orchestrate both persistence and messaging. Data does not publish messages.

**Example**:
```csharp
// Application layer - orchestrates Data and Transport
namespace HoneyDrunk.OrderService.Application
{
    using HoneyDrunk.Transport.Abstractions;

    public class OrderService(
        IOrderRepository repository,
        IMessagePublisher publisher)
    {
        public async Task CreateOrderAsync(
            Order order, 
            IGridContext context, 
            CancellationToken ct)
        {
            // 1. Use Data to persist
            await repository.SaveAsync(order, ct);

            // 2. Use Transport to publish a domain event
            await publisher.PublishAsync(
                destination: "orders.created",
                message: new OrderCreated(order.Id),
                gridContext: context,
                cancellationToken: ct);
        }
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

### ✅ Correct Approach (Bridge Package: Transport Outbox → Data Provider)

- Data provider exposes:
  - Connection/session abstractions
  - Schema conventions for shared tables (optional)

- Application infrastructure (or a future bridge package) implements:
  - `IOutboxStore` using the provider's connection factory and conventions
  - Optional dispatcher background service

Core Data and core Transport stay decoupled; the application infrastructure layer (or bridge package) is where they meet.

```csharp
// CORRECT: Data exposes storage primitives only
namespace HoneyDrunk.Data.SqlServer
{
    public interface ISqlConnectionFactory
    {
        Task<SqlConnection> CreateConnectionAsync(CancellationToken ct);
    }
    
    public static class OutboxTableConventions
    {
        public static string GetFullTableName() => "[messaging].[Outbox]";
        public static string GetIdColumnName() => "Id";
        public static string GetPayloadColumnName() => "Payload";
        public static string GetDestinationColumnName() => "Destination";
        public static string GetDispatchedAtColumnName() => "DispatchedAt";
    }
}

// CORRECT: Application infrastructure implements outbox using Data's primitives
namespace MyApp.Infrastructure.Outbox
{
    public class SqlServerOutboxStore(
        ISqlConnectionFactory connectionFactory) : IOutboxStore
    {
        public async Task SaveAsync(OutboxMessage message, CancellationToken ct)
        {
            // Use Data's connection factory
            var connection = await connectionFactory.CreateConnectionAsync(ct);
            
            // Use Data's conventions
            var table = OutboxTableConventions.GetFullTableName();
            var sql = $"INSERT INTO {table} ...";
            
            await connection.ExecuteAsync(sql, message);
        }
        
        public async Task<IEnumerable<OutboxMessage>> LoadPendingAsync(int limit, CancellationToken ct)
        {
            var connection = await connectionFactory.CreateConnectionAsync(ct);
            var table = OutboxTableConventions.GetFullTableName();
            var dispatchedCol = OutboxTableConventions.GetDispatchedAtColumnName();
            
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
- Transport defines "messaging brain" contracts (`IOutboxStore`, `IOutboxDispatcher`)
- Bridge package provides the implementation that wires them together
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

### 2. Naming Conventions (Optional)

```csharp
public static class OutboxTableConventions
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

// Application infrastructure (or future bridge package) provides the migration contributor
// (lives in application infrastructure, not core Transport)
public class OutboxMigrationContributor : IMigrationContributor
{
    public string Name => "Transport.Outbox";
    
    public Task<IEnumerable<string>> GetMigrationScriptsAsync(CancellationToken ct)
    {
        var scripts = new[]
        {
            $@"
            CREATE TABLE {OutboxTableConventions.GetFullTableName()} (
                {OutboxTableConventions.GetIdColumnName()} UNIQUEIDENTIFIER PRIMARY KEY,
                {OutboxTableConventions.GetPayloadColumnName()} VARBINARY(MAX) NOT NULL,
                {OutboxTableConventions.GetCreatedAtColumnName()} DATETIME2 NOT NULL,
                {OutboxTableConventions.GetDispatchedAtColumnName()} DATETIME2 NULL
            )"
        };
        
        return Task.FromResult<IEnumerable<string>>(scripts);
    }
}
```

---

## What Data Should NOT Expose

### ❌ Messaging Abstractions

Data should **never define** messaging abstractions. Those belong in Transport.

```csharp
// ❌ WRONG - Data defining messaging abstractions
namespace HoneyDrunk.Data
{
    public interface IMessagePublisher { }      // ❌ Don't define in Data
    public interface ITransportPublisher { }    // ❌ Don't define in Data
    public interface IMessageHandler<T> { }     // ❌ Don't define in Data
    public interface ITransportConsumer { }     // ❌ Don't define in Data
    public interface IOutboxStore { }           // ❌ Don't define in Data
}
```

### ❌ Messaging Behavior

```csharp
// WRONG - Dispatch logic belongs in Transport (or bridge packages)
namespace HoneyDrunk.Data
{
    public class OutboxDispatcher { }           // ❌
    public class OutboxMessage { }              // ❌ (Transport's DTO)
}
```

### ❌ Broker Knowledge

```csharp
// WRONG - Data should not know about brokers
namespace HoneyDrunk.Data
{
    public interface IServiceBusClient { }      // ❌
    public interface IStorageQueueClient { }    // ❌
    public class EnvelopeFactory { }            // ❌
}
```

### ❌ Publishing from Repositories

Data repositories should **not** publish messages. That's application layer responsibility.

```csharp
// ❌ WRONG - Repository publishing events
namespace HoneyDrunk.Data.Repositories
{
    public class OrderRepository(
        IDbSession session,
        IMessagePublisher publisher) // ❌ Don't inject publishers into Data
    {
        public async Task SaveAsync(Order order, CancellationToken ct)
        {
            await session.ExecuteAsync("INSERT INTO Orders ...", order);
            await publisher.PublishAsync(...); // ❌ Data should not publish
        }
    }
}

// ✅ CORRECT - Application layer orchestrates persistence and messaging
namespace HoneyDrunk.OrderService.Application
{
    public class OrderService(
        IOrderRepository repository,    // From Data
        IMessagePublisher publisher)    // From Transport
    {
        public async Task CreateOrderAsync(Order order, IGridContext context, CancellationToken ct)
        {
            await repository.SaveAsync(order, ct);           // Data persists
            await publisher.PublishAsync("orders.created",   // Transport publishes
                new OrderCreated(order.Id), context, ct);
        }
    }
}
```

---

## Summary

| Layer | Provides | Depends On | Does NOT Provide |
|-------|----------|------------|------------------|
| **Kernel** | Base primitives (`IGridContext`, `CorrelationId`, etc.) | — | Storage, messaging |
| **Data** | Storage primitives, provider conventions, migrations | Kernel | Messaging abstractions or behavior |
| **Transport** | Messaging abstractions, pipeline, outbox contracts | Kernel | Domain logic, storage implementations |
| **Bridge pkgs / App infra** | Outbox stores for specific databases | Transport.Abstractions, Data provider | Core abstractions |
| **Application** | Business logic, orchestration | Kernel, Data, Transport | Infrastructure primitives |

**Core Principle**: Core Transport and core Data never reference each other directly. Integration happens either in application code or in dedicated bridge packages that depend on both.

**Golden Rules**:
1. Data is the "database muscle" - connections, sessions, conventions
2. Transport is the "messaging brain" - publishers, consumers, handlers, outbox contracts
3. Bridge packages wire them together for specific database providers
4. Application services orchestrate Data (persistence) and Transport (messaging)

---

[← Back to File Guide](FILE_GUIDE.md) | [↑ Back to top](#table-of-contents)
