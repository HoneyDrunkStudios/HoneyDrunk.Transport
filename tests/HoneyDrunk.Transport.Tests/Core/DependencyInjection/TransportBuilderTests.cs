using HoneyDrunk.Transport.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace HoneyDrunk.Transport.Tests.Core.DependencyInjection;

/// <summary>
/// Tests for transport builder implementation.
/// </summary>
public sealed class TransportBuilderTests
{
    /// <summary>
    /// Test service interface.
    /// </summary>
    private interface ITestService
    {
    }

    /// <summary>
    /// Other test service interface.
    /// </summary>
    private interface IOtherService
    {
    }

    /// <summary>
    /// Verifies constructor assigns Services property.
    /// </summary>
    [Fact]
    public void Constructor_WithServiceCollection_AssignsServicesProperty()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var builder = new TransportBuilder(services);

        // Assert
        Assert.Same(services, builder.Services);
    }

    /// <summary>
    /// Verifies Services property returns same instance.
    /// </summary>
    [Fact]
    public void Services_WhenAccessed_ReturnsSameInstance()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = new TransportBuilder(services);

        // Act
        var services1 = builder.Services;
        var services2 = builder.Services;

        // Assert
        Assert.Same(services1, services2);
        Assert.Same(services, services1);
    }

    /// <summary>
    /// Verifies builder implements ITransportBuilder interface.
    /// </summary>
    [Fact]
    public void Builder_ImplementsITransportBuilder()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        ITransportBuilder builder = new TransportBuilder(services);

        // Assert
        Assert.NotNull(builder);
        Assert.IsType<ITransportBuilder>(builder, exactMatch: false);
    }

    /// <summary>
    /// Verifies builder can be used to add services.
    /// </summary>
    [Fact]
    public void Services_CanAddServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = new TransportBuilder(services);

        // Act
        builder.Services.AddSingleton<ITestService, TestService>();
        var provider = services.BuildServiceProvider();

        // Assert
        var service = provider.GetService<ITestService>();
        Assert.NotNull(service);
        Assert.IsType<TestService>(service);
    }

    /// <summary>
    /// Verifies multiple builders can share same service collection.
    /// </summary>
    [Fact]
    public void MultipleBuilders_WithSameServiceCollection_ShareServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder1 = new TransportBuilder(services);
        var builder2 = new TransportBuilder(services);

        // Act
        builder1.Services.AddSingleton<ITestService, TestService>();
        var provider = services.BuildServiceProvider();

        // Assert - both builders point to same collection
        Assert.Same(builder1.Services, builder2.Services);
        var service = provider.GetService<ITestService>();
        Assert.NotNull(service);
    }

    /// <summary>
    /// Verifies builder works with empty service collection.
    /// </summary>
    [Fact]
    public void Constructor_WithEmptyServiceCollection_WorksCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var builder = new TransportBuilder(services);

        // Assert
        Assert.Empty(builder.Services);
        Assert.Same(services, builder.Services);
    }

    /// <summary>
    /// Verifies builder preserves existing services.
    /// </summary>
    [Fact]
    public void Constructor_WithExistingServices_PreservesServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<ITestService, TestService>();
        services.AddTransient<IOtherService, OtherService>();

        // Act
        var builder = new TransportBuilder(services);

        // Assert
        Assert.Equal(2, builder.Services.Count);
        var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<ITestService>());
        Assert.NotNull(provider.GetService<IOtherService>());
    }

    /// <summary>
    /// Verifies class is sealed (cannot be inherited).
    /// </summary>
    [Fact]
    public void Class_IsSealed()
    {
        // Arrange
        var type = typeof(TransportBuilder);

        // Assert
        Assert.True(type.IsSealed);
    }

    /// <summary>
    /// Verifies class is internal (not publicly exposed).
    /// </summary>
    [Fact]
    public void Class_IsInternal()
    {
        // Arrange
        var type = typeof(TransportBuilder);

        // Assert
        Assert.False(type.IsPublic);
        Assert.True(type.IsNotPublic);
    }

    /// <summary>
    /// Test service implementation.
    /// </summary>
#pragma warning disable CA1812 // Avoid uninstantiated internal classes - used via DI
    private sealed class TestService : ITestService
    {
    }
#pragma warning restore CA1812

    /// <summary>
    /// Other test service implementation.
    /// </summary>
#pragma warning disable CA1812 // Avoid uninstantiated internal classes - used via DI
    private sealed class OtherService : IOtherService
    {
    }
#pragma warning restore CA1812
}
