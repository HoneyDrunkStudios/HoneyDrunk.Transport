using Azure.Messaging.ServiceBus;
using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.AzureServiceBus.Configuration;
using HoneyDrunk.Transport.AzureServiceBus.Mapping;
using HoneyDrunk.Transport.Configuration;
using HoneyDrunk.Transport.Pipeline;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HoneyDrunk.Transport.AzureServiceBus;

/// <summary>
/// Azure Service Bus implementation of transport consumer.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ServiceBusTransportConsumer"/> class.
/// </remarks>
/// <param name="client">The Service Bus client.</param>
/// <param name="pipeline">The message processing pipeline.</param>
/// <param name="options">The Azure Service Bus configuration options.</param>
/// <param name="logger">The logger instance.</param>
public sealed class ServiceBusTransportConsumer(
    ServiceBusClient client,
    IMessagePipeline pipeline,
    IOptions<AzureServiceBusOptions> options,
    ILogger<ServiceBusTransportConsumer> logger) : ITransportConsumer, IAsyncDisposable
{
    private readonly ServiceBusClient _client = client;
    private readonly IMessagePipeline _pipeline = pipeline;
    private readonly IOptions<AzureServiceBusOptions> _options = options;
    private readonly ILogger<ServiceBusTransportConsumer> _logger = logger;
    private ServiceBusProcessor? _processor;
    private ServiceBusSessionProcessor? _sessionProcessor;
    private readonly SemaphoreSlim _initLock = new(1, 1);
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
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Stopping Service Bus consumer");
        }

        if (_processor != null)
        {
            await _processor.StopProcessingAsync(cancellationToken);
            await _processor.DisposeAsync();
            _processor = null;
        }

        if (_sessionProcessor != null)
        {
            await _sessionProcessor.StopProcessingAsync(cancellationToken);
            await _sessionProcessor.DisposeAsync();
            _sessionProcessor = null;
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Service Bus consumer stopped");
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

        _processor.ProcessMessageAsync += ProcessMessageAsync;
        _processor.ProcessErrorAsync += ProcessErrorAsync;

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

        _sessionProcessor.ProcessMessageAsync += ProcessSessionMessageAsync;
        _sessionProcessor.ProcessErrorAsync += ProcessErrorAsync;

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

    private async Task ProcessMessageAsync(ProcessMessageEventArgs args)
    {
        var envelope = EnvelopeMapper.FromServiceBusMessage(args.Message);
        var deliveryCount = EnvelopeMapper.GetDeliveryCount(args.Message);
        var transaction = EnvelopeMapper.CreateTransaction(args.Message);

        await ProcessMessageCoreAsync(envelope, transaction, deliveryCount, args, args.CancellationToken);
    }

    private async Task ProcessSessionMessageAsync(ProcessSessionMessageEventArgs args)
    {
        var envelope = EnvelopeMapper.FromServiceBusMessage(args.Message);
        var deliveryCount = EnvelopeMapper.GetDeliveryCount(args.Message);
        var transaction = EnvelopeMapper.CreateTransaction(args.Message);

        await ProcessMessageCoreAsync(envelope, transaction, deliveryCount, args, args.CancellationToken);
    }

    private async Task ProcessMessageCoreAsync(
        ITransportEnvelope envelope,
        ITransportTransaction transaction,
        int deliveryCount,
        object args,
        CancellationToken cancellationToken)
    {
        var context = new MessageContext
        {
            Envelope = envelope,
            Transaction = transaction,
            DeliveryCount = deliveryCount
        };

        try
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Processing message {MessageId} (Delivery count: {DeliveryCount})",
                    envelope.MessageId,
                    deliveryCount);
            }

            var result = await _pipeline.ProcessAsync(envelope, context, cancellationToken);

            await HandleProcessingResultAsync(result, args, envelope, cancellationToken);
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(
                    ex,
                    "Unhandled error processing message {MessageId}",
                    envelope.MessageId);
            }

            // Let Service Bus retry mechanism handle it
            if (args is ProcessMessageEventArgs msgArgs && !_options.Value.AutoComplete)
            {
                await msgArgs.AbandonMessageAsync(msgArgs.Message, cancellationToken: cancellationToken);
            }
            else if (args is ProcessSessionMessageEventArgs sessionArgs && !_options.Value.AutoComplete)
            {
                await sessionArgs.AbandonMessageAsync(sessionArgs.Message, cancellationToken: cancellationToken);
            }
        }
    }

    private async Task HandleProcessingResultAsync(
        MessageProcessingResult result,
        object args,
        ITransportEnvelope envelope,
        CancellationToken cancellationToken)
    {
        if (_options.Value.AutoComplete)
        {
            // Auto-complete is enabled; Service Bus handles it
            return;
        }

        switch (result)
        {
            case MessageProcessingResult.Success:
                await CompleteMessageAsync(args, cancellationToken);
                break;

            case MessageProcessingResult.Retry:
            case MessageProcessingResult.Abandon:
                await AbandonMessageAsync(args, cancellationToken);
                break;

            case MessageProcessingResult.DeadLetter:
                await DeadLetterMessageAsync(args, envelope, cancellationToken);
                break;
        }
    }

    private static async Task CompleteMessageAsync(object args, CancellationToken cancellationToken)
    {
        if (args is ProcessMessageEventArgs msgArgs)
        {
            await msgArgs.CompleteMessageAsync(msgArgs.Message, cancellationToken);
        }
        else if (args is ProcessSessionMessageEventArgs sessionArgs)
        {
            await sessionArgs.CompleteMessageAsync(sessionArgs.Message, cancellationToken);
        }
    }

    private static async Task AbandonMessageAsync(object args, CancellationToken cancellationToken)
    {
        if (args is ProcessMessageEventArgs msgArgs)
        {
            await msgArgs.AbandonMessageAsync(msgArgs.Message, cancellationToken: cancellationToken);
        }
        else if (args is ProcessSessionMessageEventArgs sessionArgs)
        {
            await sessionArgs.AbandonMessageAsync(sessionArgs.Message, cancellationToken: cancellationToken);
        }
    }

    private static async Task DeadLetterMessageAsync(
        object args,
        ITransportEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var reason = "Message processing failed";
        var description = $"Message {envelope.MessageId} could not be processed";

        if (args is ProcessMessageEventArgs msgArgs)
        {
            await msgArgs.DeadLetterMessageAsync(
                msgArgs.Message,
                reason,
                description,
                cancellationToken);
        }
        else if (args is ProcessSessionMessageEventArgs sessionArgs)
        {
            await sessionArgs.DeadLetterMessageAsync(
                sessionArgs.Message,
                reason,
                description,
                cancellationToken);
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

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        await StopAsync();
        _initLock.Dispose();
    }
}
