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

    /// <summary>Non-success results settle explicitly in either completion mode through SDK event arguments.</summary>
    /// <param name="autoComplete">Whether successful callbacks are completed by the SDK.</param>
    /// <param name="result">Pipeline disposition.</param>
    /// <param name="session">Whether the session callback is used.</param>
    /// <returns>The callback test.</returns>
    [Theory]
    [InlineData(true, MessageProcessingResult.Retry, false)]
    [InlineData(false, MessageProcessingResult.Retry, false)]
    [InlineData(true, MessageProcessingResult.Abandon, false)]
    [InlineData(false, MessageProcessingResult.Abandon, false)]
    [InlineData(true, MessageProcessingResult.DeadLetter, false)]
    [InlineData(false, MessageProcessingResult.DeadLetter, false)]
    [InlineData(true, MessageProcessingResult.Success, false)]
    [InlineData(false, MessageProcessingResult.Success, false)]
    [InlineData(true, MessageProcessingResult.Retry, true)]
    [InlineData(false, MessageProcessingResult.Retry, true)]
    [InlineData(true, MessageProcessingResult.Abandon, true)]
    [InlineData(false, MessageProcessingResult.Abandon, true)]
    [InlineData(true, MessageProcessingResult.DeadLetter, true)]
    [InlineData(false, MessageProcessingResult.DeadLetter, true)]
    [InlineData(true, MessageProcessingResult.Success, true)]
    [InlineData(false, MessageProcessingResult.Success, true)]
    public async Task ProcessMessageAsync_PipelineResult_PreservesSettlement(bool autoComplete, MessageProcessingResult result, bool session)
    {
        var pipeline = Substitute.For<IMessagePipeline>();
        pipeline.ProcessAsync(Arg.Any<ITransportEnvelope>(), Arg.Any<MessageContext>(), Arg.Any<CancellationToken>()).Returns(result);
        await using var fixture = CreateConsumer(pipeline, autoComplete);
        await using ServiceBusReceiver receiver = session ? Substitute.For<ServiceBusSessionReceiver>() : Substitute.For<ServiceBusReceiver>();
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(body: BinaryData.FromString("{}"), messageId: "settlement");
        object eventArgs = session
            ? new ProcessSessionMessageEventArgs(message, (ServiceBusSessionReceiver)receiver, CancellationToken.None)
            : new ProcessMessageEventArgs(message, receiver, CancellationToken.None);

        await InvokeProcessingAsync(fixture.Consumer, session ? "ProcessSessionMessageAsync" : "ProcessMessageAsync", eventArgs);

        await receiver.Received(result == MessageProcessingResult.Success && !autoComplete ? 1 : 0).CompleteMessageAsync(message, CancellationToken.None);
        await receiver.Received(result is MessageProcessingResult.Retry or MessageProcessingResult.Abandon ? 1 : 0)
            .AbandonMessageAsync(message, Arg.Any<IDictionary<string, object>>(), CancellationToken.None);
        await receiver.Received(result == MessageProcessingResult.DeadLetter ? 1 : 0)
            .DeadLetterMessageAsync(message, Arg.Any<string>(), Arg.Any<string>(), CancellationToken.None);
    }

    /// <summary>Auto-completion must observe failures, including cancellation, instead of treating them as success.</summary>
    /// <param name="cancelled">Whether processing was cancelled.</param>
    /// <returns>The callback test.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProcessMessageAsync_AutoCompleteFailure_PropagatesToSdk(bool cancelled)
    {
        Exception failure = cancelled ? new OperationCanceledException("cancelled") : new InvalidOperationException("failed");
        var pipeline = Substitute.For<IMessagePipeline>();
        pipeline.ProcessAsync(Arg.Any<ITransportEnvelope>(), Arg.Any<MessageContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<MessageProcessingResult>(failure));
        await using var fixture = CreateConsumer(pipeline, autoComplete: true);
        await using var receiver = Substitute.For<ServiceBusReceiver>();
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(body: BinaryData.FromString("{}"), messageId: "failure");
        var eventArgs = new ProcessMessageEventArgs(message, receiver, CancellationToken.None);

        var observed = await Record.ExceptionAsync(() => InvokeProcessingAsync(fixture.Consumer, "ProcessMessageAsync", eventArgs));

        Assert.Same(failure, observed);
        Assert.Empty(receiver.ReceivedCalls());
    }

    /// <summary>A failed explicit settlement cannot turn into a successful automatic callback.</summary>
    /// <returns>The callback test.</returns>
    [Fact]
    public async Task ProcessMessageAsync_AutoCompleteSettlementFails_PropagatesToSdk()
    {
        var pipeline = Substitute.For<IMessagePipeline>();
        pipeline.ProcessAsync(Arg.Any<ITransportEnvelope>(), Arg.Any<MessageContext>(), Arg.Any<CancellationToken>()).Returns(MessageProcessingResult.DeadLetter);
        await using var fixture = CreateConsumer(pipeline, autoComplete: true);
        await using var receiver = Substitute.For<ServiceBusReceiver>();
        var failure = new ServiceBusException("lock lost", ServiceBusFailureReason.MessageLockLost);
        receiver.DeadLetterMessageAsync(Arg.Any<ServiceBusReceivedMessage>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(failure));
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(body: BinaryData.FromString("{}"), messageId: "lock-lost");

        var observed = await Record.ExceptionAsync(() => InvokeProcessingAsync(fixture.Consumer, "ProcessMessageAsync", new ProcessMessageEventArgs(message, receiver, CancellationToken.None)));

        Assert.Same(failure, observed);
        await receiver.DidNotReceive().CompleteMessageAsync(message, Arg.Any<CancellationToken>());
    }

    /// <summary>Real pipeline handler exceptions retain their retry or poison disposition.</summary>
    /// <param name="autoComplete">Whether successful callbacks are completed by the SDK.</param>
    /// <param name="poison">Whether the handler explicitly rejects a poison message.</param>
    /// <returns>The pipeline/callback test.</returns>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task ProcessMessageAsync_HandlerFails_PreservesPipelineDisposition(bool autoComplete, bool poison)
    {
        var handler = Substitute.For<IMessageHandler<SampleMessage>>();
        Exception failure = poison ? new MessageHandlerException("poison", MessageProcessingResult.DeadLetter) : new InvalidOperationException("persistence failed");
        handler.HandleAsync(Arg.Any<SampleMessage>(), Arg.Any<MessageContext>(), Arg.Any<CancellationToken>()).Returns(Task.FromException(failure));
        await using var provider = new ServiceCollection().AddSingleton(handler).BuildServiceProvider();
        var pipeline = new MessagePipeline([], new HoneyDrunk.Transport.DependencyInjection.JsonMessageSerializer(), provider, NullLogger<MessagePipeline>.Instance);
        await using var client = Substitute.For<ServiceBusClient>();
        await using var consumer = new ServiceBusTransportConsumer(client, pipeline, provider.GetRequiredService<IServiceScopeFactory>(), Options.Create(new AzureServiceBusOptions { Address = "orders", AutoComplete = autoComplete }), NullLogger<ServiceBusTransportConsumer>.Instance);
        await using var receiver = Substitute.For<ServiceBusReceiver>();
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(body: BinaryData.FromString("{\"value\":\"test\"}"), messageId: "handler-failure", subject: typeof(SampleMessage).AssemblyQualifiedName);

        await InvokeProcessingAsync(consumer, "ProcessMessageAsync", new ProcessMessageEventArgs(message, receiver, CancellationToken.None));

        await handler.Received(1).HandleAsync(Arg.Any<SampleMessage>(), Arg.Any<MessageContext>(), Arg.Any<CancellationToken>());
        await receiver.Received(poison ? 0 : 1).AbandonMessageAsync(message, Arg.Any<IDictionary<string, object>>(), CancellationToken.None);
        await receiver.Received(poison ? 1 : 0).DeadLetterMessageAsync(message, Arg.Any<string>(), Arg.Any<string>(), CancellationToken.None);
        await receiver.DidNotReceive().CompleteMessageAsync(message, Arg.Any<CancellationToken>());
    }

    /// <summary>Manual cancellation still abandons using a non-cancelled settlement token.</summary>
    /// <returns>The cancellation test.</returns>
    [Fact]
    public async Task ProcessMessageAsync_ManualCancellation_AbandonsWithoutProcessingToken()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var pipeline = Substitute.For<IMessagePipeline>();
        pipeline.ProcessAsync(Arg.Any<ITransportEnvelope>(), Arg.Any<MessageContext>(), cancellation.Token)
            .Returns(Task.FromCanceled<MessageProcessingResult>(cancellation.Token));
        await using var fixture = CreateConsumer(pipeline, autoComplete: false);
        await using var receiver = Substitute.For<ServiceBusReceiver>();
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(body: BinaryData.FromString("{}"), messageId: "cancelled");

        await InvokeProcessingAsync(fixture.Consumer, "ProcessMessageAsync", new ProcessMessageEventArgs(message, receiver, cancellation.Token));

        await receiver.Received(1).AbandonMessageAsync(message, Arg.Any<IDictionary<string, object>>(), CancellationToken.None);
        await receiver.DidNotReceive().CompleteMessageAsync(message, Arg.Any<CancellationToken>());
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "ServiceBusTransportConsumer and ServiceProvider ownership is transferred to ConsumerFixture, which disposes both in DisposeAsync. CA2000 cannot trace cross-method ownership transfer through a primary-constructor capture (documented analyzer limitation).")]
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

    private sealed class ConsumerFixture(ServiceBusTransportConsumer consumer, ServiceProvider provider) : IAsyncDisposable
    {
        public ServiceBusTransportConsumer Consumer { get; } = consumer;

        private ServiceProvider Provider { get; } = provider;

        public async ValueTask DisposeAsync()
        {
            await Consumer.DisposeAsync();
            await Provider.DisposeAsync();
        }
    }
}
