using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Extensions.Logging;

namespace HoneyDrunk.Transport.StorageQueue.Internal;

/// <summary>
/// Helper for moving poison messages to a dedicated poison queue.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Duplicate Handling:</strong> Due to Azure Storage Queue's lack of transactions,
/// a message may be moved to the poison queue multiple times if deletion from the primary
/// queue fails. The OriginalMessageId can be used for deduplication when processing the
/// poison queue.
/// </para>
/// </remarks>
/// <param name="logger">The logger instance.</param>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by dependency injection")]
internal sealed class PoisonQueueMover(ILogger<PoisonQueueMover> logger)
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ILogger<PoisonQueueMover> _logger = logger;

    /// <summary>
    /// Moves a message to the poison queue with error metadata.
    /// </summary>
    /// <param name="poisonQueueClient">The poison queue client.</param>
    /// <param name="message">The original queue message.</param>
    /// <param name="error">The exception that caused the message to be poisoned, or null if explicitly dead-lettered without an exception.</param>
    /// <param name="originalDequeueCount">The dequeue count from the original message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks>
    /// The <paramref name="error"/> parameter may be null when a message is explicitly moved to the poison queue
    /// via MessageProcessingResult.DeadLetter without an exception being thrown, or when
    /// the maximum dequeue count is exceeded during a retry scenario.
    /// </remarks>
    public async Task MoveMessageToPoisonQueueAsync(
        QueueClient poisonQueueClient,
        QueueMessage message,
        Exception? error,
        long originalDequeueCount,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(poisonQueueClient);
        ArgumentNullException.ThrowIfNull(message);

        try
        {
            // Create poison envelope with error metadata
            // Note: error may be null for explicit dead-letter operations or max dequeue count scenarios
            var poisonEnvelope = CreatePoisonEnvelope(message, error, originalDequeueCount);

            // Serialize to JSON
            var poisonMessageText = JsonSerializer.Serialize(poisonEnvelope, SerializerOptions);

            // Send to poison queue
            await poisonQueueClient.SendMessageAsync(
                poisonMessageText,
                cancellationToken: cancellationToken);

            if (_logger.IsEnabled(LogLevel.Warning))
            {
                var errorDescription = error != null
                    ? $"{error.GetType().Name}: {error.Message}"
                    : "Explicit dead-letter or max dequeue count exceeded";

                _logger.LogWarning(
                    "Moved message {MessageId} to poison queue after {DequeueCount} attempts. Reason: {Reason}",
                    message.MessageId,
                    originalDequeueCount,
                    errorDescription);
            }
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(
                    ex,
                    "Failed to move message {MessageId} to poison queue",
                    message.MessageId);
            }

            throw;
        }
    }

    /// <summary>
    /// Creates a poison envelope with error metadata.
    /// </summary>
    /// <param name="message">The original queue message.</param>
    /// <param name="error">The exception that caused poisoning, or null if no exception occurred.</param>
    /// <param name="dequeueCount">The number of times the message was dequeued.</param>
    /// <returns>A poison envelope containing the original message and failure metadata.</returns>
    /// <remarks>
    /// <para>
    /// When <paramref name="error"/> is null, the ErrorType, ErrorMessage, and ErrorStackTrace
    /// fields will be null in the resulting envelope. This is expected for scenarios where a message
    /// is dead-lettered without an exception (e.g., explicit dead-letter, max dequeue count exceeded
    /// on successful processing that returns Retry).
    /// </para>
    /// <para>
    /// <strong>Timestamp Semantics:</strong> Both FirstFailureTimestamp and LastFailureTimestamp are set
    /// to the current UTC time when the message is moved to the poison queue. Azure Storage Queue does not
    /// track when processing first failed, only when the message was originally enqueued (InsertedOn).
    /// Therefore, the "first failure" timestamp represents when we made the decision to poison the message,
    /// not the absolute first processing attempt.
    /// </para>
    /// </remarks>
    private static PoisonEnvelope CreatePoisonEnvelope(
        QueueMessage message,
        Exception? error,
        long dequeueCount)
    {
        var now = DateTimeOffset.UtcNow;

        return new PoisonEnvelope
        {
            OriginalMessageId = message.MessageId,
            OriginalMessage = message.Body.ToString(),
            DequeueCount = dequeueCount,
            FirstFailureTimestamp = now,
            LastFailureTimestamp = now,
            ErrorType = error?.GetType().FullName,
            ErrorMessage = error?.Message,
            ErrorStackTrace = error?.StackTrace,
            Metadata = new Dictionary<string, string>
            {
                ["PopReceipt"] = message.PopReceipt ?? string.Empty,
                ["InsertedOn"] = message.InsertedOn?.ToString("o") ?? string.Empty,
                ["ExpiresOn"] = message.ExpiresOn?.ToString("o") ?? string.Empty,
                ["NextVisibleOn"] = message.NextVisibleOn?.ToString("o") ?? string.Empty
            }
        };
    }

    /// <summary>
    /// Represents a message that has been moved to the poison queue.
    /// </summary>
    private sealed class PoisonEnvelope
    {
        [JsonPropertyName("originalMessageId")]
        public required string OriginalMessageId { get; init; }

        [JsonPropertyName("originalMessage")]
        public required string OriginalMessage { get; init; }

        [JsonPropertyName("dequeueCount")]
        public long DequeueCount { get; init; }

        [JsonPropertyName("firstFailureTimestamp")]
        public DateTimeOffset FirstFailureTimestamp { get; init; }

        [JsonPropertyName("lastFailureTimestamp")]
        public DateTimeOffset LastFailureTimestamp { get; init; }

        [JsonPropertyName("errorType")]
        public string? ErrorType { get; init; }

        [JsonPropertyName("errorMessage")]
        public string? ErrorMessage { get; init; }

        [JsonPropertyName("errorStackTrace")]
        public string? ErrorStackTrace { get; init; }

        [JsonPropertyName("metadata")]
        public Dictionary<string, string>? Metadata { get; init; }
    }
}
