using Azure.Messaging.ServiceBus;
using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.AzureServiceBus;
using HoneyDrunk.Transport.AzureServiceBus.Configuration;
using HoneyDrunk.Transport.Pipeline;
using HoneyDrunk.Transport.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Reflection;

namespace HoneyDrunk.Transport.Tests.Transports.AzureServiceBus;

/// <summary>
/// Tests for Service Bus consumer message-processing settlement paths.
/// </summary>
public sealed class ServiceBusTransportConsumerProcessingTests
{
    /// <summary>
    /// Processing success completes the message when auto-complete is disabled.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task ProcessReceivedMessageAsync_WhenPipelineSucceeds_CompletesMessage()
    {
        var pipeline = Substitute.For<IMessagePipeline>();
        pipeline.ProcessAsync(Arg.Any<ITransportEnvelope>(), Arg.Any<MessageContext>(), Arg.Any<CancellationToken>())
            .Returns(MessageProcessingResult.Success);
        await using var fixture = CreateConsumer(pipeline, autoComplete: false);
        var completeCount = 0;
        var context = CreateReceivedContext(onComplete: () => completeCount++);

        await InvokeProcessingAsync(fixture.Consumer, "ProcessReceivedMessageAsync", context);

        Assert.Equal(1, completeCount);
    }

    /// <summary>
    /// Processing retry abandons the message when auto-complete is disabled.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task ProcessReceivedMessageAsync_WhenPipelineRetries_AbandonsMessage()
    {
        var pipeline = Substitute.For<IMessagePipeline>();
        pipeline.ProcessAsync(Arg.Any<ITransportEnvelope>(), Arg.Any<MessageContext>(), Arg.Any<CancellationToken>())
            .Returns(MessageProcessingResult.Retry);
        await using var fixture = CreateConsumer(pipeline, autoComplete: false);
        var abandonCount = 0;
        var context = CreateReceivedContext(onAbandon: () => abandonCount++);

        await InvokeProcessingAsync(fixture.Consumer, "ProcessReceivedMessageAsync", context);

        Assert.Equal(1, abandonCount);
    }

    /// <summary>
    /// Processing dead-letter result dead-letters the message when auto-complete is disabled.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task ProcessReceivedMessageAsync_WhenPipelineDeadLetters_DeadLettersMessage()
    {
        var pipeline = Substitute.For<IMessagePipeline>();
        pipeline.ProcessAsync(Arg.Any<ITransportEnvelope>(), Arg.Any<MessageContext>(), Arg.Any<CancellationToken>())
            .Returns(MessageProcessingResult.DeadLetter);
        await using var fixture = CreateConsumer(pipeline, autoComplete: false);
        var deadLetterCount = 0;
        var context = CreateReceivedContext(onDeadLetter: () => deadLetterCount++);

        await InvokeProcessingAsync(fixture.Consumer, "ProcessReceivedMessageAsync", context);

        Assert.Equal(1, deadLetterCount);
    }

    /// <summary>
    /// Processing exceptions abandon the message when auto-complete is disabled.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task ProcessReceivedMessageAsync_WhenPipelineThrows_AbandonsMessage()
    {
        var pipeline = Substitute.For<IMessagePipeline>();
        pipeline.ProcessAsync(Arg.Any<ITransportEnvelope>(), Arg.Any<MessageContext>(), Arg.Any<CancellationToken>())
            .Returns<Task<MessageProcessingResult>>(_ => throw new InvalidOperationException("boom"));
        await using var fixture = CreateConsumer(pipeline, autoComplete: false);
        var abandonCount = 0;
        var context = CreateReceivedContext(onAbandon: () => abandonCount++);

        await InvokeProcessingAsync(fixture.Consumer, "ProcessReceivedMessageAsync", context);

        Assert.Equal(1, abandonCount);
    }

    /// <summary>
    /// Auto-complete mode leaves settlement to the Service Bus SDK.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task ProcessReceivedMessageAsync_WhenAutoCompleteEnabled_DoesNotSettle()
    {
        var pipeline = Substitute.For<IMessagePipeline>();
        pipeline.ProcessAsync(Arg.Any<ITransportEnvelope>(), Arg.Any<MessageContext>(), Arg.Any<CancellationToken>())
            .Returns(MessageProcessingResult.Success);
        await using var fixture = CreateConsumer(pipeline, autoComplete: true);
        var completeCount = 0;
        var context = CreateReceivedContext(onComplete: () => completeCount++);

        await InvokeProcessingAsync(fixture.Consumer, "ProcessReceivedMessageAsync", context);

        Assert.Equal(0, completeCount);
    }

    private static ConsumerFixture CreateConsumer(IMessagePipeline pipeline, bool autoComplete)
    {
        var provider = new ServiceCollection().BuildServiceProvider();
        var consumer = new ServiceBusTransportConsumer(
            Substitute.For<ServiceBusClient>(),
            pipeline,
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new AzureServiceBusOptions { Address = "orders", AutoComplete = autoComplete }),
            NullLogger<ServiceBusTransportConsumer>.Instance);

        return new ConsumerFixture(consumer, provider);
    }

    private static object CreateReceivedContext(
        Action? onComplete = null,
        Action? onAbandon = null,
        Action? onDeadLetter = null)
    {
        var type = typeof(ServiceBusTransportConsumer).GetNestedType("ServiceBusReceivedMessageContext", BindingFlags.NonPublic)!;
        var constructor = type.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
        var envelope = TestData.CreateEnvelope(new SampleMessage { Value = "message" });
        return constructor.Invoke(
        [
            envelope,
            NoOpTransportTransaction.Instance,
            2,
            ToFunc(onComplete),
            ToFunc(onAbandon),
            ToFunc(onDeadLetter),
            CancellationToken.None,
        ]);
    }

    private static Func<Task> ToFunc(Action? action) => () =>
    {
        action?.Invoke();
        return Task.CompletedTask;
    };

    private static async Task InvokeProcessingAsync(
        ServiceBusTransportConsumer consumer,
        string methodName,
        object context)
    {
        var method = typeof(ServiceBusTransportConsumer).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!;
        var task = (Task)method.Invoke(consumer, [context])!;
        await task;
    }

    private sealed class ConsumerFixture : IAsyncDisposable
    {
        public ConsumerFixture(ServiceBusTransportConsumer consumer, ServiceProvider provider)
        {
            Consumer = consumer;
            Provider = provider;
        }

        public ServiceBusTransportConsumer Consumer { get; }

        private ServiceProvider Provider { get; }

        public async ValueTask DisposeAsync()
        {
            await Consumer.DisposeAsync();
            await Provider.DisposeAsync();
        }
    }
}
