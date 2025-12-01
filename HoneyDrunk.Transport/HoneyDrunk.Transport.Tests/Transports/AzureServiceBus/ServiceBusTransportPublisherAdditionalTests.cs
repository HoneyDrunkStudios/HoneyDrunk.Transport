using Azure.Messaging.ServiceBus;
using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.AzureServiceBus;
using HoneyDrunk.Transport.AzureServiceBus.Configuration;
using HoneyDrunk.Transport.AzureServiceBus.DependencyInjection;
using HoneyDrunk.Transport.AzureServiceBus.Internal;
using HoneyDrunk.Transport.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace HoneyDrunk.Transport.Tests.Transports.AzureServiceBus;

/// <summary>
/// Additional tests for ServiceBusTransportPublisher behaviors.
/// </summary>
public sealed class ServiceBusTransportPublisherAdditionalTests
{
    /// <summary>
    /// Ensures that when fallback is disabled, publish failures propagate to callers.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task PublishAsync_FailureWithoutFallback_Rethrows()
    {
        var options = new AzureServiceBusOptions
        {
            FullyQualifiedNamespace = "ns",
            Address = "q"
        };
        var client = Substitute.For<ServiceBusClient>();
        var sender = Substitute.For<ServiceBusSender>();
        client.CreateSender(Arg.Any<string>()).Returns(sender);
        var logger = Substitute.For<ILogger<ServiceBusTransportPublisher>>();

        var iopts = Substitute.For<IOptions<AzureServiceBusOptions>>();
        iopts.Value.Returns(options);

        sender
            .SendMessageAsync(Arg.Any<ServiceBusMessage>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("fail"));

        await using var publisher = new ServiceBusTransportPublisher(client, iopts, logger);

        var env = TestData.CreateEnvelope(new SampleMessage { Value = "x" });
        var dest = EndpointAddress.Create("q", "q");

        await Assert.ThrowsAsync<InvalidOperationException>(() => publisher.PublishAsync(env, dest));
    }

    /// <summary>
    /// Verifies that partition key and session id are applied from endpoint address properties.
    /// Note: when both are set, Azure Service Bus forces PartitionKey to match SessionId.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task PublishAsync_AppliesPartitionAndSessionFromAddress()
    {
        var options = new AzureServiceBusOptions { FullyQualifiedNamespace = "ns", Address = "q" };
        var client = Substitute.For<ServiceBusClient>();
        var sender = Substitute.For<ServiceBusSender>();
        client.CreateSender(Arg.Any<string>()).Returns(sender);
        var logger = Substitute.For<ILogger<ServiceBusTransportPublisher>>();

        var iopts = Substitute.For<IOptions<AzureServiceBusOptions>>();
        iopts.Value.Returns(options);

        ServiceBusMessage? captured = null;
        sender
            .SendMessageAsync(Arg.Any<ServiceBusMessage>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                captured = ci.Arg<ServiceBusMessage>();
                return Task.CompletedTask;
            });

        await using var publisher = new ServiceBusTransportPublisher(client, iopts, logger);

        var env = TestData.CreateEnvelope(new SampleMessage { Value = "x" });
        var dest = EndpointAddress.Create(
            name: "q",
            address: "q",
            additionalProperties: new Dictionary<string, string>
            {
                ["PartitionKey"] = "pk-1",
                ["SessionId"] = "s-1",
            });

        await publisher.PublishAsync(env, dest);

        Assert.NotNull(captured);
        Assert.Equal("s-1", captured!.SessionId);
        Assert.Equal(captured.SessionId, captured.PartitionKey);
    }

    /// <summary>
    /// Ensures original publish exception is rethrown if fallback store also fails.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task PublishAsync_FallbackStoreThrows_RethrowsOriginal()
    {
        var options = new AzureServiceBusOptions { FullyQualifiedNamespace = "ns", Address = "q" };
        options.BlobFallback.Enabled = true;
        options.BlobFallback.ConnectionString = "UseDevelopmentStorage=true";

        var client = Substitute.For<ServiceBusClient>();
        var sender = Substitute.For<ServiceBusSender>();
        client.CreateSender(Arg.Any<string>()).Returns(sender);
        var logger = Substitute.For<ILogger<ServiceBusTransportPublisher>>();

        var iopts = Substitute.For<IOptions<AzureServiceBusOptions>>();
        iopts.Value.Returns(options);

        sender
            .SendMessageAsync(Arg.Any<ServiceBusMessage>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("publish"));

        var store = Substitute.For<IBlobFallbackStore>();
        store.SaveAsync(Arg.Any<ITransportEnvelope>(), Arg.Any<IEndpointAddress>(), Arg.Any<Exception>(), Arg.Any<BlobFallbackOptions>(), Arg.Any<CancellationToken>())
            .Returns<Task<Uri>>(_ => throw new InvalidOperationException("blob"));

        await using var publisher = new ServiceBusTransportPublisher(client, iopts, logger, store);

        var env = TestData.CreateEnvelope(new SampleMessage { Value = "x" });
        var dest = EndpointAddress.Create("q", "q");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => publisher.PublishAsync(env, dest));
        Assert.Equal("publish", ex.Message);
    }

    /// <summary>
    /// DisposeAsync must be idempotent and dispose sender once.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task DisposeAsync_Idempotent_DisposesSenderOnce()
    {
        var options = new AzureServiceBusOptions { FullyQualifiedNamespace = "ns", Address = "q" };
        var client = Substitute.For<ServiceBusClient>();
        var sender = Substitute.For<ServiceBusSender>();
        client.CreateSender(Arg.Any<string>()).Returns(sender);
        var logger = Substitute.For<ILogger<ServiceBusTransportPublisher>>();

        var iopts = Substitute.For<IOptions<AzureServiceBusOptions>>();
        iopts.Value.Returns(options);

        await using var publisher = new ServiceBusTransportPublisher(client, iopts, logger);

        // Trigger initialization
        var env = TestData.CreateEnvelope(new SampleMessage { Value = "x" });
        var dest = EndpointAddress.Create("q", "q");

        sender.SendMessageAsync(Arg.Any<ServiceBusMessage>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await publisher.PublishAsync(env, dest);

        // Dispose concurrently
        var tasks = Enumerable.Range(0, 5).Select(_ => publisher.DisposeAsync().AsTask());
        await Task.WhenAll(tasks);

        await sender.Received(1).DisposeAsync();
    }

    /// <summary>
    /// DI ensures fallback store is registered by default.
    /// </summary>
    [Fact]
    public void DI_Registers_BlobFallbackStore()
    {
        var services = new ServiceCollection();
        services.AddHoneyDrunkServiceBusTransport(o =>
        {
            o.ConnectionString = "Endpoint=sb://ns/;SharedAccessKey=key";
            o.Address = "q";
        });

        var sp = services.BuildServiceProvider();

        var store = sp.GetService<IBlobFallbackStore>();
        Assert.NotNull(store);
    }
}
