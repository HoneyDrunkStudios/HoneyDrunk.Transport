using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.Health;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HoneyDrunk.Transport.Runtime;

/// <summary>
/// Default implementation of transport runtime that orchestrates consumer lifecycle.
/// Registered as a hosted service to integrate with ASP.NET Core hosting model.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="TransportRuntimeHost"/> class.
/// </remarks>
/// <param name="consumers">All registered transport consumers.</param>
/// <param name="healthContributors">All registered health contributors.</param>
/// <param name="logger">Logger instance.</param>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1873:Use cached 'SearchValues' instance", Justification = "LoggerMessage source generator creates optimized logging code that only evaluates arguments when logging is enabled.")]
public sealed partial class TransportRuntimeHost(
    IEnumerable<ITransportConsumer> consumers,
    IEnumerable<ITransportHealthContributor> healthContributors,
    ILogger<TransportRuntimeHost> logger) : IHostedService, ITransportRuntime, IDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <inheritdoc/>
    public bool IsRunning { get; private set; }

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (IsRunning)
            {
                LogTransportAlreadyRunning(logger);
                return;
            }

            LogStartingTransportRuntime(logger, consumers.Count());

            var startTasks = consumers.Select(consumer =>
                StartConsumerAsync(consumer, cancellationToken));

            await Task.WhenAll(startTasks);

            IsRunning = true;
            LogTransportRuntimeStarted(logger);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!IsRunning)
            {
                LogTransportNotRunning(logger);
                return;
            }

            LogStoppingTransportRuntime(logger, consumers.Count());

            var stopTasks = consumers.Select(consumer =>
                StopConsumerAsync(consumer, cancellationToken));

            await Task.WhenAll(stopTasks);

            IsRunning = false;
            LogTransportRuntimeStopped(logger);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public Task<IEnumerable<ITransportHealthContributor>> GetHealthContributorsAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(healthContributors);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _lock?.Dispose();
    }

    /// <inheritdoc/>
    Task IHostedService.StartAsync(CancellationToken cancellationToken)
    {
        return StartAsync(cancellationToken);
    }

    /// <inheritdoc/>
    Task IHostedService.StopAsync(CancellationToken cancellationToken)
    {
        return StopAsync(cancellationToken);
    }

    // LoggerMessage source generator methods for high-performance logging
    [LoggerMessage(Level = LogLevel.Warning, Message = "Transport runtime is already running")]
    private static partial void LogTransportAlreadyRunning(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting transport runtime with {ConsumerCount} consumers")]
    private static partial void LogStartingTransportRuntime(ILogger logger, int consumerCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Transport runtime started successfully")]
    private static partial void LogTransportRuntimeStarted(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Transport runtime is not running")]
    private static partial void LogTransportNotRunning(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Stopping transport runtime with {ConsumerCount} consumers")]
    private static partial void LogStoppingTransportRuntime(ILogger logger, int consumerCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Transport runtime stopped successfully")]
    private static partial void LogTransportRuntimeStopped(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Starting consumer {ConsumerType}")]
    private static partial void LogStartingConsumer(ILogger logger, string consumerType);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Consumer {ConsumerType} started successfully")]
    private static partial void LogConsumerStarted(ILogger logger, string consumerType);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to start consumer {ConsumerType}")]
    private static partial void LogConsumerStartFailed(ILogger logger, string consumerType, Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Stopping consumer {ConsumerType}")]
    private static partial void LogStoppingConsumer(ILogger logger, string consumerType);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Consumer {ConsumerType} stopped successfully")]
    private static partial void LogConsumerStopped(ILogger logger, string consumerType);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error stopping consumer {ConsumerType}")]
    private static partial void LogConsumerStopError(ILogger logger, string consumerType, Exception ex);

    private async Task StartConsumerAsync(ITransportConsumer consumer, CancellationToken cancellationToken)
    {
        try
        {
            LogStartingConsumer(logger, consumer.GetType().Name);
            await consumer.StartAsync(cancellationToken);
            LogConsumerStarted(logger, consumer.GetType().Name);
        }
        catch (Exception ex)
        {
            LogConsumerStartFailed(logger, consumer.GetType().Name, ex);
            throw;
        }
    }

    private async Task StopConsumerAsync(ITransportConsumer consumer, CancellationToken cancellationToken)
    {
        try
        {
            LogStoppingConsumer(logger, consumer.GetType().Name);
            await consumer.StopAsync(cancellationToken);
            LogConsumerStopped(logger, consumer.GetType().Name);
        }
        catch (Exception ex)
        {
            LogConsumerStopError(logger, consumer.GetType().Name, ex);

            // Don't rethrow - allow other consumers to stop
        }
    }
}
