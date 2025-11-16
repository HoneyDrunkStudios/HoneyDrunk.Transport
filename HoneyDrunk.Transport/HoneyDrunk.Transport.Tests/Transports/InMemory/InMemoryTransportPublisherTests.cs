using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.InMemory;
using HoneyDrunk.Transport.Tests.Support;

namespace HoneyDrunk.Transport.Tests.Transports.InMemory;

/// <summary>
/// Tests for InMemory transport publisher.
/// </summary>
public sealed class InMemoryTransportPublisherTests
{
    /// <summary>
    /// Verifies PublishAsync with single envelope publishes to broker.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task PublishAsync_WithSingleEnvelope_PublishesToBrokerSuccessfully()
    {
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<InMemoryBroker>.Instance;
        var broker = new InMemoryBroker(logger);
        var publisherLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<InMemoryTransportPublisher>.Instance;
        var publisher = new InMemoryTransportPublisher(broker, publisherLogger);

        var envelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" });
        var destination = TestData.Address("test", "queue1");

        await publisher.PublishAsync(envelope, destination);

        Assert.True(broker.GetMessageCount("queue1") > 0);
    }

    /// <summary>
    /// Verifies PublishAsync throws on null envelope.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task PublishAsync_WithNullEnvelope_ThrowsArgumentNullException()
    {
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<InMemoryBroker>.Instance;
        var broker = new InMemoryBroker(logger);
        var publisherLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<InMemoryTransportPublisher>.Instance;
        var publisher = new InMemoryTransportPublisher(broker, publisherLogger);

        var destination = TestData.Address("test", "queue3");

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => publisher.PublishAsync((ITransportEnvelope)null!, destination));
    }

    /// <summary>
    /// Verifies PublishAsync throws on null destination.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task PublishAsync_WithNullDestination_ThrowsArgumentNullException()
    {
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<InMemoryBroker>.Instance;
        var broker = new InMemoryBroker(logger);
        var publisherLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<InMemoryTransportPublisher>.Instance;
        var publisher = new InMemoryTransportPublisher(broker, publisherLogger);

        var envelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" });

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => publisher.PublishAsync(envelope, null!));
    }
}
