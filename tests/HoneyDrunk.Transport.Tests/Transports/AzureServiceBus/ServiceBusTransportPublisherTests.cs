using Azure.Messaging.ServiceBus;
using HoneyDrunk.Transport.AzureServiceBus;
using HoneyDrunk.Transport.AzureServiceBus.Configuration;
using HoneyDrunk.Transport.Tests.Support;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace HoneyDrunk.Transport.Tests.Transports.AzureServiceBus;

/// <summary>
/// Tests for Azure Service Bus transport publisher.
/// </summary>
public sealed class ServiceBusTransportPublisherTests
{
    /// <summary>
    /// Verifies PublishAsync sends message to Service Bus.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task PublishAsync_WithValidMessage_SendsMessageToServiceBus()
    {
        var options = new TestOptions(new AzureServiceBusOptions
        {
            FullyQualifiedNamespace = "test.servicebus.windows.net",
            EntityType = ServiceBusEntityType.Queue
        });

        var client = Substitute.For<ServiceBusClient>();
        var sender = Substitute.For<ServiceBusSender>();
        client.CreateSender(Arg.Any<string>()).Returns(sender);

        var logger = Substitute.For<ILogger<ServiceBusTransportPublisher>>();

        await using var publisher = new ServiceBusTransportPublisher(client, options, logger);

        var envelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" });
        var destination = TestData.Address("test-queue", "test-queue");

        await publisher.PublishAsync(envelope, destination);

        await sender.Received(1).SendMessageAsync(Arg.Any<ServiceBusMessage>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Verifies DisposeAsync disposes sender.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task DisposeAsync_AfterPublish_DisposeSenderSuccessfully()
    {
        var options = new TestOptions(new AzureServiceBusOptions
        {
            FullyQualifiedNamespace = "test.servicebus.windows.net",
            EntityType = ServiceBusEntityType.Queue
        });

        var client = Substitute.For<ServiceBusClient>();
        var sender = Substitute.For<ServiceBusSender>();
        client.CreateSender(Arg.Any<string>()).Returns(sender);

        var logger = Substitute.For<ILogger<ServiceBusTransportPublisher>>();

        var publisher = new ServiceBusTransportPublisher(client, options, logger);

        // Trigger sender creation
        var envelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" });
        var destination = TestData.Address("test-queue", "test-queue");
        await publisher.PublishAsync(envelope, destination);

        await publisher.DisposeAsync();

        await sender.Received(1).DisposeAsync();
    }

    // Nested helper types should be placed after methods to satisfy SA1201 within a class
    private sealed class TestOptions(AzureServiceBusOptions value) : IOptions<AzureServiceBusOptions>
    {
        public AzureServiceBusOptions Value { get; } = value;
    }
}
