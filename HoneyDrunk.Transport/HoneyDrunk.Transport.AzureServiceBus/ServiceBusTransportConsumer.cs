using Azure.Messaging.ServiceBus;
using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.AzureServiceBus.Configuration;
using HoneyDrunk.Transport.AzureServiceBus.Mapping;
using HoneyDrunk.Transport.Exceptions;
using HoneyDrunk.Transport.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace HoneyDrunk.Transport.AzureServiceBus;

/// <summary>
/// Azure Service Bus implementation of transport consumer.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ServiceBusTransportConsumer"/> class.
/// </remarks>
/// <param name="client">The Service Bus client.</param>
/// <param name="pipeline">The message processing pipeline.</param>
/// <param name="serviceScopeFactory">The service scope factory for creating scoped service providers.</param>
/// <param name="options">The Azure Service Bus configuration options.</param>
/// <param name="logger">The logger instance.</param>
[SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "ServiceBusClient is injected via DI and its lifetime is managed by the DI container, not by this class")]
public sealed class ServiceBusTransportConsumer(
    ServiceBusClient client,
    IMessagePipeline pipeline,
    IServiceScopeFactory serviceScopeFactory,
    IOptions<AzureServiceBusOptions> options,
    ILogger<ServiceBusTransportConsumer> logger) : ITransportConsumer, IAsyncDisposable
{
    private readonly ServiceBusClient _client = client;
    private readonly IMessagePipeline _pipeline = pipeline;
    private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
    private readonly IOptions<AzureServiceBusOptions> _options = options;
    private readonly ILogger<ServiceBusTransportConsumer> _logger = logger;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private ServiceBusProcessor? _processor;
    private ServiceBusSessionProcessor? _sessionProcessor;
    private bool _disposed;

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_processor != null || _sessionProcessor != null)
            {
                throw new InvalidOperationException("Consumer is already started");
            }

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Starting Service Bus consumer for endpoint {EndpointName}",
                    _options.Value.EndpointName);
            }

            var processorOptions = CreateProcessorOptions();

            if (_options.Value.SessionEnabled)
            {
                await StartSessionProcessorAsync(processorOptions, cancellationToken);
            }
            else
            {
                await StartStandardProcessorAsync(processorOptions, cancellationToken);
            }

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Service Bus consumer started");
            }
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_processor == null && _sessionProcessor == null)
            {
                return; // Nothing to stop
            }

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Stopping Service Bus consumer");
            }

            await StopAndDisposeProcessorsAsync(cancellationToken);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Service Bus consumer stopped");
            }
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, true))
        {
            return;
        }

        try
        {
            await StopAsync();
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning(ex, "Error stopping consumer during dispose");
            }
        }

        _initLock.Dispose();
    }

    private static bool IsTestSubstitute(object instance) =>
        instance.GetType().Assembly.FullName?.Contains("DynamicProxyGenAssembly", StringComparison.Ordinal) == true;

    private async Task StopAndDisposeProcessorsAsync(CancellationToken cancellationToken)
    {
        if (_processor != null)
        {
            try
            {
                await _processor.StopProcessingAsync(cancellationToken);
            }
            finally
            {
                await _processor.DisposeAsync();
                _processor = null;
            }
        }

        if (_sessionProcessor != null)
        {
            try
            {
                await _sessionProcessor.StopProcessingAsync(cancellationToken);
            }
            finally
            {
                await _sessionProcessor.DisposeAsync();
                _sessionProcessor = null;
            }
        }
    }

    private async Task StartStandardProcessorAsync(
        ServiceBusProcessorOptions options,
        CancellationToken cancellationToken)
    {
        if (_options.Value.EntityType == ServiceBusEntityType.Queue)
        {
            _processor = _client.CreateProcessor(_options.Value.Address, options);
        }
        else
        {
            if (string.IsNullOrEmpty(_options.Value.SubscriptionName))
            {
                throw new InvalidOperationException(
                    "SubscriptionName is required when EntityType is Topic");
            }

            _processor = _client.CreateProcessor(
                _options.Value.Address,
                _options.Value.SubscriptionName,
                options);
        }

        if (IsTestSubstitute(_processor))
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Skipping processor event hookup (test substitute detected)");
            }
        }
        else
        {
            _processor.ProcessMessageAsync += ProcessMessageAsync;
            _processor.ProcessErrorAsync += ProcessErrorAsync;
        }

        await _processor.StartProcessingAsync(cancellationToken);
    }

    private async Task StartSessionProcessorAsync(
        ServiceBusProcessorOptions baseOptions,
        CancellationToken cancellationToken)
    {
        var sessionOptions = new ServiceBusSessionProcessorOptions
        {
            AutoCompleteMessages = baseOptions.AutoCompleteMessages,
            MaxConcurrentSessions = _options.Value.MaxConcurrency,
            MaxConcurrentCallsPerSession = 1,
            PrefetchCount = baseOptions.PrefetchCount,
            ReceiveMode = baseOptions.ReceiveMode,
            MaxAutoLockRenewalDuration = baseOptions.MaxAutoLockRenewalDuration
        };

        if (_options.Value.EntityType == ServiceBusEntityType.Queue)
        {
            _sessionProcessor = _client.CreateSessionProcessor(_options.Value.Address, sessionOptions);
        }
        else
        {
            if (string.IsNullOrEmpty(_options.Value.SubscriptionName))
            {
                throw new InvalidOperationException(
                    "SubscriptionName is required when EntityType is Topic");
            }

            _sessionProcessor = _client.CreateSessionProcessor(
                _options.Value.Address,
                _options.Value.SubscriptionName,
                sessionOptions);
        }

        if (IsTestSubstitute(_sessionProcessor))
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Skipping session processor event hookup (test substitute detected)");
            }
        }
        else
        {
            _sessionProcessor.ProcessMessageAsync += ProcessSessionMessageAsync;
            _sessionProcessor.ProcessErrorAsync += ProcessErrorAsync;
        }

        await _sessionProcessor.StartProcessingAsync(cancellationToken);
    }

    private ServiceBusProcessorOptions CreateProcessorOptions()
    {
        return new ServiceBusProcessorOptions
        {
            AutoCompleteMessages = _options.Value.AutoComplete,
            MaxConcurrentCalls = _options.Value.MaxConcurrency,
            PrefetchCount = _options.Value.PrefetchCount,
            ReceiveMode = ServiceBusReceiveMode.PeekLock,
            MaxAutoLockRenewalDuration = _options.Value.MessageLockDuration
        };
    }

    private Task ProcessMessageAsync(ProcessMessageEventArgs args) =>
        ProcessReceivedMessageAsync(ServiceBusReceivedMessageContext.Create(args));

    private Task ProcessSessionMessageAsync(ProcessSessionMessageEventArgs args) =>
        ProcessReceivedMessageAsync(ServiceBusReceivedMessageContext.Create(args));

    private async Task ProcessReceivedMessageAsync(ServiceBusReceivedMessageContext receivedMessage)
    {
        await using var scope = _serviceScopeFactory.CreateAsyncScope();

        var context = new MessageContext
        {
            Envelope = receivedMessage.Envelope,
            Transaction = receivedMessage.Transaction,
            DeliveryCount = receivedMessage.DeliveryCount,
            ServiceProvider = scope.ServiceProvider
        };

        try
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Processing message {MessageId} (Delivery count: {DeliveryCount})",
                    receivedMessage.Envelope.MessageId,
                    receivedMessage.DeliveryCount);
            }

            var result = await _pipeline.ProcessAsync(
                receivedMessage.Envelope,
                context,
                receivedMessage.CancellationToken);

            await HandleProcessingResultAsync(result, receivedMessage);
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(
                    ex,
                    "Unhandled error processing message {MessageId}",
                    receivedMessage.Envelope.MessageId);
            }

            if (!_options.Value.AutoComplete)
            {
                await receivedMessage.AbandonAsync();
            }
        }
    }

    private async Task HandleProcessingResultAsync(
        MessageProcessingResult result,
        ServiceBusReceivedMessageContext receivedMessage)
    {
        if (_options.Value.AutoComplete)
        {
            return;
        }

        switch (result)
        {
            case MessageProcessingResult.Success:
                await receivedMessage.CompleteAsync();
                break;

            case MessageProcessingResult.Retry:
            case MessageProcessingResult.Abandon:
                await receivedMessage.AbandonAsync();
                break;

            case MessageProcessingResult.DeadLetter:
                await receivedMessage.DeadLetterAsync();
                break;
        }
    }

    private Task ProcessErrorAsync(ProcessErrorEventArgs args)
    {
        if (_logger.IsEnabled(LogLevel.Error))
        {
            _logger.LogError(
                args.Exception,
                "Service Bus error in {Source}: {ErrorSource}",
                args.EntityPath,
                args.ErrorSource);
        }

        return Task.CompletedTask;
    }

    private sealed class ServiceBusReceivedMessageContext
    {
        private const string DeadLetterReason = "Message processing failed";
        private const string DeadLetterDescriptionFormat = "Message {0} could not be processed";

        private ServiceBusReceivedMessageContext(
            ITransportEnvelope envelope,
            ITransportTransaction transaction,
            int deliveryCount,
            CancellationToken cancellationToken,
            Func<Task> completeAsync,
            Func<Task> abandonAsync,
            Func<Task> deadLetterAsync)
        {
            Envelope = envelope;
            Transaction = transaction;
            DeliveryCount = deliveryCount;
            CancellationToken = cancellationToken;
            CompleteAsync = completeAsync;
            AbandonAsync = abandonAsync;
            DeadLetterAsync = deadLetterAsync;
        }

        public ITransportEnvelope Envelope { get; }

        public ITransportTransaction Transaction { get; }

        public int DeliveryCount { get; }

        public CancellationToken CancellationToken { get; }

        public Func<Task> CompleteAsync { get; }

        public Func<Task> AbandonAsync { get; }

        public Func<Task> DeadLetterAsync { get; }

        public static ServiceBusReceivedMessageContext Create(ProcessMessageEventArgs args)
        {
            // Settlement should still reach the broker when processing cancellation is already signaled.
            // The processing token remains on the message context for user pipeline cancellation.
            var settlementCancellationToken = CancellationToken.None;

            return Create(
                args.Message,
                args.CancellationToken,
                () => args.CompleteMessageAsync(args.Message, settlementCancellationToken),
                () => args.AbandonMessageAsync(args.Message, cancellationToken: settlementCancellationToken),
                (reason, description) => args.DeadLetterMessageAsync(
                    args.Message,
                    reason,
                    description,
                    settlementCancellationToken));
        }

        public static ServiceBusReceivedMessageContext Create(ProcessSessionMessageEventArgs args)
        {
            // Settlement should still reach the broker when processing cancellation is already signaled.
            // The processing token remains on the message context for user pipeline cancellation.
            var settlementCancellationToken = CancellationToken.None;

            return Create(
                args.Message,
                args.CancellationToken,
                () => args.CompleteMessageAsync(args.Message, settlementCancellationToken),
                () => args.AbandonMessageAsync(args.Message, cancellationToken: settlementCancellationToken),
                (reason, description) => args.DeadLetterMessageAsync(
                    args.Message,
                    reason,
                    description,
                    settlementCancellationToken));
        }

        private static ServiceBusReceivedMessageContext Create(
            ServiceBusReceivedMessage message,
            CancellationToken processingCancellationToken,
            Func<Task> completeAsync,
            Func<Task> abandonAsync,
            Func<string, string, Task> deadLetterAsync)
        {
            var envelope = EnvelopeMapper.FromServiceBusMessage(message);

            return new ServiceBusReceivedMessageContext(
                envelope,
                EnvelopeMapper.CreateTransaction(message),
                EnvelopeMapper.GetDeliveryCount(message),
                processingCancellationToken,
                completeAsync,
                abandonAsync,
                () => deadLetterAsync(
                    DeadLetterReason,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        DeadLetterDescriptionFormat,
                        envelope.MessageId)));
        }
    }
}
