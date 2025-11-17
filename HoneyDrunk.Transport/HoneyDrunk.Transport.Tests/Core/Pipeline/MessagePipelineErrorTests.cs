using HoneyDrunk.Transport.DependencyInjection;
using HoneyDrunk.Transport.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace HoneyDrunk.Transport.Tests.Core.Pipeline;

/// <summary>
/// Additional tests for message pipeline error handling.
/// </summary>
public sealed class MessagePipelineErrorTests
{
    /// <summary>
    /// Verifies pipeline handles handler exceptions and returns retry.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task ProcessAsync_WhenHandlerThrows_ReturnsRetryResult()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHoneyDrunkTransportCore();
        services.AddMessageHandler<SampleMessage, ThrowingHandler>();

        var provider = new DefaultServiceProviderFactory().CreateServiceProvider(services);
        var pipeline = provider.GetRequiredService<HoneyDrunk.Transport.Pipeline.IMessagePipeline>();

        var envelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" });
        var context = new HoneyDrunk.Transport.Abstractions.MessageContext
        {
            Envelope = envelope,
            Transaction = HoneyDrunk.Transport.Abstractions.NoOpTransportTransaction.Instance,
            DeliveryCount = 1
        };

        var result = await pipeline.ProcessAsync(envelope, context);

        Assert.Equal(HoneyDrunk.Transport.Abstractions.MessageProcessingResult.Retry, result);
    }

    /// <summary>
    /// Verifies pipeline handles null message type gracefully.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task ProcessAsync_WithNullMessageType_ReturnsDeadLetterResult()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHoneyDrunkTransportCore();
        var provider = new DefaultServiceProviderFactory().CreateServiceProvider(services);
        var pipeline = provider.GetRequiredService<HoneyDrunk.Transport.Pipeline.IMessagePipeline>();

        var badEnvelope = new HoneyDrunk.Transport.Primitives.TransportEnvelope
        {
            MessageId = Guid.NewGuid().ToString("N"),
            MessageType = string.Empty,
            Payload = new byte[] { 1, 2, 3 },
            Headers = new Dictionary<string, string>(),
            Timestamp = DateTimeOffset.UtcNow
        };

        var context = new HoneyDrunk.Transport.Abstractions.MessageContext
        {
            Envelope = badEnvelope,
            Transaction = HoneyDrunk.Transport.Abstractions.NoOpTransportTransaction.Instance,
            DeliveryCount = 1
        };

        var result = await pipeline.ProcessAsync(badEnvelope, context);

        Assert.Equal(HoneyDrunk.Transport.Abstractions.MessageProcessingResult.DeadLetter, result);
    }

    /// <summary>
    /// Verifies pipeline handles malformed JSON payload.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task ProcessAsync_WithMalformedJson_ReturnsDeadLetterResult()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHoneyDrunkTransportCore();
        services.AddMessageHandler<SampleMessage, SampleMessageHandler>();

        var provider = new DefaultServiceProviderFactory().CreateServiceProvider(services);
        var pipeline = provider.GetRequiredService<HoneyDrunk.Transport.Pipeline.IMessagePipeline>();

        var badEnvelope = new HoneyDrunk.Transport.Primitives.TransportEnvelope
        {
            MessageId = Guid.NewGuid().ToString("N"),
            MessageType = typeof(SampleMessage).AssemblyQualifiedName!,
            Payload = System.Text.Encoding.UTF8.GetBytes("not valid json {{{"),
            Headers = new Dictionary<string, string>(),
            Timestamp = DateTimeOffset.UtcNow
        };

        var context = new HoneyDrunk.Transport.Abstractions.MessageContext
        {
            Envelope = badEnvelope,
            Transaction = HoneyDrunk.Transport.Abstractions.NoOpTransportTransaction.Instance,
            DeliveryCount = 1
        };

        var result = await pipeline.ProcessAsync(badEnvelope, context);

        Assert.Equal(HoneyDrunk.Transport.Abstractions.MessageProcessingResult.DeadLetter, result);
    }

    /// <summary>
    /// Verifies pipeline handles cancellation.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task ProcessAsync_WithCancellationToken_ThrowsOperationCanceledException()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHoneyDrunkTransportCore();

        var tcs = new TaskCompletionSource();
        services.AddMessageHandler<SampleMessage>(async (msg, ctx, ct) =>
        {
            await tcs.Task;
        });

        var provider = new DefaultServiceProviderFactory().CreateServiceProvider(services);
        var pipeline = provider.GetRequiredService<HoneyDrunk.Transport.Pipeline.IMessagePipeline>();

        var envelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" });
        var context = new HoneyDrunk.Transport.Abstractions.MessageContext
        {
            Envelope = envelope,
            Transaction = HoneyDrunk.Transport.Abstractions.NoOpTransportTransaction.Instance,
            DeliveryCount = 1
        };

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => pipeline.ProcessAsync(envelope, context, cts.Token));

        tcs.SetResult();
    }

#pragma warning disable CA1812 // Internal class is instantiated via DI
    private sealed class ThrowingHandler : HoneyDrunk.Transport.Abstractions.IMessageHandler<SampleMessage>
#pragma warning restore CA1812
    {
        public Task HandleAsync(SampleMessage message, HoneyDrunk.Transport.Abstractions.MessageContext context, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Handler error");
        }
    }
}
