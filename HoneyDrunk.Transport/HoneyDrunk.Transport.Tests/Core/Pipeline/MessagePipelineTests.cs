using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.DependencyInjection;
using HoneyDrunk.Transport.Pipeline;
using HoneyDrunk.Transport.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HoneyDrunk.Transport.Tests.Core.Pipeline;

/// <summary>
/// Tests for the message processing pipeline.
/// </summary>
public sealed class MessagePipelineTests
{
    /// <summary>
    /// Ensures the pipeline invokes the registered handler and returns success.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task ProcessAsync_WithRegisteredHandler_ReturnsSuccessAndInvokesHandler()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Debug));
        services.AddHoneyDrunkTransportCore();
        services.AddMessageHandler<SampleMessage, SampleMessageHandler>();

        var provider = new DefaultServiceProviderFactory().CreateServiceProvider(services);
        var pipeline = provider.GetRequiredService<IMessagePipeline>();

        var envelope = TestData.CreateEnvelope(new SampleMessage { Value = "ok" });
        var context = new MessageContext
        {
            Envelope = envelope,
            Transaction = NoOpTransportTransaction.Instance,
            DeliveryCount = 1
        };

        var result = await pipeline.ProcessAsync(envelope, context);

        Assert.Equal(MessageProcessingResult.Success, result);
        Assert.Equal("ok", (string)context.Properties["handled"]);
    }

    /// <summary>
    /// Ensures the pipeline returns success when no handler is registered.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task ProcessAsync_WithNoHandler_ReturnsSuccess()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHoneyDrunkTransportCore();
        var provider = new DefaultServiceProviderFactory().CreateServiceProvider(services);
        var pipeline = provider.GetRequiredService<IMessagePipeline>();

        var envelope = TestData.CreateEnvelope(new SampleMessage { Value = "x" });
        var context = new MessageContext
        {
            Envelope = envelope,
            Transaction = NoOpTransportTransaction.Instance,
            DeliveryCount = 1
        };

        var result = await pipeline.ProcessAsync(envelope, context);
        Assert.Equal(MessageProcessingResult.Success, result);
    }

    /// <summary>
    /// Ensures deserialization failures result in dead-letter.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task ProcessAsync_WithDeserializationFailure_ReturnsDeadLetter()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHoneyDrunkTransportCore();
        var provider = new DefaultServiceProviderFactory().CreateServiceProvider(services);

        var pipeline = provider.GetRequiredService<IMessagePipeline>();

        // Use a bad payload for a valid type to cause deserialization to fail
        var badEnvelope = new Primitives.TransportEnvelope
        {
            MessageId = Guid.NewGuid().ToString("N"),
            MessageType = typeof(string).AssemblyQualifiedName!,
            Payload = new byte[] { 0x01, 0x02, 0x03 },
            Headers = new Dictionary<string, string>(),
            Timestamp = DateTimeOffset.UtcNow
        };

        var context = new MessageContext
        {
            Envelope = badEnvelope,
            Transaction = NoOpTransportTransaction.Instance,
            DeliveryCount = 1
        };

        var result = await pipeline.ProcessAsync(badEnvelope, context);
        Assert.Equal(MessageProcessingResult.DeadLetter, result);
    }
}
