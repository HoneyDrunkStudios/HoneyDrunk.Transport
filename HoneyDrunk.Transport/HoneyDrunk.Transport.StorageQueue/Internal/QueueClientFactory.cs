using Azure.Storage.Queues;
using HoneyDrunk.Transport.StorageQueue.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;

namespace HoneyDrunk.Transport.StorageQueue.Internal;

/// <summary>
/// Factory for creating and managing queue clients.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="QueueClientFactory"/> class.
/// </remarks>
/// <param name="options">The storage queue configuration options.</param>
/// <param name="logger">The logger instance.</param>
/// <exception cref="InvalidOperationException">Thrown when neither ConnectionString nor AccountEndpoint is configured.</exception>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by dependency injection")]
internal sealed class QueueClientFactory(
    IOptions<StorageQueueOptions> options,
    ILogger<QueueClientFactory> logger) : IAsyncDisposable
{
    private readonly StorageQueueOptions _options = ValidateOptions(options.Value);
    private readonly ILogger<QueueClientFactory> _logger = logger;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private QueueClient? _primaryQueueClient;
    private QueueClient? _poisonQueueClient;
    private bool _disposed;

    /// <summary>
    /// Gets or creates the primary queue client.
    /// </summary>
    public async Task<QueueClient> GetOrCreatePrimaryQueueClientAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_primaryQueueClient != null)
        {
            return _primaryQueueClient;
        }

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_primaryQueueClient != null)
            {
                return _primaryQueueClient;
            }

            _primaryQueueClient = CreateQueueClient(_options.QueueName);

            if (_options.CreateIfNotExists)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Creating queue {QueueName} if not exists", _options.QueueName);
                }

                await _primaryQueueClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Queue {QueueName} ready", _options.QueueName);
                }
            }

            return _primaryQueueClient;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// Gets or creates the poison queue client.
    /// </summary>
    public async Task<QueueClient> GetOrCreatePoisonQueueClientAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_poisonQueueClient != null)
        {
            return _poisonQueueClient;
        }

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_poisonQueueClient != null)
            {
                return _poisonQueueClient;
            }

            var poisonQueueName = _options.GetPoisonQueueName();
            _poisonQueueClient = CreateQueueClient(poisonQueueName);

            if (_options.CreateIfNotExists)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Creating poison queue {QueueName} if not exists", poisonQueueName);
                }

                await _poisonQueueClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Poison queue {QueueName} ready", poisonQueueName);
                }
            }

            return _poisonQueueClient;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// Disposes resources used by the factory.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        // Thread-safe disposal check using Interlocked.Exchange
        // Atomically sets _disposed to true and returns the previous value
        // If previous value was true, we've already disposed
        if (Interlocked.Exchange(ref _disposed, true))
        {
            return;
        }

        // Acquire the initialization lock to ensure no concurrent queue client creation
        await _initLock.WaitAsync();
        try
        {
            // QueueClient instances are not owned by this factory and should not be disposed here
            // The Azure SDK QueueClient is designed to be reused and doesn't require explicit disposal
            // Setting to null allows garbage collection
            _primaryQueueClient = null;
            _poisonQueueClient = null;
        }
        finally
        {
            _initLock.Release();
            _initLock.Dispose();
        }
    }

    private static StorageQueueOptions ValidateOptions(StorageQueueOptions options)
    {
        if (string.IsNullOrEmpty(options.ConnectionString) && options.AccountEndpoint == null)
        {
            throw new InvalidOperationException(
                "Either ConnectionString or AccountEndpoint must be configured");
        }

        return options;
    }

    /// <summary>
    /// Creates a queue client for the specified queue name.
    /// </summary>
    private QueueClient CreateQueueClient(string queueName)
    {
        if (!string.IsNullOrEmpty(_options.ConnectionString))
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Creating queue client for {QueueName} using connection string", queueName);
            }

            return new QueueClient(_options.ConnectionString, queueName);
        }
        else if (_options.AccountEndpoint != null)
        {
            throw new NotImplementedException(
                "TokenCredential authentication is not yet implemented. Please use ConnectionString for now.");
        }
        else
        {
            throw new InvalidOperationException(
                "Either ConnectionString or AccountEndpoint must be configured");
        }
    }
}
