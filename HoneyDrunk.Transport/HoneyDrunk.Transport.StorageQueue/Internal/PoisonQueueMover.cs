using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Extensions.Logging;

namespace HoneyDrunk.Transport.StorageQueue.Internal;

/// <summary>
/// Helper for moving poison messages to a dedicated poison queue.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="PoisonQueueMover"/> class.
/// </remarks>
/// <param name="logger">The logger instance.</param>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by dependency injection")]
internal sealed class PoisonQueueMover(ILogger logger)
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ILogger _logger = logger;

    /// <summary>
    /// Moves a message to the poison queue with error metadata.
    /// </summary>
    /// <param name="poisonQueueClient">The poison queue client.</param>
    /// <param name="message">The original queue message.</param>
    /// <param name="error">The exception that caused the message to be poisoned.</param>
    /// <param name="originalDequeueCount">The dequeue count from the original message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
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
            var poisonEnvelope = CreatePoisonEnvelope(message, error, originalDequeueCount);

            // Serialize to JSON
            var poisonMessageText = JsonSerializer.Serialize(poisonEnvelope, SerializerOptions);

            // Send to poison queue
            await poisonQueueClient.SendMessageAsync(
                poisonMessageText,
                cancellationToken: cancellationToken);

            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning(
                    "Moved message {MessageId} to poison queue after {DequeueCount} attempts. Error: {ErrorType}",
                    message.MessageId,
                    originalDequeueCount,
                    error?.GetType().Name ?? "Unknown");
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
    private static PoisonEnvelope CreatePoisonEnvelope(
        QueueMessage message,
        Exception? error,
        long dequeueCount)
    {
        return new PoisonEnvelope
        {
            OriginalMessageId = message.MessageId,
            OriginalMessage = message.Body.ToString(),
            DequeueCount = dequeueCount,
            FirstFailureTimestamp = message.InsertedOn ?? DateTimeOffset.UtcNow,
            LastFailureTimestamp = DateTimeOffset.UtcNow,
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
        public required string OriginalMessageId { get; init; }

        public required string OriginalMessage { get; init; }

        public long DequeueCount { get; init; }

        public DateTimeOffset FirstFailureTimestamp { get; init; }

        public DateTimeOffset LastFailureTimestamp { get; init; }

        public string? ErrorType { get; init; }

        public string? ErrorMessage { get; init; }

        public string? ErrorStackTrace { get; init; }

        public Dictionary<string, string>? Metadata { get; init; }
    }
}
