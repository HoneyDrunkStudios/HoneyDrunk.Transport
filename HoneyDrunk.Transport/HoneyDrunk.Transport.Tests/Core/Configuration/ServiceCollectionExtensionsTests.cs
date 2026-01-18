using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.DependencyInjection;
using HoneyDrunk.Transport.Metrics;
using HoneyDrunk.Transport.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace HoneyDrunk.Transport.Tests.Core.Configuration;

/// <summary>
/// Tests for service registration extensions.
/// </summary>
public sealed class ServiceCollectionExtensionsTests
{
    /// <summary>
    /// Verifies AddHoneyDrunkTransportCore registers core services.
    /// </summary>
    [Fact]
    public void AddHoneyDrunkTransportCore_WhenCalled_RegistersCoreServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHoneyDrunkTransportCore();

        var provider = new DefaultServiceProviderFactory().CreateServiceProvider(services);

        Assert.NotNull(provider.GetService<IMessageSerializer>());
        Assert.NotNull(provider.GetService<HoneyDrunk.Transport.Pipeline.IMessagePipeline>());
    }

    /// <summary>
    /// Verifies AddMessageHandler registers handler.
    /// </summary>
    [Fact]
    public void AddMessageHandler_WithHandlerType_RegistersHandlerSuccessfully()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHoneyDrunkTransportCore();
        services.AddMessageHandler<SampleMessage, SampleMessageHandler>();

        var provider = new DefaultServiceProviderFactory().CreateServiceProvider(services);

        var handler = provider.GetService<IMessageHandler<SampleMessage>>();
        Assert.NotNull(handler);
        Assert.IsType<SampleMessageHandler>(handler);
    }

    /// <summary>
    /// Verifies AddMessageHandler with delegate registers handler.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task AddMessageHandler_WithDelegate_RegistersHandlerSuccessfully()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHoneyDrunkTransportCore();

        var called = false;
        services.AddMessageHandler<SampleMessage>((msg, ctx, ct) =>
        {
            called = true;
            return Task.CompletedTask;
        });

        var provider = new DefaultServiceProviderFactory().CreateServiceProvider(services);

        var handler = provider.GetService<IMessageHandler<SampleMessage>>();
        Assert.NotNull(handler);

        var envelope = TestData.CreateEnvelope(new SampleMessage { Value = "test" });
        var context = new MessageContext
        {
            Envelope = envelope,
            Transaction = NoOpTransportTransaction.Instance,
            DeliveryCount = 1
        };

        await handler!.HandleAsync(new SampleMessage { Value = "test" }, context, CancellationToken.None);
        Assert.True(called);
    }

    /// <summary>
    /// Verifies AddHoneyDrunkTransportCore registers ITransportMetrics.
    /// </summary>
    [Fact]
    public void AddHoneyDrunkTransportCore_WhenCalled_RegistersITransportMetrics()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHoneyDrunkTransportCore();

        // Act
        var provider = new DefaultServiceProviderFactory().CreateServiceProvider(services);
        var metrics = provider.GetService<ITransportMetrics>();

        // Assert
        Assert.NotNull(metrics);
        Assert.Same(NoOpTransportMetrics.Instance, metrics);
    }

    /// <summary>
    /// Verifies consumer can override ITransportMetrics before calling AddHoneyDrunkTransportCore.
    /// </summary>
    [Fact]
    public void AddHoneyDrunkTransportCore_WhenCustomMetricsRegisteredFirst_UsesCustomMetrics()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var customMetrics = new CustomTransportMetrics();
        services.AddSingleton<ITransportMetrics>(customMetrics);

        services.AddHoneyDrunkTransportCore();

        // Act
        var provider = new DefaultServiceProviderFactory().CreateServiceProvider(services);
        var metrics = provider.GetService<ITransportMetrics>();

        // Assert
        Assert.NotNull(metrics);
        Assert.Same(customMetrics, metrics);
    }

    /// <summary>
    /// Custom implementation for testing consumer override.
    /// </summary>
    private sealed class CustomTransportMetrics : ITransportMetrics
    {
        public void RecordMessagePublished(string messageType, string destination)
        {
        }

        public void RecordMessageConsumed(string messageType, string source)
        {
        }

        public void RecordProcessingDuration(string messageType, TimeSpan duration, string result)
        {
        }

        public void RecordMessageRetry(string messageType, int attemptNumber)
        {
        }

        public void RecordMessageDeadLettered(string messageType, string reason)
        {
        }

        public void RecordPayloadSize(string messageType, long sizeBytes, string direction)
        {
        }
    }
}
