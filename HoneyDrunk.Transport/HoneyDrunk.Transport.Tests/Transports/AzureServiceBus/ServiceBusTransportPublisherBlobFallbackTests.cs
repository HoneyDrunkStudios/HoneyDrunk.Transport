using Azure.Messaging.ServiceBus;
using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.AzureServiceBus;
using HoneyDrunk.Transport.AzureServiceBus.Configuration;
using HoneyDrunk.Transport.AzureServiceBus.Internal;
using HoneyDrunk.Transport.Tests.Support;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace HoneyDrunk.Transport.Tests.Transports.AzureServiceBus;

/// <summary>
/// Tests for blob fallback behavior in ServiceBusTransportPublisher.
/// </summary>
public sealed class ServiceBusTransportPublisherBlobFallbackTests
{
    /// <summary>
    /// Verifies that when single send fails, the envelope is persisted via blob fallback store.
    /// </summary>
    /// <returns>A task representing the async operation.</returns>
    [Fact]
    public async Task PublishAsync_WhenSendFails_PersistsToBlobFallback()
    {
        var asbOptions = new AzureServiceBusOptions
        {
            FullyQualifiedNamespace = "test.servicebus.windows.net",
            EntityType = ServiceBusEntityType.Queue,
            Address = "orders",
        };
        asbOptions.BlobFallback.Enabled = true;
        asbOptions.BlobFallback.ConnectionString = "UseDevelopmentStorage=true";

        var options = new TestOptions(asbOptions);

        var client = Substitute.For<ServiceBusClient>();
        var sender = Substitute.For<ServiceBusSender>();
        client.CreateSender(Arg.Any<string>()).Returns(sender);

        // Simulate send failure
        sender
            .SendMessageAsync(Arg.Any<ServiceBusMessage>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("boom"));

        var logger = Substitute.For<ILogger<ServiceBusTransportPublisher>>();

        var fakeStore = Substitute.For<IBlobFallbackStore>();
        fakeStore
            .SaveAsync(Arg.Any<ITransportEnvelope>(), Arg.Any<IEndpointAddress>(), Arg.Any<Exception>(), Arg.Any<BlobFallbackOptions>(), Arg.Any<CancellationToken>())
            .Returns(new Uri("https://account.blob.core.windows.net/container/blob.json"));

        await using var publisher = new ServiceBusTransportPublisher(client, options, logger, fakeStore);

        var envelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" });
        var destination = TestData.Address("orders", "orders");

        await publisher.PublishAsync(envelope, destination);

        await fakeStore.Received(1).SaveAsync(
            envelope,
            destination,
            Arg.Any<Exception>(),
            Arg.Any<BlobFallbackOptions>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Verifies that when batch publish fails early, all envelopes are persisted to blob fallback.
    /// </summary>
    /// <returns>A task representing the async operation.</returns>
    [Fact]
    public async Task PublishBatchAsync_WhenBatchFlowFails_PersistsAllToBlobFallback()
    {
        var asbOptions = new AzureServiceBusOptions
        {
            FullyQualifiedNamespace = "test.servicebus.windows.net",
            EntityType = ServiceBusEntityType.Queue,
            Address = "orders",
        };
        asbOptions.BlobFallback.Enabled = true;
        asbOptions.BlobFallback.ConnectionString = "UseDevelopmentStorage=true";

        var options = new TestOptions(asbOptions);

        var client = Substitute.For<ServiceBusClient>();
        var sender = Substitute.For<ServiceBusSender>();
        client.CreateSender(Arg.Any<string>()).Returns(sender);

#pragma warning disable CA2012 // ValueTask instances should be consumed correctly
        // Fail immediately during batch creation to drive catch path in PublishBatchAsync
        sender
            .CreateMessageBatchAsync(Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromException<ServiceBusMessageBatch>(new InvalidOperationException("batch create failed")));
#pragma warning restore CA2012

        var logger = Substitute.For<ILogger<ServiceBusTransportPublisher>>();

        var fakeStore = Substitute.For<IBlobFallbackStore>();
        fakeStore
            .SaveAsync(Arg.Any<ITransportEnvelope>(), Arg.Any<IEndpointAddress>(), Arg.Any<Exception>(), Arg.Any<BlobFallbackOptions>(), Arg.Any<CancellationToken>())
            .Returns(new Uri("https://account.blob.core.windows.net/container/blob.json"));

        await using var publisher = new ServiceBusTransportPublisher(client, options, logger, fakeStore);

        var envelopes = new[]
        {
            TestData.CreateEnvelope(new SampleMessage { Value = "a" }),
            TestData.CreateEnvelope(new SampleMessage { Value = "b" }),
        };
        var destination = TestData.Address("orders", "orders");

        await publisher.PublishBatchAsync(envelopes, destination);

        await fakeStore.Received(2).SaveAsync(
            Arg.Any<ITransportEnvelope>(),
            destination,
            Arg.Any<Exception>(),
            Arg.Any<BlobFallbackOptions>(),
            Arg.Any<CancellationToken>());
    }

    private sealed class TestOptions(AzureServiceBusOptions value) : IOptions<AzureServiceBusOptions>
    {
        public AzureServiceBusOptions Value { get; } = value;
    }
}
