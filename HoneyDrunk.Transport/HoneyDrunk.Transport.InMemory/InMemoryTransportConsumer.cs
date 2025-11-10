using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.Configuration;
using HoneyDrunk.Transport.Pipeline;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HoneyDrunk.Transport.InMemory;

/// <summary>
/// In-memory implementation of transport consumer.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="InMemoryTransportConsumer"/> class.
/// </remarks>
/// <param name="broker">The in-memory message broker.</param>
/// <param name="pipeline">The message processing pipeline.</param>
/// <param name="options">The transport configuration options.</param>
/// <param name="logger">The logger instance.</param>
public sealed class InMemoryTransportConsumer(
    InMemoryBroker broker,
    IMessagePipeline pipeline,
    IOptions<TransportOptions> options,
    ILogger<InMemoryTransportConsumer> logger) : ITransportConsumer
{
    private readonly InMemoryBroker _broker = broker;
    private readonly IMessagePipeline _pipeline = pipeline;
    private readonly IOptions<TransportOptions> _options = options;
    private readonly ILogger<InMemoryTransportConsumer> _logger = logger;
    private CancellationTokenSource? _cts;
    private Task? _consumeTask;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_cts != null)
        {
            throw new InvalidOperationException("Consumer is already started");
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Starting in-memory consumer for endpoint {EndpointName}",
                _options.Value.EndpointName);
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Start multiple concurrent consumers based on MaxConcurrency
        var tasks = new List<Task>();
        for (int i = 0; i < _options.Value.MaxConcurrency; i++)
        {
            var consumerId = i;
            var task = Task.Run(async () =>
            {
                await ConsumeMessagesAsync(consumerId, _cts.Token);
            }, _cts.Token);
            tasks.Add(task);
        }

        _consumeTask = Task.WhenAll(tasks);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_cts == null)
        {
            return;
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Stopping in-memory consumer for endpoint {EndpointName}",
                _options.Value.EndpointName);
        }

        _cts.Cancel();

        if (_consumeTask != null)
        {
            try
            {
                await _consumeTask;
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
            }
        }

        _cts.Dispose();
        _cts = null;
        _consumeTask = null;

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Consumer stopped");
        }
    }

    private async Task ConsumeMessagesAsync(int consumerId, CancellationToken cancellationToken)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Consumer {ConsumerId} started for address {Address}",
                consumerId,
                _options.Value.Address);
        }

        try
        {
            await _broker.ConsumeAsync(
                _options.Value.Address,
                async (envelope, ct) => await ProcessMessageAsync(envelope, ct),
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Expected when stopping
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(
                    ex,
                    "Consumer {ConsumerId} encountered an error",
                    consumerId);
            }
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Consumer {ConsumerId} stopped",
                consumerId);
        }
    }

    private async Task ProcessMessageAsync(ITransportEnvelope envelope, CancellationToken cancellationToken)
    {
        var context = new MessageContext
        {
            Envelope = envelope,
            Transaction = NoOpTransportTransaction.Instance,
            DeliveryCount = 1
        };

        try
        {
            var result = await _pipeline.ProcessAsync(envelope, context, cancellationToken);

            if (result != MessageProcessingResult.Success)
            {
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning(
                        "Message {MessageId} processing returned {Result}",
                        envelope.MessageId,
                        result);
                }

                // In-memory transport doesn't support retry/DLQ, so we just log
                if (result == MessageProcessingResult.Retry)
                {
                    // Could re-publish to the same queue for simple retry
                    await _broker.PublishAsync(_options.Value.Address, envelope, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(
                    ex,
                    "Failed to process message {MessageId}",
                    envelope.MessageId);
            }

            // In a real transport, this would trigger retry logic
        }
    }
}
