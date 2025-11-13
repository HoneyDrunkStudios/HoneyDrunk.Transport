using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Azure;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.Pipeline;
using HoneyDrunk.Transport.Primitives;
using HoneyDrunk.Transport.StorageQueue.Configuration;
using HoneyDrunk.Transport.Telemetry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HoneyDrunk.Transport.StorageQueue.Internal;

/// <summary>
/// Azure Storage Queue implementation of transport consumer.
/// </summary>
/// <remarks>
/// <para>
/// Implements a two-level concurrency model:
/// </para>
/// <list type="bullet">
/// <item><description><strong>MaxConcurrency</strong>: Number of concurrent fetch loops (default: 5).</description></item>
/// <item><description><strong>BatchProcessingConcurrency</strong>: Concurrent messages per fetch loop (default: 1).</description></item>
/// </list>
/// <para>
/// Total concurrent message processing = MaxConcurrency × BatchProcessingConcurrency.
/// </para>
/// <para>
/// <strong>Example:</strong> MaxConcurrency=5, BatchProcessingConcurrency=4 = 20 concurrent operations.
/// </para>
/// </remarks>
/// <param name="queueClientFactory">The queue client factory.</param>
/// <param name="pipeline">The message processing pipeline.</param>
/// <param name="poisonQueueMover">The poison queue mover for handling dead-lettered messages.</param>
/// <param name="options">The storage queue configuration options.</param>
/// <param name="logger">The logger instance.</param>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by dependency injection")]
internal sealed class StorageQueueProcessor(
    QueueClientFactory queueClientFactory,
    IMessagePipeline pipeline,
    PoisonQueueMover poisonQueueMover,
    IOptions<StorageQueueOptions> options,
    ILogger<StorageQueueProcessor> logger) : ITransportConsumer, IAsyncDisposable
{
    private static readonly JsonSerializerOptions DeserializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly QueueClientFactory _queueClientFactory = queueClientFactory;
    private readonly IMessagePipeline _pipeline = pipeline;
    private readonly PoisonQueueMover _poisonMover = poisonQueueMover;
    private readonly StorageQueueOptions _options = options.Value;
    private readonly ILogger<StorageQueueProcessor> _logger = logger;
    private readonly SemaphoreSlim _startStopLock = new(1, 1);
    private CancellationTokenSource? _cts;
    private Task? _processingTask;
    private bool _disposed;

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _startStopLock.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_cts != null)
            {
                throw new InvalidOperationException("Consumer is already started");
            }

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Starting Storage Queue consumer for queue {QueueName}",
                    _options.QueueName);
            }

            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // Start multiple concurrent consumers based on MaxConcurrency
            var tasks = new List<Task>();
            for (int i = 0; i < _options.MaxConcurrency; i++)
            {
                var consumerId = i;
                var task = Task.Run(
                    async () =>
                    {
                        await ConsumeMessagesAsync(consumerId, _cts.Token);
                    },
                    _cts.Token);
                tasks.Add(task);
            }

            _processingTask = Task.WhenAll(tasks);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Started {ConsumerCount} concurrent consumers for queue {QueueName}",
                    _options.MaxConcurrency,
                    _options.QueueName);
            }
        }
        finally
        {
            _startStopLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _startStopLock.WaitAsync(cancellationToken);
        try
        {
            if (_cts == null)
            {
                return;
            }

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Stopping Storage Queue consumer for queue {QueueName}",
                    _options.QueueName);
            }

            await _cts.CancelAsync();

            if (_processingTask != null)
            {
                try
                {
                    await _processingTask;
                }
                catch (OperationCanceledException)
                {
                    // Expected when stopping
                }
            }

            _cts.Dispose();
            _cts = null;
            _processingTask = null;

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Consumer stopped");
            }
        }
        finally
        {
            _startStopLock.Release();
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
        finally
        {
            _startStopLock.Dispose();
            await _queueClientFactory.DisposeAsync();
        }
    }

    /// <summary>
    /// Deserializes a Storage Queue message to a transport envelope.
    /// </summary>
    private static TransportEnvelope DeserializeEnvelope(QueueMessage message)
    {
        var messageText = message.Body.ToString();
        var queueEnvelope = JsonSerializer.Deserialize<StorageQueueEnvelope>(messageText, DeserializerOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize message {message.MessageId}");

        var payloadBytes = Convert.FromBase64String(queueEnvelope.PayloadBase64);

        return new TransportEnvelope
        {
            MessageId = queueEnvelope.MessageId,
            CorrelationId = queueEnvelope.CorrelationId,
            CausationId = queueEnvelope.CausationId,
            Timestamp = queueEnvelope.Timestamp,
            MessageType = queueEnvelope.MessageType,
            Headers = new Dictionary<string, string>(queueEnvelope.Headers),
            Payload = payloadBytes
        };
    }

    /// <summary>
    /// Determines if an Azure Storage error is transient.
    /// </summary>
    private static bool IsTransientError(RequestFailedException ex)
    {
        // HTTP 5xx errors (server errors) are typically transient
        return ex.Status >= 500 && ex.Status < 600;
    }

    /// <summary>
    /// Background consumer loop for receiving and processing messages.
    /// </summary>
    private async Task ConsumeMessagesAsync(int consumerId, CancellationToken cancellationToken)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Consumer {ConsumerId} started for queue {QueueName}",
                consumerId,
                _options.QueueName);
        }

        // Per-consumer backoff state (thread-safe - each consumer has its own)
        var backoffState = new ConsumerBackoffState(_options.EmptyQueuePollingInterval);

        try
        {
            var queueClient = await _queueClientFactory.GetOrCreatePrimaryQueueClientAsync(cancellationToken);
            var poisonQueueClient = await _queueClientFactory.GetOrCreatePoisonQueueClientAsync(cancellationToken);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Receive messages from queue
                    var messages = await ReceiveMessagesAsync(queueClient, cancellationToken);

                    if (messages.Length == 0)
                    {
                        // Queue is empty, apply exponential backoff
                        await HandleEmptyQueueAsync(consumerId, backoffState, cancellationToken);
                        continue;
                    }

                    // Reset backoff since we got messages
                    backoffState.Reset(_options.EmptyQueuePollingInterval);

                    // Process messages with configured batch concurrency
                    if (_options.BatchProcessingConcurrency == 1)
                    {
                        // Sequential processing (default, backward compatible)
                        foreach (var message in messages)
                        {
                            await ProcessMessageAsync(
                                queueClient,
                                poisonQueueClient,
                                message,
                                cancellationToken);
                        }
                    }
                    else
                    {
                        // Parallel processing within batch
                        using var semaphore = new SemaphoreSlim(_options.BatchProcessingConcurrency);
                        var tasks = messages.Select(async message =>
                        {
                            await semaphore.WaitAsync(cancellationToken);
                            try
                            {
                                await ProcessMessageAsync(
                                    queueClient,
                                    poisonQueueClient,
                                    message,
                                    cancellationToken);
                            }
                            finally
                            {
                                semaphore.Release();
                            }
                        });

                        await Task.WhenAll(tasks);
                    }
                }
                catch (RequestFailedException ex) when (IsTransientError(ex))
                {
                    if (_logger.IsEnabled(LogLevel.Warning))
                    {
                        _logger.LogWarning(
                            ex,
                            "Consumer {ConsumerId} encountered transient error, will retry",
                            consumerId);
                    }

                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // Expected when stopping
                    break;
                }
                catch (Exception ex)
                {
                    if (_logger.IsEnabled(LogLevel.Error))
                    {
                        _logger.LogError(
                            ex,
                            "Consumer {ConsumerId} encountered unexpected error",
                            consumerId);
                    }

                    await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                }
            }
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
                    "Consumer {ConsumerId} fatal error",
                    consumerId);
            }
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Consumer {ConsumerId} stopped", consumerId);
        }
    }

    /// <summary>
    /// Receives messages from the queue.
    /// </summary>
    private async Task<QueueMessage[]> ReceiveMessagesAsync(
        QueueClient queueClient,
        CancellationToken cancellationToken)
    {
        var response = await queueClient.ReceiveMessagesAsync(
            maxMessages: _options.PrefetchMaxMessages,
            visibilityTimeout: _options.VisibilityTimeout,
            cancellationToken: cancellationToken);

        return response.Value ?? [];
    }

    /// <summary>
    /// Processes a single message through the pipeline.
    /// </summary>
    private async Task ProcessMessageAsync(
        QueueClient queueClient,
        QueueClient poisonQueueClient,
        QueueMessage message,
        CancellationToken cancellationToken)
    {
        try
        {
            // Deserialize envelope
            var envelope = DeserializeEnvelope(message);

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Processing message {MessageId} (dequeue count: {DequeueCount})",
                    envelope.MessageId,
                    message.DequeueCount);
            }

            using var activity = _options.EnableTelemetry
                ? TransportTelemetry.StartProcessActivity(envelope, _options.QueueName)
                : null;

            TransportTelemetry.RecordDeliveryCount(activity, (int)message.DequeueCount);

            // Create message context
            var context = new MessageContext
            {
                Envelope = envelope,
                Transaction = NoOpTransportTransaction.Instance,
                DeliveryCount = (int)message.DequeueCount
            };

            try
            {
                // Process through pipeline
                var result = await _pipeline.ProcessAsync(envelope, context, cancellationToken);

                if (result == MessageProcessingResult.Success)
                {
                    // Delete message from queue
                    await queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt, cancellationToken);

                    TransportTelemetry.RecordOutcome(activity, MessageProcessingResult.Success);

                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug("Successfully processed message {MessageId}", envelope.MessageId);
                    }
                }
                else if (result == MessageProcessingResult.DeadLetter)
                {
                    // Move to poison queue immediately
                    // Note: If deletion fails after poison queue insertion, message may be reprocesseds
                    // The PoisonQueueMover includes MessageId which can be used for deduplication
                    try
                    {
                        await _poisonMover.MoveMessageToPoisonQueueAsync(
                            poisonQueueClient,
                            message,
                            null,
                            message.DequeueCount,
                            cancellationToken);

                        // Delete from primary queue only after successful poison queue insertion
                        await queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt, cancellationToken);

                        TransportTelemetry.RecordOutcome(activity, MessageProcessingResult.DeadLetter);
                    }
                    catch (Exception poisonEx)
                    {
                        if (_logger.IsEnabled(LogLevel.Error))
                        {
                            _logger.LogError(
                                poisonEx,
                                "Failed to complete dead-letter operation for message {MessageId}. Message will be retried and may appear as duplicate in poison queue.",
                                envelope.MessageId);
                        }

                        // Don't throw - let message become visible again for retry
                        // Poison queue deduplication should be handled by MessageId
                        TransportTelemetry.RecordError(activity, poisonEx);
                    }
                }
                else
                {
                    // Retry: Check if max dequeue count exceeded
                    if (message.DequeueCount >= _options.MaxDequeueCount)
                    {
                        if (_logger.IsEnabled(LogLevel.Warning))
                        {
                            _logger.LogWarning(
                                "Message {MessageId} exceeded max dequeue count ({MaxDequeueCount}), moving to poison queue",
                                envelope.MessageId,
                                _options.MaxDequeueCount);
                        }

                        try
                        {
                            await _poisonMover.MoveMessageToPoisonQueueAsync(
                                poisonQueueClient,
                                message,
                                null,
                                message.DequeueCount,
                                cancellationToken);

                            // Delete from primary queue only after successful poison queue insertion
                            await queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt, cancellationToken);

                            TransportTelemetry.RecordOutcome(activity, MessageProcessingResult.DeadLetter);
                        }
                        catch (Exception poisonEx)
                        {
                            if (_logger.IsEnabled(LogLevel.Error))
                            {
                                _logger.LogError(
                                    poisonEx,
                                    "Failed to complete poison queue operation for message {MessageId}. Message will be retried and may appear as duplicate in poison queue.",
                                    envelope.MessageId);
                            }

                            // Don't throw - let message become visible again for retry
                            TransportTelemetry.RecordError(activity, poisonEx);
                        }
                    }
                    else
                    {
                        // Message will become visible again after VisibilityTimeout.
                        // Optionally extend visibility for longer retry delays.
                        TransportTelemetry.RecordOutcome(activity, MessageProcessingResult.Retry);

                        if (_logger.IsEnabled(LogLevel.Debug))
                        {
                            _logger.LogDebug(
                                "Message {MessageId} will be retried (dequeue count: {DequeueCount})",
                                envelope.MessageId,
                                message.DequeueCount);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                TransportTelemetry.RecordError(activity, ex);
                throw;
            }
        }
        catch (Exception processingError)
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(
                    processingError,
                    "Failed to process message {MessageId}",
                    message.MessageId);
            }

            // Check if message should be poisoned
            if (message.DequeueCount >= _options.MaxDequeueCount)
            {
                try
                {
                    await _poisonMover.MoveMessageToPoisonQueueAsync(
                        poisonQueueClient,
                        message,
                        processingError,
                        message.DequeueCount,
                        cancellationToken);

                    await queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt, cancellationToken);
                }
                catch (Exception poisonEx)
                {
                    if (_logger.IsEnabled(LogLevel.Error))
                    {
                        _logger.LogError(
                            poisonEx,
                            "Failed to move message {MessageId} to poison queue",
                            message.MessageId);
                    }
                }
            }

            // Otherwise, message will reappear after visibility timeout for retry
        }
    }

    /// <summary>
    /// Handles empty queue scenario with exponential backoff.
    /// </summary>
    private async Task HandleEmptyQueueAsync(
        int consumerId,
        ConsumerBackoffState backoffState,
        CancellationToken cancellationToken)
    {
        backoffState.IncrementEmptyReceive();

        // Apply exponential backoff with jitter
        backoffState.ApplyExponentialBackoff(_options.MaxPollingInterval);

        // Add jitter to prevent thundering herd
        // Random factor ranges from -0.25 to +0.25, producing 75%-125% of base delay
        var jitter = (Random.Shared.NextDouble() * 0.5) - 0.25;
        var delayMs = backoffState.CurrentPollingInterval.TotalMilliseconds * (1 + jitter);

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace(
                "Consumer {ConsumerId} queue empty, waiting {DelayMs}ms (base: {BaseMs}ms)",
                consumerId,
                delayMs,
                backoffState.CurrentPollingInterval.TotalMilliseconds);
        }

        await Task.Delay(TimeSpan.FromMilliseconds(delayMs), cancellationToken);
    }

    /// <summary>
    /// Per-consumer backoff state to avoid race conditions between concurrent consumers.
    /// </summary>
    /// <remarks>
    /// Thread-safety: Each consumer task has its own instance accessed sequentially within
    /// that consumer's loop. No synchronization needed as methods are never called concurrently
    /// within the same consumer.
    /// </remarks>
    private sealed class ConsumerBackoffState(TimeSpan initialPollingInterval)
    {
        private int _consecutiveEmptyReceives;

        public TimeSpan CurrentPollingInterval { get; private set; } = initialPollingInterval;

        /// <summary>
        /// Increments the empty receive counter.
        /// </summary>
        public void IncrementEmptyReceive()
        {
            _consecutiveEmptyReceives++;
        }

        /// <summary>
        /// Applies exponential backoff to the current polling interval.
        /// </summary>
        /// <param name="maxPollingInterval">The maximum allowed polling interval.</param>
        public void ApplyExponentialBackoff(TimeSpan maxPollingInterval)
        {
            if (_consecutiveEmptyReceives > 1)
            {
                CurrentPollingInterval = TimeSpan.FromMilliseconds(
                    Math.Min(
                        CurrentPollingInterval.TotalMilliseconds * 1.5,
                        maxPollingInterval.TotalMilliseconds));
            }
        }

        /// <summary>
        /// Resets the backoff state when messages are received.
        /// </summary>
        /// <param name="initialPollingInterval">The initial polling interval to reset to.</param>
        public void Reset(TimeSpan initialPollingInterval)
        {
            if (_consecutiveEmptyReceives > 0)
            {
                _consecutiveEmptyReceives = 0;
                CurrentPollingInterval = initialPollingInterval;
            }
        }
    }
}
