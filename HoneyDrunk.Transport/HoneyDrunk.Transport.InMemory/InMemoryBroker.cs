using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Threading.Channels;
using HoneyDrunk.Transport.Abstractions;
using Microsoft.Extensions.Logging;

namespace HoneyDrunk.Transport.InMemory;

/// <summary>
/// In-memory message broker for local development and testing.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="InMemoryBroker"/> class.
/// </remarks>
/// <param name="logger">The logger instance.</param>
public sealed class InMemoryBroker(ILogger<InMemoryBroker> logger)
{
    private readonly ConcurrentDictionary<string, Channel<ITransportEnvelope>> _queues = new();
    private readonly ConcurrentDictionary<string, ImmutableList<Func<ITransportEnvelope, CancellationToken, Task>>> _subscribers = new();
    private readonly ILogger<InMemoryBroker> _logger = logger;

    /// <summary>
    /// Publishes a message to a queue.
    /// </summary>
    /// <param name="address">The destination address.</param>
    /// <param name="envelope">The message envelope to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task PublishAsync(
        string address,
        ITransportEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        var queue = GetOrCreateQueue(address);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Publishing message {MessageId} to address {Address}",
                envelope.MessageId,
                address);
        }

        await queue.Writer.WriteAsync(envelope, cancellationToken);

        // Also notify any direct subscribers
        if (_subscribers.TryGetValue(address, out var handlers))
        {
            // ImmutableList is thread-safe for enumeration
            foreach (var handler in handlers)
            {
                _ = Task.Run(
                    async () =>
                    {
                        try
                        {
                            await handler(envelope, cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            if (_logger.IsEnabled(LogLevel.Error))
                            {
                                _logger.LogError(
                                    ex,
                                    "Error in subscriber for address {Address}",
                                    address);
                            }
                        }
                    },
                    cancellationToken);
            }
        }
    }

    /// <summary>
    /// Subscribes to messages from an address.
    /// </summary>
    /// <param name="address">The address to subscribe to.</param>
    /// <param name="handler">The message handler function.</param>
    public void Subscribe(
        string address,
        Func<ITransportEnvelope, CancellationToken, Task> handler)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Subscribing to address {Address}", address);
        }

        // Use AddOrUpdate with ImmutableList for thread-safe modification
        _subscribers.AddOrUpdate(
            address,
            _ => [handler],
            (_, existingList) => existingList.Add(handler));
    }

    /// <summary>
    /// Starts consuming messages from an address.
    /// </summary>
    /// <param name="address">The address to consume from.</param>
    /// <param name="handler">The message handler function.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ConsumeAsync(
        string address,
        Func<ITransportEnvelope, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default)
    {
        var queue = GetOrCreateQueue(address);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Started consuming from address {Address}", address);
        }

        await foreach (var envelope in queue.Reader.ReadAllAsync(cancellationToken))
        {
            try
            {
                await handler(envelope, cancellationToken);
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(
                        ex,
                        "Error processing message {MessageId} from address {Address}",
                        envelope.MessageId,
                        address);
                }
            }
        }
    }

    /// <summary>
    /// Gets the current message count for an address.
    /// </summary>
    /// <param name="address">The address to check.</param>
    /// <returns>The number of pending messages.</returns>
    public int GetMessageCount(string address)
    {
        if (_queues.TryGetValue(address, out var queue))
        {
            return queue.Reader.Count;
        }

        return 0;
    }

    /// <summary>
    /// Clears all messages from an address by draining the channel without completing it.
    /// </summary>
    /// <param name="address">The address to clear.</param>
    /// <remarks>
    /// This method drains all pending messages from the queue without completing the channel,
    /// ensuring that active consumers continue to receive new messages published after the clear.
    /// </remarks>
    public void ClearQueue(string address)
    {
        if (_queues.TryGetValue(address, out var queue))
        {
            // Drain all pending messages without completing the channel
            var drainedCount = 0;
            while (queue.Reader.TryRead(out _))
            {
                drainedCount++;
            }

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Cleared {Count} message(s) from queue for address {Address}",
                    drainedCount,
                    address);
            }
        }
    }

    /// <summary>
    /// Gets or creates a queue for the specified address.
    /// </summary>
    /// <param name="address">The address to get or create a queue for.</param>
    /// <returns>The channel representing the queue for the address.</returns>
    private Channel<ITransportEnvelope> GetOrCreateQueue(string address)
    {
        return _queues.GetOrAdd(address, _ =>
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Creating queue for address {Address}", address);
            }

            return Channel.CreateUnbounded<ITransportEnvelope>(new UnboundedChannelOptions
            {
                SingleReader = false,
                SingleWriter = false
            });
        });
    }
}
