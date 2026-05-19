using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.Health;
using HoneyDrunk.Transport.Runtime;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace HoneyDrunk.Transport.Tests.Core.Runtime;

/// <summary>
/// Tests for <see cref="TransportRuntimeHost"/> lifecycle orchestration.
/// </summary>
public sealed class TransportRuntimeHostTests
{
    /// <summary>
    /// Starting the host starts all consumers and marks runtime running.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task StartAsync_WhenStopped_StartsConsumersAndMarksRunning()
    {
        var first = Substitute.For<ITransportConsumer>();
        var second = Substitute.For<ITransportConsumer>();
        using var host = Create([first, second]);

        await host.StartAsync();

        Assert.True(host.IsRunning);
        await first.Received(1).StartAsync(Arg.Any<CancellationToken>());
        await second.Received(1).StartAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Starting an already running host is idempotent.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task StartAsync_WhenAlreadyRunning_DoesNotStartConsumersAgain()
    {
        var consumer = Substitute.For<ITransportConsumer>();
        using var host = Create([consumer]);

        await host.StartAsync();
        await host.StartAsync();

        await consumer.Received(1).StartAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Stop is a no-op when the runtime has not started.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task StopAsync_WhenNotRunning_DoesNotStopConsumers()
    {
        var consumer = Substitute.For<ITransportConsumer>();
        using var host = Create([consumer]);

        await host.StopAsync();

        Assert.False(host.IsRunning);
        await consumer.DidNotReceive().StopAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Stopping the host stops all consumers and marks runtime stopped.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task StopAsync_WhenRunning_StopsConsumersAndMarksStopped()
    {
        var consumer = Substitute.For<ITransportConsumer>();
        using var host = Create([consumer]);

        await host.StartAsync();
        await host.StopAsync();

        Assert.False(host.IsRunning);
        await consumer.Received(1).StopAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Stop continues when a consumer throws a non-fatal exception.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task StopAsync_WhenConsumerStopThrows_ContinuesStoppingOtherConsumers()
    {
        var failing = Substitute.For<ITransportConsumer>();
        failing.StopAsync(Arg.Any<CancellationToken>()).Returns<Task>(_ => throw new InvalidOperationException("stop failed"));
        var second = Substitute.For<ITransportConsumer>();
        using var host = Create([failing, second]);

        await host.StartAsync();
        await host.StopAsync();

        Assert.False(host.IsRunning);
        await second.Received(1).StopAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Start propagates non-fatal consumer start failures and does not mark running.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task StartAsync_WhenConsumerStartThrows_PropagatesAndRemainsStopped()
    {
        var consumer = Substitute.For<ITransportConsumer>();
        consumer.StartAsync(Arg.Any<CancellationToken>()).Returns<Task>(_ => throw new InvalidOperationException("start failed"));
        using var host = Create([consumer]);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => host.StartAsync());

        Assert.Equal("start failed", ex.Message);
        Assert.False(host.IsRunning);
    }

    /// <summary>
    /// Health contributors are returned as registered.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task GetHealthContributorsAsync_ReturnsRegisteredContributors()
    {
        var contributor = Substitute.For<ITransportHealthContributor>();
        using var host = new TransportRuntimeHost(
            [],
            [contributor],
            NullLogger<TransportRuntimeHost>.Instance);

        var contributors = await host.GetHealthContributorsAsync();

        Assert.Same(contributor, Assert.Single(contributors));
    }

    /// <summary>
    /// Explicit hosted-service start and stop delegate to runtime lifecycle methods.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task IHostedServiceLifecycle_DelegatesToRuntimeLifecycle()
    {
        var consumer = Substitute.For<ITransportConsumer>();
        using var runtimeHost = Create([consumer]);
        IHostedService host = runtimeHost;

        await host.StartAsync(CancellationToken.None);
        await host.StopAsync(CancellationToken.None);

        await consumer.Received(1).StartAsync(Arg.Any<CancellationToken>());
        await consumer.Received(1).StopAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Creates a runtime host for tests.
    /// </summary>
    /// <param name="consumers">The consumers to register.</param>
    /// <returns>A runtime host.</returns>
    private static TransportRuntimeHost Create(IEnumerable<ITransportConsumer> consumers)
    {
        return new TransportRuntimeHost(
            consumers,
            [],
            NullLogger<TransportRuntimeHost>.Instance);
    }
}
