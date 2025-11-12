using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using Azure;
using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.Exceptions;
using HoneyDrunk.Transport.StorageQueue.Configuration;
using HoneyDrunk.Transport.Telemetry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HoneyDrunk.Transport.StorageQueue.Internal;

/// <summary>
/// Azure Storage Queue implementation of transport publisher.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="StorageQueueSender"/> class.
/// </remarks>
/// <param name="queueClientFactory">The queue client factory.</param>
/// <param name="options">The storage queue configuration options.</param>
/// <param name="logger">The logger instance.</param>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by dependency injection")]
internal sealed class StorageQueueSender(
    QueueClientFactory queueClientFactory,
    IOptions<StorageQueueOptions> options,
    ILogger<StorageQueueSender> logger) : ITransportPublisher, IAsyncDisposable
{
    // Azure Storage Queue max message size is ~64KB (base64-encoded)
    private const int MaxMessageSizeBytes = 64 * 1024;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly QueueClientFactory _queueClientFactory = queueClientFactory;
    private readonly StorageQueueOptions _options = options.Value;
    private readonly ILogger<StorageQueueSender> _logger = logger;
    private bool _disposed;

    /// <inheritdoc />
    public async Task PublishAsync(
        ITransportEnvelope envelope,
        IEndpointAddress destination,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(destination);

        using var activity = _options.EnableTelemetry
            ? TransportTelemetry.StartPublishActivity(envelope, destination)
            : null;

        try
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Publishing message {MessageId} of type {MessageType} to queue {QueueName}",
                    envelope.MessageId,
                    envelope.MessageType,
                    destination.Address);
            }

            // Serialize envelope to JSON
            var messageText = SerializeEnvelope(envelope);

            // Validate size
            var messageSizeBytes = Encoding.UTF8.GetByteCount(messageText);
            if (messageSizeBytes > MaxMessageSizeBytes)
            {
                var ex = new MessageTooLargeException(envelope.MessageId, messageSizeBytes, MaxMessageSizeBytes, "Azure Storage Queue");

                if (_logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(ex, "Message {MessageId} exceeds size limit", envelope.MessageId);
                }

                TransportTelemetry.RecordError(activity, ex);
                throw ex;
            }

            // Get queue client
            var queueClient = await _queueClientFactory.GetOrCreatePrimaryQueueClientAsync(cancellationToken);

            // Send message
            await queueClient.SendMessageAsync(
                messageText,
                visibilityTimeout: null, // Visible immediately
                timeToLive: _options.MessageTimeToLive,
                cancellationToken: cancellationToken);

            TransportTelemetry.RecordOutcome(activity, MessageProcessingResult.Success);

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Successfully published message {MessageId}", envelope.MessageId);
            }
        }
        catch (RequestFailedException ex) when (IsTransientError(ex))
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning(
                    ex,
                    "Transient error publishing message {MessageId}, may be retried",
                    envelope.MessageId);
            }

            TransportTelemetry.RecordError(activity, ex);
            throw;
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(
                    ex,
                    "Failed to publish message {MessageId}",
                    envelope.MessageId);
            }

            TransportTelemetry.RecordError(activity, ex);
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
        ArgumentNullException.ThrowIfNull(envelopes);
        ArgumentNullException.ThrowIfNull(destination);

        // Storage Queue doesn't support atomic batching, so parallelize sends
        var envelopeList = envelopes.ToList();

        if (envelopeList.Count == 0)
        {
            return;
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Publishing batch of {Count} messages to queue {QueueName}",
                envelopeList.Count,
                destination.Address);
        }

        var exceptions = new List<Exception>();

        // Parallel I/O-bound operations - Azure SDK handles connection pooling
        await Parallel.ForEachAsync(
            envelopeList,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount * 4,  // I/O-bound: allow more concurrency
                CancellationToken = cancellationToken
            },
            async (envelope, ct) =>
            {
                try
                {
                    await PublishAsync(envelope, destination, ct);
                }
                catch (Exception ex)
                {
                    lock (exceptions)
                    {
                        exceptions.Add(ex);
                    }
                }
            });

        if (exceptions.Count > 0)
        {
            throw new AggregateException(
                $"Failed to publish {exceptions.Count} of {envelopeList.Count} messages",
                exceptions);
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Successfully published batch of {Count} messages", envelopeList.Count);
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

        // Dispose factory - no need for lock synchronization as disposal flag
        // prevents new operations and Azure SDK QueueClient is thread-safe
        _queueClientFactory.Dispose();
    }

    /// <summary>
    /// Serializes a transport envelope to JSON.
    /// </summary>
    private static string SerializeEnvelope(ITransportEnvelope envelope)
    {
        var queueEnvelope = new StorageQueueEnvelope
        {
            MessageId = envelope.MessageId,
            CorrelationId = envelope.CorrelationId,
            CausationId = envelope.CausationId,
            Timestamp = envelope.Timestamp,
            MessageType = envelope.MessageType,
            ContentType = envelope.Headers.TryGetValue("ContentType", out var contentType) ? contentType : null,
            Headers = new Dictionary<string, string>(envelope.Headers),
            PayloadBase64 = Convert.ToBase64String(envelope.Payload.ToArray()),
            Metadata = new Dictionary<string, string>
            {
                ["EnvelopeVersion"] = "1.0",
                ["Transport"] = "StorageQueue"
            }
        };

        return JsonSerializer.Serialize(queueEnvelope, SerializerOptions);
    }

    /// <summary>
    /// Determines if an Azure Storage error is transient and potentially retryable.
    /// </summary>
    private static bool IsTransientError(RequestFailedException ex)
    {
        // HTTP 5xx errors (server errors) are typically transient
        return ex.Status >= 500 && ex.Status < 600;
    }
}
