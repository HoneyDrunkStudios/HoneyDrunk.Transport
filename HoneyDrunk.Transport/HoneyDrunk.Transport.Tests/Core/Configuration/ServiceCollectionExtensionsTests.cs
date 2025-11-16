using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.DependencyInjection;
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
}
