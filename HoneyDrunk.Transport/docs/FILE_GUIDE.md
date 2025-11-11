# 📦 HoneyDrunk.Transport - Complete File Guide

## Overview

**Think of this library as a postal service for your application**

Just like how the postal service lets you send letters without worrying about trucks, planes, or delivery routes, this library lets applications send messages without worrying about the underlying messaging technology.

---

## 🎯 HoneyDrunk.Transport (Core Project)

*The main postal service headquarters with all the rules and tools*

### 📋 Abstractions (Contracts/Interfaces)

*These define "what" things should do, not "how" they do it*

#### ITransportEnvelope.cs
- **What it is:** The "envelope" that wraps around every message
- **Real-world analogy:** Like a physical envelope with an address, return address, tracking number, and the letter inside
- **What it contains:** Message ID, who sent it, what type of message, and the actual content
- **How it's used:** Every message sent or received is wrapped in an envelope; you interact with it through `EnvelopeFactory.Create()`
- **Why it matters:** Provides distributed tracing (correlation IDs), message routing (MessageType), and metadata (Headers) without coupling to transport specifics
- **When to use:** You rarely construct this directly - use `EnvelopeFactory` instead. Access it in middleware or when inspecting messages
- **Example:**
  ```csharp
  // Reading envelope properties in a handler
  public async Task<MessageProcessingResult> HandleAsync(OrderCreated message, MessageContext context, CancellationToken ct)
  {
      var envelope = context.Envelope;
      _logger.LogInformation("Processing message {MessageId} with correlation {CorrelationId}", 
          envelope.MessageId, envelope.CorrelationId);
      // ... handle message
  }
  ```

#### ITransportPublisher.cs
- **What it is:** The "post office" that sends messages
- **Real-world analogy:** The counter where you drop off your letters
- **What it does:** Takes your message and sends it to the right destination
- **How it's used:** Injected into your services to publish messages; abstracts the underlying broker (Azure Service Bus, RabbitMQ, etc.)
- **Why it matters:** Decouples your application code from specific messaging infrastructure - swap transports without changing business logic
- **When to use:** Anytime you need to send a message to another service or component
- **Example:**
  ```csharp
  public class OrderService(ITransportPublisher publisher, EnvelopeFactory factory)
  {
      public async Task CreateOrderAsync(Order order, CancellationToken ct)
      {
          // Save order to database
          await _repository.SaveAsync(order, ct);
          
          // Publish event
          var message = new OrderCreated(order.Id, order.CustomerId);
          var envelope = factory.Create(message, "orders");
          await publisher.PublishAsync(envelope, ct);
      }
  ```
  
#### ITransportConsumer.cs
- **What it is:** The "mailbox" that receives messages
- **Real-world analogy:** Your home mailbox where letters arrive
- **What it does:** Listens for incoming messages and processes them
- **How it's used:** Registered by transport implementations (Azure Service Bus, InMemory) during startup; runs as background service
- **Why it matters:** Handles message polling/listening and invokes the pipeline for processing
- **When to use:** Implemented by transport providers, not typically used directly in application code
- **Example:**
  ```csharp
  // Transport implementations use this
  services.AddHoneyDrunkTransportCore()
      .AddHoneyDrunkServiceBusTransport(options => /* ... */)
      .WithTopicSubscription("my-subscription"); // Configures consumer
  ```

#### IMessageHandler.cs
- **What it is:** The "mail handler" for specific types of messages
- **Real-world analogy:** Like having different people handle bills vs. birthday cards
- **What it does:** Defines how to process a specific type of message when it arrives
- **How it's used:** Implement `IMessageHandler<TMessage>` for each message type you want to handle
- **Why it matters:** Type-safe message handling with automatic deserialization and routing
- **When to use:** Create one handler per message type that your service needs to process
- **Example:**
  ```csharp
  public class OrderCreatedHandler(ILogger<OrderCreatedHandler> logger) : IMessageHandler<OrderCreated>
  {
      public async Task<MessageProcessingResult> HandleAsync(
          OrderCreated message, 
          MessageContext context, 
          CancellationToken ct)
      {
          logger.LogInformation("Order {OrderId} created by customer {CustomerId}", 
              message.OrderId, message.CustomerId);
          
          // Process the order
          await ProcessOrderAsync(message, ct);
          
          return MessageProcessingResult.Success;
      }
  }
  
  // Register in DI
  services.AddMessageHandler<OrderCreated, OrderCreatedHandler>();
  ```

#### IMessageSerializer.cs
- **What it is:** The "translator" for messages
- **Real-world analogy:** Converting your spoken words into written text
- **What it does:** Converts objects into bytes (for sending) and bytes back into objects (for receiving)
- **How it's used:** Automatically invoked by the publisher/consumer to serialize/deserialize message payloads
- **Why it matters:** Allows pluggable serialization (JSON, Protobuf, MessagePack) without changing message handling code
- **When to use:** Use the default `JsonMessageSerializer` or implement custom for different formats (e.g., Protobuf for performance)
- **Example:**
  ```csharp
  // Using default JSON serializer (automatic)
  services.AddHoneyDrunkTransportCore(); // Registers JsonMessageSerializer
  
  // Custom serializer
  public class ProtobufSerializer : IMessageSerializer
  {
      public ReadOnlyMemory<byte> Serialize<T>(T message) where T : class
      {
          // Protobuf serialization logic
      }
      
      public T Deserialize<T>(ReadOnlyMemory<byte> data) where T : class
      {
          // Protobuf deserialization logic
      }
  }
  
  // Register custom
  services.AddSingleton<IMessageSerializer, ProtobufSerializer>();
  ```

#### ITransportTransaction.cs
- **What it is:** The "tracking system" for messages
- **Real-world analogy:** Like certified mail with tracking and confirmation
- **What it does:** Ensures messages are processed reliably with commit/rollback capabilities
- **How it's used:** Available in `MessageContext.Transaction` - commit on success, rollback on failure
- **Why it matters:** Enables reliable message processing - uncommitted messages return to queue for retry
- **When to use:** Transports that support transactions (Azure Service Bus sessions); automatic handling by pipeline
- **Example:**
  ```csharp
  public async Task<MessageProcessingResult> HandleAsync(OrderCreated message, MessageContext context, CancellationToken ct)
  {
      try
      {
          await ProcessOrderAsync(message, ct);
          await context.Transaction.CommitAsync(ct); // Mark as processed
          return MessageProcessingResult.Success;
      }
      catch (Exception ex)
      {
          await context.Transaction.RollbackAsync(ct); // Return to queue
          return MessageProcessingResult.Retry;
      }
  }
  ```

#### NoOpTransportTransaction.cs
- **What it is:** A "fake" transaction for when you don't need tracking
- **Real-world analogy:** Like regular mail without tracking
- **What it does:** Does nothing, but satisfies the requirement to have a transaction
- **How it's used:** Default transaction for non-transactional transports (InMemory, standard queues)
- **Why it matters:** Allows transaction-aware code to work with non-transactional transports
- **When to use:** Automatically used by transports without transaction support - you don't typically use this directly

#### IEndpointAddress.cs / EndpointAddress.cs
- **What it is:** The "mailing address" for messages
- **Real-world analogy:** Like "123 Main St, City, State, ZIP"
- **What it does:** Identifies where messages should go
- **How it's used:** Passed to `PublishAsync()` to specify destination topic/queue
- **Why it matters:** Type-safe routing without hardcoding queue names throughout code
- **When to use:** When publishing to specific destinations or configuring consumers
- **Example:**
  ```csharp
  var destination = new EndpointAddress("orders.created");
  var envelope = factory.Create(message, destination);
  await publisher.PublishAsync(envelope, ct);
  
  // Or use string overload (converts to EndpointAddress internally)
  var envelope2 = factory.Create(message, "orders.created");
  ```

#### IMessageReceiver.cs
- **What it is:** The "mail carrier" that delivers to handlers
- **Real-world analogy:** The postal worker who puts mail in your mailbox
- **What it does:** Receives messages and routes them to the right handler
- **How it's used:** Internal component used by consumers to dispatch messages through the pipeline
- **Why it matters:** Coordinates handler resolution, pipeline execution, and error handling
- **When to use:** Implemented internally - not typically used directly in application code

#### MessageContext.cs
- **What it is:** The "delivery slip" with extra information
- **Real-world analogy:** Like the metadata on a package (weight, date, sender notes)
- **What it does:** Carries extra information about the message during processing
- **How it's used:** Passed to handlers and middleware; access envelope, transaction, cancellation token, custom properties
- **Why it matters:** Provides context for decision-making (retry attempts, tracing info, custom metadata)
- **When to use:** Access in handlers/middleware to read envelope metadata or store custom state
- **Example:**
  ```csharp
  public async Task<MessageProcessingResult> HandleAsync(OrderCreated message, MessageContext context, CancellationToken ct)
  {
      // Access envelope
      var messageId = context.Envelope.MessageId;
      
      // Store custom state for downstream middleware/handlers
      context.Properties["ProcessedBy"] = "OrderService";
      
      // Access transaction
      await context.Transaction.CommitAsync(ct);
      
      return MessageProcessingResult.Success;
  }
  ```

#### MessageProcessingResult.cs
- **What it is:** The "delivery status report"
- **Real-world analogy:** "Delivered successfully" vs. "Address unknown - return to sender"
- **What it does:** Tells the system what happened: Success, Retry, or DeadLetter (failed)
- **How it's used:** Return from handlers to indicate outcome; determines if message is completed, retried, or moved to DLQ
- **Why it matters:** Controls message lifecycle and retry behavior at the handler level
- **When to use:** Return appropriate result from every handler based on processing outcome
- **Example:**
  ```csharp
  public async Task<MessageProcessingResult> HandleAsync(OrderCreated message, MessageContext context, CancellationToken ct)
  {
      try
      {
          await ProcessOrderAsync(message, ct);
          return MessageProcessingResult.Success; // Remove from queue
      }
      catch (TransientException ex) // Database timeout, network issue
      {
          _logger.LogWarning(ex, "Transient error, will retry");
          return MessageProcessingResult.Retry; // Return to queue for retry
      }
      catch (PoisonMessageException ex) // Invalid data, can't be processed
      {
          _logger.LogError(ex, "Poison message, moving to dead letter");
          return MessageProcessingResult.DeadLetter; // Move to DLQ, no retry
      }
  }
  ```

#### MessageHandler.cs
- **What it is:** A base class for message handlers
- **Real-world analogy:** A template for how to handle different types of mail
- **What it does:** Provides common functionality for all message handlers
- **How it's used:** Optionally inherit from this instead of implementing `IMessageHandler<T>` directly for shared logic
- **Why it matters:** Reduces boilerplate for common handler patterns (logging, error handling)
- **When to use:** When you want shared behavior across handlers (e.g., automatic error logging)
- **Example:**
  ```csharp
  public abstract class LoggingMessageHandler<T>(ILogger logger) : MessageHandler<T> where T : class
  {
      protected override async Task<MessageProcessingResult> HandleAsync(T message, MessageContext context, CancellationToken ct)
      {
          logger.LogInformation("Handling {MessageType}", typeof(T).Name);
          try
          {
              var result = await HandleCoreAsync(message, context, ct);
              logger.LogInformation("Handled {MessageType} with result {Result}", typeof(T).Name, result);
              return result;
          }
          catch (Exception ex)
          {
              logger.LogError(ex, "Error handling {MessageType}", typeof(T).Name);
              throw;
          }
      }
      
      protected abstract Task<MessageProcessingResult> HandleCoreAsync(T message, MessageContext context, CancellationToken ct);
  }
  ```

---

### 🔧 Primitives (Building Blocks)

*Basic pieces that everything else uses*

#### TransportEnvelope.cs
- **What it is:** The actual implementation of the envelope
- **Real-world analogy:** The physical envelope with all its properties filled in
- **What it does:** Stores message ID, type, headers, payload, and correlation info
- **How it's used:** Created by `EnvelopeFactory`; immutable with `With*()` methods for modifications
- **Why it matters:** Immutable design prevents accidental mutations; rich metadata supports observability
- **When to use:** Access properties in handlers/middleware; create modified copies with `WithHeaders()` or `WithCorrelation()`
- **Example:**
  ```csharp
  // Add custom headers to envelope
  var originalEnvelope = factory.Create(message, "orders");
  var enrichedEnvelope = originalEnvelope.WithHeaders(new Dictionary<string, string>
  {
      ["TenantId"] = "tenant-123",
      ["Priority"] = "high"
  });
  await publisher.PublishAsync(enrichedEnvelope, ct);
  
  // Read headers in handler
  if (context.Envelope.Headers.TryGetValue("TenantId", out var tenantId))
  {
      _logger.LogInformation("Processing for tenant {TenantId}", tenantId);
  }
  ```

#### EnvelopeFactory.cs
- **What it is:** The "envelope maker"
- **Real-world analogy:** Like a machine that pre-prints envelopes with tracking numbers
- **What it does:** Creates new envelopes with auto-generated IDs and timestamps
- **How it's used:** Inject and call `Create()` to wrap messages; depends on `IIdGenerator` and `IClock` from Kernel
- **Why it matters:** Ensures consistent ID generation and timestamping; testable via Kernel abstractions
- **When to use:** Always use this to create envelopes - never construct `TransportEnvelope` directly
- **Example:**
  ```csharp
  public class OrderService(EnvelopeFactory factory, ITransportPublisher publisher)
  {
      public async Task PublishOrderCreatedAsync(Order order, CancellationToken ct)
      {
          var message = new OrderCreated(order.Id, order.CustomerId);
          
          // Factory generates unique MessageId and Timestamp
          var envelope = factory.Create(message, "orders.created");
          
          // Optional: set correlation for request tracing
          var correlatedEnvelope = envelope.WithCorrelation(
              correlationId: _contextAccessor.CorrelationId,
              causationId: _contextAccessor.RequestId
          );
          
          await publisher.PublishAsync(correlatedEnvelope, ct);
      }
  }
  ```

---

### ⚙️ Configuration (Settings)

*All the knobs and switches to control behavior*

#### TransportCoreOptions.cs
- **What it is:** The main settings panel
- **Real-world analogy:** The control panel for your postal service
- **What it does:** Enables/disables telemetry, logging, correlation tracking
- **How it's used:** Configure via `IOptions<TransportCoreOptions>` or builder pattern
- **Why it matters:** Controls built-in middleware (correlation, telemetry, logging) without code changes
- **When to use:** Configure during startup to enable/disable features globally
- **Example:**
  ```csharp
  services.AddHoneyDrunkTransportCore(options =>
  {
      options.EnableTelemetry = true;  // OpenTelemetry integration
      options.EnableLogging = true;    // Structured logging
      options.EnableCorrelation = true; // Correlation ID propagation
  });
  ```

#### TransportOptions.cs
- **What it is:** General transport settings
- **Real-world analogy:** Basic settings like "how long to keep messages"
- **What it does:** Configuration for transport-specific behaviors
- **How it's used:** Extended by specific transport implementations (Azure Service Bus, etc.)
- **Why it matters:** Provides common configuration baseline for all transports
- **When to use:** Base for transport-specific options classes

#### RetryOptions.cs
- **What it is:** The "retry policy" settings
- **Real-world analogy:** "Try to deliver 3 times, wait 1 second, then 2, then 4..."
- **What it does:** Controls how many times to retry failed messages and how long to wait
- **How it's used:** Configure via builder `.WithRetry()` or options binding
- **Why it matters:** Handles transient failures (network blips, temporary outages) without manual intervention
- **When to use:** Always configure retry for production - prevents message loss during temporary failures
- **Example:**
  ```csharp
  services.AddHoneyDrunkTransportCore()
      .AddHoneyDrunkServiceBusTransport(options => /* ... */)
      .WithRetry(retry =>
      {
          retry.MaxAttempts = 5;
          retry.InitialDelay = TimeSpan.FromSeconds(1);
          retry.BackoffStrategy = BackoffStrategy.Exponential; // 1s, 2s, 4s, 8s, 16s
          retry.MaxDelay = TimeSpan.FromMinutes(1); // Cap exponential growth
      });
  ```

#### BackoffStrategy.cs
- **What it is:** The "wait time calculator" strategies
- **Real-world analogy:** Fixed wait, or increasing wait times (1 sec, 2 sec, 4 sec...)
- **What it does:** Defines: Fixed, Linear, or Exponential backoff
- **How it's used:** Set in `RetryOptions.BackoffStrategy`
- **Why it matters:** Controls retry timing - exponential prevents overwhelming failing services
- **When to use:** 
  - **Fixed**: Predictable retry timing (testing, simple scenarios)
  - **Linear**: Gradual increase (moderate load)
  - **Exponential**: Rapid backoff (production, avoid thundering herd)
- **Example:**
  ```csharp
  // Fixed: 1s, 1s, 1s, 1s, 1s
  retry.BackoffStrategy = BackoffStrategy.Fixed;
  
  // Linear: 1s, 2s, 3s, 4s, 5s
  retry.BackoffStrategy = BackoffStrategy.Linear;
  
  // Exponential: 1s, 2s, 4s, 8s, 16s (best for production)
  retry.BackoffStrategy = BackoffStrategy.Exponential;
  ```

#### IErrorHandlingStrategy.cs
- **What it is:** The "error response plan"
- **Real-world analogy:** "What do we do when mail can't be delivered?"
- **What it does:** Decides whether to retry, dead-letter, or ignore errors
- **How it's used:** Implement custom error handling logic based on exception type/context
- **Why it matters:** Allows sophisticated error handling (e.g., retry transient, DLQ permanent errors)
- **When to use:** Implement custom when default retry logic isn't sufficient
- **Example:**
  ```csharp
  public class CustomErrorStrategy : IErrorHandlingStrategy
  {
      public ErrorHandlingDecision Decide(Exception exception, MessageContext context, int attemptCount)
      {
          return exception switch
          {
              SqlException { Number: -2 } => // Timeout
                  new ErrorHandlingDecision(ErrorHandlingAction.Retry, TimeSpan.FromSeconds(5)),
              
              ValidationException => // Bad data
                  new ErrorHandlingDecision(ErrorHandlingAction.DeadLetter),
              
              _ => attemptCount < 3 
                  ? new ErrorHandlingDecision(ErrorHandlingAction.Retry, TimeSpan.FromSeconds(attemptCount * 2))
                  : new ErrorHandlingDecision(ErrorHandlingAction.DeadLetter)
          };
      }
  }
  
  services.AddSingleton<IErrorHandlingStrategy, CustomErrorStrategy>();
  ```

#### ErrorHandlingAction.cs
- **What it is:** The possible error actions
- **Real-world analogy:** "Retry", "Return to sender", or "Destroy"
- **What it does:** Enum defining possible error handling actions
- **How it's used:** Returned by `IErrorHandlingStrategy` to indicate desired action
- **Why it matters:** Type-safe error handling decisions
- **When to use:** Use in custom error handling strategies

#### ErrorHandlingDecision.cs
- **What it is:** The decision object for errors
- **Real-world analogy:** A delivery slip saying what to do with undeliverable mail
- **What it does:** Contains the action to take and optional delay
- **How it's used:** Returned by `IErrorHandlingStrategy.Decide()`
- **Why it matters:** Couples action with delay for time-based retry strategies
- **When to use:** Return from custom error handling strategies

---

### 🔄 Pipeline (Processing Chain)

*The assembly line that processes messages*

#### IMessagePipeline.cs / MessagePipeline.cs
- **What it is:** The message processing assembly line
- **Real-world analogy:** Like a factory conveyor belt where each station does something
- **What it does:** Runs messages through middleware, then to handlers
- **How it's used:** Automatically invoked by consumers; middleware registered via DI wraps handlers in onion pattern
- **Why it matters:** Provides cross-cutting concerns (logging, telemetry, retry) without modifying handlers
- **When to use:** Register custom middleware during startup; pipeline execution is automatic
- **Example:**
  ```csharp
  // Pipeline execution order (automatic):
  // Correlation → Telemetry → Logging → CustomMiddleware1 → CustomMiddleware2 → Handler
  
  // Register custom middleware
  services.AddMessageMiddleware<AuthorizationMiddleware>();
  services.AddMessageMiddleware<ValidationMiddleware>();
  
  // Middleware wraps in reverse registration order (LIFO)
  ```

#### IMessageMiddleware.cs / MessageMiddleware.cs
- **What it is:** One station on the assembly line
- **Real-world analogy:** Like quality control stations on a factory line
- **What it does:** Intercepts messages before they reach handlers (logging, validation, etc.)
- **How it's used:** Implement `IMessageMiddleware` and register with `AddMessageMiddleware<T>()`
- **Why it matters:** DRY principle - implement cross-cutting concerns once instead of in every handler
- **When to use:** For logic that applies to multiple message types (auth, validation, enrichment)
- **Example:**
  ```csharp
  public class TenantResolutionMiddleware(ITenantResolver resolver) : IMessageMiddleware
  {
      public async Task InvokeAsync(
          ITransportEnvelope envelope,
          MessageContext context,
          Func<Task> next,
          CancellationToken ct)
      {
          // Extract tenant from headers
          if (envelope.Headers.TryGetValue("TenantId", out var tenantId))
          {
              var tenant = await resolver.ResolveAsync(tenantId, ct);
              context.Properties["Tenant"] = tenant;
          }
          
          // Continue pipeline
          await next();
      }
  }
  
  // Register
  services.AddMessageMiddleware<TenantResolutionMiddleware>();
  ```

#### MessageHandlerException.cs
- **What it is:** A special error for message handling problems
- **Real-world analogy:** A "delivery failed" notice with reason
- **What it does:** Wraps exceptions with a processing result
- **How it's used:** Throw from handlers to control error handling without try/catch in pipeline
- **Why it matters:** Allows handlers to signal retry/dead-letter intent via exceptions
- **When to use:** When you want to throw an exception but control the processing result
- **Example:**
  ```csharp
  public async Task<MessageProcessingResult> HandleAsync(OrderCreated message, MessageContext context, CancellationToken ct)
  {
      if (message.OrderId <= 0)
      {
          // Throw with explicit dead-letter intent
          throw new MessageHandlerException(
              "Invalid OrderId - cannot be zero or negative",
              MessageProcessingResult.DeadLetter);
      }
      
      try
      {
          await ProcessOrderAsync(message, ct);
          return MessageProcessingResult.Success;
      }
      catch (DbException ex) when (ex.IsTransient)
      {
          // Throw with retry intent
          throw new MessageHandlerException("Database timeout", ex, MessageProcessingResult.Retry);
      }
  }
  ```

#### Pipeline/Middleware (Built-in Assembly Line Stations)

**CorrelationMiddleware.cs**
- **What it is:** The "tracking number manager"
- **Real-world analogy:** Stamps tracking numbers on packages
- **What it does:** Adds correlation IDs to track messages across systems
- **How it's used:** Automatically registered when `TransportCoreOptions.EnableCorrelation = true`
- **Why it matters:** Enables distributed tracing across service boundaries
- **When to use:** Enable in production for request tracing and debugging
- **Example:**
  ```csharp
  // Correlation flow:
  // 1. Service A publishes with CorrelationId "abc-123"
  // 2. Service B receives message, CorrelationMiddleware extracts "abc-123"
  // 3. Service B logs with "abc-123", all logs correlated
  // 4. Service B publishes new message with same CorrelationId
  // 5. Entire request chain traceable via "abc-123"
  
  // Access in handler
  var correlationId = context.Envelope.CorrelationId;
  _logger.LogInformation("Processing order in correlation {CorrelationId}", correlationId);
  ```

**LoggingMiddleware.cs**
- **What it is:** The "activity logger"
- **Real-world analogy:** Security camera recording all deliveries
- **What it does:** Logs when messages arrive, succeed, or fail
- **How it's used:** Automatically registered when `TransportCoreOptions.EnableLogging = true`
- **Why it matters:** Automatic structured logging for all messages without handler changes
- **When to use:** Enable in production for observability and troubleshooting
- **Example:**
  ```csharp
  // Automatic logs (no code needed):
  // [INFO] Received message OrderCreated with ID abc-123
  // [INFO] Successfully processed OrderCreated with ID abc-123 in 45ms
  // [ERROR] Failed to process OrderCreated with ID abc-123: Database timeout
  
  services.AddHoneyDrunkTransportCore(options =>
  {
      options.EnableLogging = true; // Enables LoggingMiddleware
  });
  ```

**RetryMiddleware.cs**
- **What it is:** The "retry coordinator"
- **Real-world analogy:** Automatically re-attempts failed deliveries
- **What it does:** Catches failures and retries messages based on policy
- **How it's used:** Automatically registered when retry configuration is present
- **Why it matters:** Resilience against transient failures without manual retry logic
- **When to use:** Always configure for production - handles network blips, temporary outages
- **Example:**
  ```csharp
  // Retry happens automatically based on MessageProcessingResult
  public async Task<MessageProcessingResult> HandleAsync(OrderCreated message, MessageContext context, CancellationToken ct)
  {
      try
      {
          await ProcessOrderAsync(message, ct);
          return MessageProcessingResult.Success; // No retry
      }
      catch (TimeoutException)
      {
          return MessageProcessingResult.Retry; // RetryMiddleware handles backoff
      }
  }
  
  // Configure retry policy
  services.AddHoneyDrunkTransportCore()
      .WithRetry(retry =>
      {
          retry.MaxAttempts = 3;
          retry.BackoffStrategy = BackoffStrategy.Exponential;
      });
  ```

---

### 📊 Telemetry (Monitoring)

*The dashboard that shows what's happening*

#### TransportTelemetry.cs
- **What it is:** The monitoring system
- **Real-world analogy:** Like UPS tracking showing where packages are
- **What it does:** Records metrics, traces, and events for observability
- **How it's used:** Static helper for creating OpenTelemetry activities; automatically called by telemetry middleware
- **Why it matters:** Integrates with APM tools (Application Insights, Jaeger, Zipkin) for distributed tracing
- **When to use:** Enable via `TransportCoreOptions.EnableTelemetry = true`; automatic in middleware/consumers
- **Example:**
  ```csharp
  // Automatic telemetry when enabled:
  // - Activity spans for publish/consume operations
  // - Metrics: message count, processing duration, error rate
  // - Traces: distributed tracing across services
  
  services.AddHoneyDrunkTransportCore(options =>
  {
      options.EnableTelemetry = true; // Integrates with OpenTelemetry
  });
  
  // In Application Insights:
  // ├─ transport.publish (45ms)
  // │  └─ transport.consume (120ms)
  // │     ├─ transport.middleware.correlation (2ms)
  // │     ├─ transport.middleware.telemetry (1ms)
  // │     └─ transport.handler.OrderCreated (115ms)
  ```

#### TelemetryMiddleware.cs
- **What it is:** The telemetry station in the pipeline
- **Real-world analogy:** The scanner that beeps when packages pass through
- **What it does:** Automatically records telemetry for every message
- **How it's used:** Automatically registered when `TransportCoreOptions.EnableTelemetry = true`
- **Why it matters:** Zero-code instrumentation for all message processing
- **When to use:** Enable in production for APM integration

---

### 📤 Outbox (Transactional Messaging)

*The "guarantee delivery" system*

#### IOutboxStore.cs
- **What it is:** The database for storing messages before sending
- **Real-world analogy:** A safe where you store important letters until the post office opens
- **What it does:** Saves messages in your database as part of a transaction
- **How it's used:** Implement against your database (EF Core, Dapper, etc.); save messages in same transaction as business logic
- **Why it matters:** Guarantees exactly-once message delivery even if publish fails after database commit
- **When to use:** Critical workflows where message loss is unacceptable (payments, orders, account changes)
- **Example:**
  ```csharp
  // Entity Framework implementation
  public class EfCoreOutboxStore(ApplicationDbContext db) : IOutboxStore
  {
      public async Task SaveAsync(IOutboxMessage message, CancellationToken ct)
      {
          await db.OutboxMessages.AddAsync(new OutboxMessageEntity
          {
              MessageId = message.MessageId,
              Payload = message.Payload.ToArray(),
              // ... map other properties
          }, ct);
          // Don't call SaveChangesAsync - parent transaction handles it
      }
      
      public async Task<IEnumerable<IOutboxMessage>> LoadPendingAsync(int batchSize, CancellationToken ct)
      {
          return await db.OutboxMessages
              .Where(m => m.State == OutboxMessageState.Pending)
              .Take(batchSize)
              .ToListAsync(ct);
      }
      
      // ... other methods
  }
  
  // Usage in service
  public class OrderService(ApplicationDbContext db, IOutboxStore outbox, EnvelopeFactory factory)
  {
      public async Task CreateOrderAsync(Order order, CancellationToken ct)
      {
          using var transaction = await db.Database.BeginTransactionAsync(ct);
          
          // Save order
          db.Orders.Add(order);
          
          // Save message to outbox (same transaction)
          var message = new OrderCreated(order.Id, order.CustomerId);
          var envelope = factory.Create(message, "orders.created");
          await outbox.SaveAsync(new OutboxMessage(envelope), ct);
          
          await db.SaveChangesAsync(ct);
          await transaction.CommitAsync(ct);
          
          // Background dispatcher will publish later
      }
  }
  ```

#### IOutboxDispatcher.cs
- **What it is:** The background worker that sends stored messages
- **Real-world analogy:** A postal worker who checks the safe and mails everything
- **What it does:** Polls the store and publishes pending messages
- **How it's used:** Run as background service (IHostedService); polls store at intervals
- **Why it matters:** Completes the outbox pattern by ensuring messages eventually get published
- **When to use:** Required when using outbox pattern; typically one instance per application
- **Example:**
  ```csharp
  public class OutboxDispatcherService(
      IOutboxStore store,
      ITransportPublisher publisher,
      ILogger<OutboxDispatcherService> logger) : BackgroundService
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
                      await publisher.PublishAsync(message.Envelope, ct);
                      await store.MarkDispatchedAsync(message.MessageId, ct);
                  }
              }
              catch (Exception ex)
              {
                  logger.LogError(ex, "Error dispatching outbox messages");
              }
              
              await Task.Delay(TimeSpan.FromSeconds(5), ct); // Poll interval
          }
      }
  }
  
  // Register
  services.AddHostedService<OutboxDispatcherService>();
  ```

#### IOutboxMessage.cs / OutboxMessage.cs
- **What it is:** A message stored in the outbox
- **Real-world analogy:** A letter in the safe with a sticky note about when to send it
- **What it does:** Wraps a message with state (pending, dispatched, failed)
- **How it's used:** Created when saving to outbox; tracks lifecycle through states
- **Why it matters:** Tracks message state for reliable delivery and failure handling
- **When to use:** Created automatically when using outbox pattern

#### OutboxMessageState.cs
- **What it is:** The state of a stored message
- **Real-world analogy:** "Waiting to send", "Sent", "Failed", "Poisoned (give up)"
- **What it does:** Enum tracking message lifecycle in the outbox
- **How it's used:** Updated by dispatcher as messages are processed
- **Why it matters:** Prevents infinite retry loops (Poisoned state after max attempts)
- **When to use:** Track in your outbox store implementation
- **Example:**
  ```csharp
  public enum OutboxMessageState
  {
      Pending,    // Waiting to be dispatched
      Dispatched, // Successfully published
      Failed,     // Temporary failure, will retry
      Poisoned    // Permanent failure after max retries exceeded
  }
  
  // State transitions:
  // Pending → Dispatched (success)
  // Pending → Failed → Pending (retry)
  // Pending → Failed → Poisoned (max retries exceeded)
  ```

---

### 🔌 DependencyInjection (Setup Helpers)

*The toolbox for setting up the postal service*

#### ServiceCollectionExtensions.cs
- **What it is:** The "setup wizard"
- **Real-world analogy:** Like a guided setup for installing software
- **What it does:** Provides easy methods to configure the transport system
- **How it's used:** Call extension methods on `IServiceCollection` during startup
- **Why it matters:** Fluent API makes configuration discoverable and type-safe
- **When to use:** Every application using HoneyDrunk.Transport starts here
- **Example:**
  ```csharp
  // Program.cs or Startup.cs
  services.AddHoneyDrunkTransportCore(options =>
  {
      options.EnableTelemetry = true;
      options.EnableLogging = true;
      options.EnableCorrelation = true;
  })
  .AddHoneyDrunkServiceBusTransport(options =>
  {
      options.ConnectionString = configuration["AzureServiceBus:ConnectionString"];
      options.TopicName = "orders";
  })
  .WithTopicSubscription("order-processor")
  .WithRetry(retry =>
  {
      retry.MaxAttempts = 5;
      retry.BackoffStrategy = BackoffStrategy.Exponential;
  });
  
  // Register handlers
  services.AddMessageHandler<OrderCreated, OrderCreatedHandler>();
  services.AddMessageHandler<OrderCancelled, OrderCancelledHandler>();
  ```

#### ITransportBuilder.cs / TransportBuilder.cs
- **What it is:** The "configuration builder"
- **Real-world analogy:** A form you fill out step-by-step to configure your postal service
- **What it does:** Fluent API for chaining configuration calls
- **How it's used:** Returned by `AddHoneyDrunkTransportCore()` for method chaining
- **Why it matters:** Enables fluent configuration pattern
- **When to use:** Automatic - returned by setup methods

#### DelegateMessageHandler.cs
- **What it is:** A quick way to handle messages with a function
- **Real-world analogy:** "Just tell me what to do when this type of mail arrives"
- **What it does:** Wraps a simple function as a message handler
- **How it's used:** Use `AddMessageHandler<T>()` overload with lambda/function
- **Why it matters:** Quick prototyping without creating full handler classes
- **When to use:** Simple handlers, prototyping, or when handler logic is trivial
- **Example:**
  ```csharp
  // Delegate handler (no class needed)
  services.AddMessageHandler<OrderCreated>(async (message, context, ct) =>
  {
      _logger.LogInformation("Order {OrderId} created", message.OrderId);
      await ProcessOrderAsync(message, ct);
      return MessageProcessingResult.Success;
  });
  
  // vs. Full handler class
  public class OrderCreatedHandler : IMessageHandler<OrderCreated>
  {
      public async Task<MessageProcessingResult> HandleAsync(/* ... */) { /* ... */ }
  }
  services.AddMessageHandler<OrderCreated, OrderCreatedHandler>();
  ```

#### DelegateMessageMiddleware.cs
- **What it is:** A quick way to add middleware with a function
- **Real-world analogy:** "Add this step to the assembly line"
- **What it does:** Wraps a simple function as middleware
- **How it's used:** Use `AddMessageMiddleware()` with lambda/function
- **Why it matters:** Quick middleware without creating full classes
- **When to use:** Simple cross-cutting concerns or prototyping
- **Example:**
  ```csharp
  // Delegate middleware (no class needed)
  services.AddMessageMiddleware(async (envelope, context, next, ct) =>
  {
      var sw = Stopwatch.StartNew();
      await next();
      _logger.LogInformation("Processing took {ElapsedMs}ms", sw.ElapsedMilliseconds);
  });
  ```

#### NoOpMiddleware.cs
- **What it is:** A middleware that does nothing
- **Real-world analogy:** An empty station on the assembly line
- **What it does:** Placeholder middleware when you need one but it doesn't do anything
- **How it's used:** Internal/testing - you typically won't use this directly
- **Why it matters:** Satisfies middleware requirements in tests or optional configurations
- **When to use:** Testing or when conditional middleware is disabled

#### JsonMessageSerializer.cs
- **What it is:** JSON-based message translator
- **Real-world analogy:** Converts messages to/from JSON format
- **What it does:** Default serializer using System.Text.Json
- **How it's used:** Automatically registered; override with custom serializer if needed
- **Why it matters:** Human-readable format, widely compatible, good default choice
- **When to use:** Default for most scenarios; replace with Protobuf/MessagePack for performance-critical systems
- **Example:**
  ```csharp
  // Default (automatic)
  services.AddHoneyDrunkTransportCore(); // Uses JsonMessageSerializer
  
  // Custom JSON options
  services.AddSingleton<IMessageSerializer>(sp =>
      new JsonMessageSerializer(new JsonSerializerOptions
      {
          PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
          WriteIndented = false
      }));
  ```

---

### 🧠 Context (Application State)

*System for carrying information through the pipeline*

#### IKernelContextFactory.cs / KernelContextFactory.cs
- **What it is:** Factory for creating execution contexts
- **Real-world analogy:** Like a form that gets passed along with each package
- **What it does:** Creates context objects that carry state through message processing
- **How it's used:** Internal component creating `MessageContext` instances
- **Why it matters:** Integrates with HoneyDrunk.Kernel for consistent context propagation
- **When to use:** Used internally - not typically interacted with directly

---

## 🧪 HoneyDrunk.Transport.InMemory

*The "toy postal service" for testing*

### InMemoryBroker.cs
- **What it is:** A fake message broker that runs in memory
- **Real-world analogy:** Like a toy postal system for testing - mail goes in one end, comes out the other
- **What it does:** Manages queues and subscriptions in memory without external services
- **How it's used:** Registered via `AddHoneyDrunkInMemoryTransport()`; runs in same process
- **Why it matters:** Fast integration tests without Azure Service Bus or other infrastructure
- **When to use:** Unit/integration tests, local development, CI/CD pipelines
- **Example:**
  ```csharp
  // Test setup
  services.AddHoneyDrunkTransportCore()
      .AddHoneyDrunkInMemoryTransport(); // No connection strings needed
  
  // Test can publish and consume in-process
  [Fact]
  public async Task ProcessesOrderCreatedMessage()
  {
      await _publisher.PublishAsync(envelope, ct);
      await Task.Delay(100); // Give consumer time to process
      
      // Assert handler was called
      _mockHandler.Verify(h => h.HandleAsync(It.IsAny<OrderCreated>(), It.IsAny<MessageContext>(), It.IsAny<CancellationToken>()));
  }
  ```

### InMemoryTransportPublisher.cs
- **What it is:** The "post office" for the in-memory system
- **Real-world analogy:** The send counter at the toy post office
- **What it does:** Publishes messages to the in-memory broker
- **How it's used:** Injected as `ITransportPublisher`; identical API to real transports
- **Why it matters:** Test code is identical to production code
- **When to use:** Automatically used when InMemory transport is registered

### InMemoryTransportConsumer.cs
- **What it is:** The "mailbox" for the in-memory system
- **Real-world analogy:** The receive station at the toy post office
- **What it does:** Consumes messages from the in-memory broker
- **How it's used:** Runs as background service, processes messages through pipeline
- **Why it matters:** Full pipeline execution in tests (middleware, handlers, etc.)
- **When to use:** Automatically started when InMemory transport is registered

### DependencyInjection/ServiceCollectionExtensions.cs
- **What it is:** Setup wizard for in-memory transport
- **Real-world analogy:** "Click here to use the toy postal system"
- **What it does:** Registers in-memory transport with your application
- **How it's used:** Call `AddHoneyDrunkInMemoryTransport()` in test startup
- **Why it matters:** One-line switch from real transport to in-memory for tests
- **When to use:** Test projects, local development
- **Example:**
  ```csharp
  // Production (Startup.cs)
  services.AddHoneyDrunkTransportCore()
      .AddHoneyDrunkServiceBusTransport(options => /* ... */);
  
  // Tests (TestStartup.cs)
  services.AddHoneyDrunkTransportCore()
      .AddHoneyDrunkInMemoryTransport(); // Drop-in replacement
  ```

---

## ☁️ HoneyDrunk.Transport.AzureServiceBus

*The "real postal service" using Microsoft Azure*

### ServiceBusTransportPublisher.cs
- **What it is:** The Azure Service Bus sender
- **Real-world analogy:** The interface to FedEx/UPS for sending packages
- **What it does:** Publishes messages to real Azure Service Bus topics/queues
- **How it's used:** Injected as `ITransportPublisher`; converts envelopes to ServiceBusMessage
- **Why it matters:** Production-grade message delivery with Azure's reliability guarantees
- **When to use:** Production deployments requiring durable messaging
- **Example:**
  ```csharp
  services.AddHoneyDrunkTransportCore()
      .AddHoneyDrunkServiceBusTransport(options =>
      {
          options.ConnectionString = configuration["AzureServiceBus:ConnectionString"];
          options.TopicName = "orders";
      });
  
  // Publisher uses Azure Service Bus automatically
  await _publisher.PublishAsync(envelope, ct); // Sends to Azure
  ```

### ServiceBusTransportConsumer.cs
- **What it is:** The Azure Service Bus receiver
- **Real-world analogy:** The interface that receives packages from FedEx/UPS
- **What it does:** Listens to Azure Service Bus and processes incoming messages
- **How it's used:** Runs as background service; supports queues, topics, sessions
- **Why it matters:** Reliable message reception with automatic lock renewal and dead-lettering
- **When to use:** Production deployments consuming from Azure Service Bus
- **Example:**
  ```csharp
  services.AddHoneyDrunkTransportCore()
      .AddHoneyDrunkServiceBusTransport(options => /* ... */)
      .WithTopicSubscription("order-processor") // Consumer listens to subscription
      .WithSessions(); // Enable session support if needed
  ```

### Configuration/AzureServiceBusOptions.cs
- **What it is:** Settings specific to Azure Service Bus
- **Real-world analogy:** Your FedEx account settings (connection string, retry policy)
- **What it does:** Configures connection strings, topic names, subscriptions, sessions
- **How it's used:** Configure via builder or IOptions binding
- **Why it matters:** Centralizes Azure-specific settings
- **When to use:** Configure during startup for Azure deployments
- **Example:**
  ```csharp
  // appsettings.json
  {
    "AzureServiceBus": {
      "ConnectionString": "Endpoint=sb://...",
      "TopicName": "orders",
      "SubscriptionName": "order-processor",
      "EnableSessions": false,
      "MaxConcurrentCalls": 10,
      "PrefetchCount": 100
    }
  }
  
  // Startup
  services.AddHoneyDrunkTransportCore()
      .AddHoneyDrunkServiceBusTransport(
          configuration.GetSection("AzureServiceBus"));
  ```

### Mapping/EnvelopeMapper.cs
- **What it is:** The "format converter"
- **Real-world analogy:** Converts your internal envelope format to Azure's package format
- **What it does:** Maps between TransportEnvelope and Azure ServiceBusMessage
- **How it's used:** Internal component used by publisher/consumer
- **Why it matters:** Bridges library abstraction with Azure's native message format
- **When to use:** Used internally - not typically interacted with directly
- **Example:**
  ```csharp
  // Internal mapping:
  // TransportEnvelope.MessageId → ServiceBusMessage.MessageId
  // TransportEnvelope.CorrelationId → ServiceBusMessage.CorrelationId
  // TransportEnvelope.Headers → ServiceBusMessage.ApplicationProperties
  // TransportEnvelope.Payload → ServiceBusMessage.Body
  ```

### DependencyInjection/ServiceCollectionExtensions.cs
- **What it is:** Setup wizard for Azure Service Bus
- **Real-world analogy:** "Connect to Azure" configuration wizard
- **What it does:** Registers Azure Service Bus transport with your application
- **How it's used:** Call `AddHoneyDrunkServiceBusTransport()` in startup
- **Why it matters:** Fluent API for Azure-specific configuration
- **When to use:** Production deployments using Azure Service Bus
- **Example:**
  ```csharp
  services.AddHoneyDrunkTransportCore()
      .AddHoneyDrunkServiceBusTransport(options =>
      {
          options.ConnectionString = configuration["AzureServiceBus:ConnectionString"];
          options.TopicName = "orders";
      })
      .WithTopicSubscription("order-processor")
      .WithSessions() // Enable sessions
      .WithRetry(retry =>
      {
          retry.MaxAttempts = 5;
          retry.BackoffStrategy = BackoffStrategy.Exponential;
      });
  ```

---

## 🎯 Summary: The Big Picture

### What problem does this solve?

Applications need to send messages to each other, but each messaging system (Azure Service Bus, RabbitMQ, Kafka, etc.) works differently. This library provides one consistent way to send and receive messages, and you can swap out the underlying technology without changing your code.

### How to explain it to a non-technical person:

> "Imagine you write letters, but you don't want to care whether they're sent by USPS, FedEx, or carrier pigeon. This library is like having one mailbox where you drop letters, and it figures out which delivery service to use. You can switch from one delivery service to another without rewriting your letters."

### How to explain it to a developer:

> "It's a transport-agnostic messaging abstraction layer with a middleware pipeline pattern (like ASP.NET Core). Write your message handlers once, then plug in Azure Service Bus, RabbitMQ, or in-memory for testing. Includes retry policies, correlation tracking, telemetry, and transactional outbox pattern out of the box."

---

## 📚 Quick Reference

### Core Concepts

| Concept | Description | Analogy |
|---------|-------------|---------|
| **Envelope** | Message wrapper with metadata | Physical envelope with address |
| **Publisher** | Sends messages | Post office counter |
| **Consumer** | Receives messages | Mailbox |
| **Handler** | Processes specific message types | Mail sorter |
| **Middleware** | Processing pipeline steps | Assembly line stations |
| **Serializer** | Converts objects to/from bytes | Translator |
| **Outbox** | Transactional message store | Safe for important mail |

### Message Flow

```
1. Create Message → 2. Wrap in Envelope → 3. Serialize → 4. Publish
                                                              ↓
5. Consumer Receives ← 6. Deserialize ← 7. Pipeline (Middleware) ← 8. Handler
```

### Middleware Pipeline Order

```
Message → Correlation → Telemetry → Logging → Custom → Retry → Handler
```

### Common Usage Patterns

#### Basic Publish/Subscribe
```csharp
// Publish
var message = new OrderCreated(orderId, customerId);
var envelope = factory.Create(message, "orders.created");
await publisher.PublishAsync(envelope, ct);

// Subscribe (handler)
public class OrderCreatedHandler : IMessageHandler<OrderCreated>
{
    public async Task<MessageProcessingResult> HandleAsync(OrderCreated message, MessageContext context, CancellationToken ct)
    {
        // Process message
        return MessageProcessingResult.Success;
    }
}
```

#### With Retry Logic
```csharp
services.AddHoneyDrunkTransportCore()
    .AddHoneyDrunkServiceBusTransport(options => /* ... */)
    .WithRetry(retry =>
    {
        retry.MaxAttempts = 5;
        retry.BackoffStrategy = BackoffStrategy.Exponential;
    });
```

#### With Transactional Outbox
```csharp
// In service
using var transaction = await db.Database.BeginTransactionAsync(ct);
db.Orders.Add(order);
await outbox.SaveAsync(new OutboxMessage(envelope), ct);
await db.SaveChangesAsync(ct);
await transaction.CommitAsync(ct);

// Background dispatcher publishes later
services.AddHostedService<OutboxDispatcherService>();
```

#### Testing with InMemory
```csharp
// Test setup
services.AddHoneyDrunkTransportCore()
    .AddHoneyDrunkInMemoryTransport(); // No connection strings needed
  
// Test can publish and consume in-process
[Fact]
public async Task ProcessesOrderCreatedMessage()
{
    await _publisher.PublishAsync(envelope, ct);
    await Task.Delay(100); // Give consumer time to process
    
    // Assert handler was called
    _mockHandler.Verify(h => h.HandleAsync(It.IsAny<OrderCreated>(), It.IsAny<MessageContext>(), It.IsAny<CancellationToken>()));
}
```

---

*Last Updated: 2025-11-11*
*Version: 0.1.0*
