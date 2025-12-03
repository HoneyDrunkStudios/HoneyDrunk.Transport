# HoneyDrunk.Transport.InMemory

[![NuGet](https://img.shields.io/nuget/v/HoneyDrunk.Transport.InMemory.svg)](https://www.nuget.org/packages/HoneyDrunk.Transport.InMemory/)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/10.0)

> **In-Memory Transport for Testing** - Fast, no-dependency message broker for integration tests with full pipeline execution.

## 📋 What Is This?

**HoneyDrunk.Transport.InMemory** provides an in-process message broker for testing Transport applications without external dependencies. It implements the full Transport abstraction layer (`ITransportPublisher`, `ITransportConsumer`) with Channel-based observable queues, enabling fast integration tests with complete pipeline execution.

The InMemory provider runs the **exact same middleware pipeline, handler resolution, and context propagation logic** as production transports. This isn't a mock—it's a real transport implementation.

**Key Features:**
- ✅ **Zero Infrastructure** - No Azure, no Docker, no external services
- ✅ **Full Pipeline** - Complete middleware and handler execution
- ✅ **Observable Queues** - Channel-based for test verification
- ✅ **Multi-Observer** - Multiple subscribers can observe messages for testing
- ✅ **Fast Execution** - In-process, no network latency
- ✅ **Deterministic Testing** - No timing issues or flaky tests

**What InMemory Does Not Do:**
> InMemory does not simulate broker-specific features like lock tokens, sessions, dead-letter queues, or delivery retries. Its purpose is deterministic pipeline execution, not emulating Azure broker semantics.

> The InMemory transport is not designed for concurrency, lock renewal, or backpressure testing. Use real transports for those scenarios.

**Signal Quote:** *"Test like production, fail like development."*

---

## 📦 What's Inside

### InMemoryBroker
Central message router with observable queues:
- Subscribe to addresses for message notifications
- Publish messages to in-memory channels
- Retrieve `Channel<ITransportEnvelope>` for test assertions

### InMemoryTransportPublisher
`ITransportPublisher` implementation routing to InMemoryBroker

### InMemoryTransportConsumer
`ITransportConsumer` implementation processing messages through pipeline. Participates fully in the `TransportRuntimeHost` lifecycle, starting automatically as an `IHostedService`.

---

## 📥 Installation

```bash
dotnet add package HoneyDrunk.Transport.InMemory
```

```xml
<PackageReference Include="HoneyDrunk.Transport.InMemory" Version="0.1.1" />
```

---

## 💡 Quick Example

### Test Setup

```csharp
public class IntegrationTests
{
    private readonly ServiceProvider _services;
    
    public IntegrationTests()
    {
        var services = new ServiceCollection();
        
        // Register Kernel
        services.AddHoneyDrunkCoreNode(new NodeDescriptor
        {
            NodeId = "test-node",
            Version = "1.0.0"
        });
        
        // Register InMemory transport
        services.AddHoneyDrunkTransportCore(options =>
        {
            options.EnableCorrelation = true;
        })
        .AddHoneyDrunkInMemoryTransport();
        
        // Register handlers
        services.AddMessageHandler<OrderCreated, OrderCreatedHandler>();
        
        _services = services.BuildServiceProvider();
    }
}
```

### End-to-End Test

```csharp
[Fact]
public async Task ProcessesOrderCreatedMessage()
{
    // Arrange
    var publisher = _services.GetRequiredService<ITransportPublisher>();
    var broker = _services.GetRequiredService<InMemoryBroker>();
    var received = new TaskCompletionSource<bool>();
    
    broker.Subscribe("orders", async (envelope, ct) =>
    {
        received.SetResult(true);
        await Task.CompletedTask;
    });
    
    // Act
    var envelope = CreateEnvelope(new OrderCreated(123, 456));
    
    // EndpointAddress behaves the same across all transports
    await publisher.PublishAsync(envelope, EndpointAddress.Create("orders", "orders"), ct);
    
    // Assert
    var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
    Assert.True(result);
}
```

---

## 🎯 When to Use

**Use InMemory transport when:**
- ✅ Writing integration tests
- ✅ Local development without infrastructure
- ✅ CI/CD pipelines (no external dependencies)
- ✅ Testing middleware pipeline behavior
- ✅ Verifying message handler logic

**Use real transports when:**
- ✅ Production deployments
- ✅ Load testing
- ✅ Multi-node distributed scenarios
- ✅ Concurrency and backpressure testing
- ✅ Testing broker-specific features (sessions, DLQ, retries)

---

## 🔗 Related Packages

- **[HoneyDrunk.Transport](https://www.nuget.org/packages/HoneyDrunk.Transport/)** - Core abstractions
- **[HoneyDrunk.Transport.AzureServiceBus](https://www.nuget.org/packages/HoneyDrunk.Transport.AzureServiceBus/)** - Production Service Bus transport
- **[HoneyDrunk.Transport.StorageQueue](https://www.nuget.org/packages/HoneyDrunk.Transport.StorageQueue/)** - Production Storage Queue transport

---

## 📚 Documentation

- **[Testing Guide](../docs/Testing.md)** - Test patterns and examples
- **[InMemory Guide](../docs/InMemory.md)** - Detailed implementation guide
- **[Complete File Guide](../docs/FILE_GUIDE.md)** - Architecture documentation

---

## 📄 License

This project is licensed under the [MIT License](../LICENSE).

---

**Built with 🍯 by HoneyDrunk Studios**

[GitHub](https://github.com/HoneyDrunkStudios/HoneyDrunk.Transport) • [Documentation](../docs/FILE_GUIDE.md) • [Issues](https://github.com/HoneyDrunkStudios/HoneyDrunk.Transport/issues)
