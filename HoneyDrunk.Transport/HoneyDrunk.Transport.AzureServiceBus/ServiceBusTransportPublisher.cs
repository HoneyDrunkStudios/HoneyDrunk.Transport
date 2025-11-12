using System.Diagnostics.CodeAnalysis;
using Azure.Messaging.ServiceBus;
using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.AzureServiceBus.Configuration;
using HoneyDrunk.Transport.AzureServiceBus.Mapping;
using HoneyDrunk.Transport.Exceptions;
using HoneyDrunk.Transport.Telemetry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HoneyDrunk.Transport.AzureServiceBus;

/// <summary>
/// Azure Service Bus implementation of transport publisher.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ServiceBusTransportPublisher"/> class.
/// </remarks>
/// <param name="client">The Service Bus client.</param>
/// <param name="options">The Azure Service Bus configuration options.</param>
/// <param name="logger">The logger instance.</param>
[SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "ServiceBusClient is injected via DI and its lifetime is managed by the DI container, not by this class")]
public sealed class ServiceBusTransportPublisher(
    ServiceBusClient client,
    IOptions<AzureServiceBusOptions> options,
    ILogger<ServiceBusTransportPublisher> logger) : ITransportPublisher, IAsyncDisposable
{
    private readonly ServiceBusClient _client = client;
    private readonly IOptions<AzureServiceBusOptions> _options = options;
    private readonly ILogger<ServiceBusTransportPublisher> _logger = logger;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private ServiceBusSender? _sender;
    private bool _disposed;

    /// <inheritdoc />
    public async Task PublishAsync(
        ITransportEnvelope envelope,
        IEndpointAddress destination,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await EnsureInitializedAsync(cancellationToken);

        using var activity = TransportTelemetry.StartPublishActivity(envelope, destination);

        try
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Publishing message {MessageId} to {Destination}",
                    envelope.MessageId,
                    destination.Address);
            }

            var serviceBusMessage = EnvelopeMapper.ToServiceBusMessage(envelope);

            // Apply partition key if provided
            if (destination.Properties.TryGetValue("PartitionKey", out var partitionKey))
            {
                serviceBusMessage.PartitionKey = partitionKey;
            }

            // Apply session ID if provided
            if (destination.Properties.TryGetValue("SessionId", out var sessionId))
            {
                serviceBusMessage.SessionId = sessionId;
            }

            await _sender!.SendMessageAsync(serviceBusMessage, cancellationToken);

            TransportTelemetry.RecordOutcome(activity, MessageProcessingResult.Success);

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Successfully published message {MessageId}",
                    envelope.MessageId);
            }
        }
        catch (Exception ex)
        {
            TransportTelemetry.RecordError(activity, ex);

            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(
                    ex,
                    "Failed to publish message {MessageId}",
                    envelope.MessageId);
            }

            throw;
        }
    }

    /// <inheritdoc />
    public async Task PublishBatchAsync(
        IEnumerable<ITransportEnvelope> envelopes,
        IEndpointAddress destination,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await EnsureInitializedAsync(cancellationToken);

        var messages = envelopes
            .Select(envelope =>
            {
                var message = EnvelopeMapper.ToServiceBusMessage(envelope);

                // Apply partition key if provided
                if (destination.Properties.TryGetValue("PartitionKey", out var partitionKey))
                {
                    message.PartitionKey = partitionKey;
                }

                // Apply session ID if provided
                if (destination.Properties.TryGetValue("SessionId", out var sessionId))
                {
                    message.SessionId = sessionId;
                }

                return message;
            })
            .ToList();

        try
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Publishing batch of {Count} messages to {Destination}",
                    messages.Count,
                    destination.Address);
            }

            await SendMessageBatchesAsync(messages, cancellationToken);

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Successfully published batch");
            }
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(ex, "Failed to publish batch");
            }

            throw;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        // Thread-safe disposal check using Interlocked.Exchange
        // Atomically sets _disposed to true and returns the previous value
        // If previous value was true, we've already disposed
        if (Interlocked.Exchange(ref _disposed, true))
        {
            return;
        }

        // Acquire the initialization lock to ensure no concurrent initialization or disposal
        await _initLock.WaitAsync();
        try
        {
            if (_sender != null)
            {
                await _sender.DisposeAsync();
                _sender = null;
            }
        }
        finally
        {
            _initLock.Release();
            _initLock.Dispose();
        }
    }

    private async Task SendMessageBatchesAsync(List<ServiceBusMessage> messages, CancellationToken cancellationToken)
    {
        ServiceBusMessageBatch? currentBatch = await _sender!.CreateMessageBatchAsync(cancellationToken);

        try
        {
            foreach (var message in messages)
            {
                // Try to add message to current batch
                if (!currentBatch.TryAddMessage(message))
                {
                    // Current batch is full, send and dispose it
                    if (currentBatch.Count > 0)
                    {
                        await _sender!.SendMessagesAsync(currentBatch, cancellationToken);
                    }

                    currentBatch.Dispose();

                    // Create a new batch for the message that didn't fit
                    currentBatch = await _sender!.CreateMessageBatchAsync(cancellationToken);

                    // If message still doesn't fit in empty batch, it's too large
                    if (!currentBatch.TryAddMessage(message))
                    {
                        var error = new MessageTooLargeException(
                            message.MessageId,
                            message.Body.Length,
                            currentBatch.MaxSizeInBytes,
                            "Azure Service Bus");

                        if (_logger.IsEnabled(LogLevel.Error))
                        {
                            _logger.LogError(
                                error,
                                "Message {MessageId} exceeds maximum batch size ({MaxSize} bytes) and cannot be sent",
                                message.MessageId,
                                currentBatch.MaxSizeInBytes);
                        }

                        throw error;
                    }
                }
            }

            // Send any remaining messages in the final batch
            if (currentBatch.Count > 0)
            {
                await _sender!.SendMessagesAsync(currentBatch, cancellationToken);
            }
        }
        finally
        {
            // Ensure batch is disposed even if an exception occurs
            currentBatch?.Dispose();
        }
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_sender != null)
        {
            return;
        }

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            // Check disposal flag after acquiring lock
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_sender != null)
            {
                return;
            }

            var queueOrTopicName = _options.Value.Address;
            _sender = _client.CreateSender(queueOrTopicName);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Initialized Service Bus sender for {Entity}",
                    queueOrTopicName);
            }
        }
        finally
        {
            _initLock.Release();
        }
    }
}
