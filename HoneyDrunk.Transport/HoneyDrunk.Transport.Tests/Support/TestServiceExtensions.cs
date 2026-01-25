using HoneyDrunk.Kernel.Abstractions.Context;
using HoneyDrunk.Kernel.Context;
using Microsoft.Extensions.DependencyInjection;

namespace HoneyDrunk.Transport.Tests.Support;

/// <summary>
/// Test service registration extensions.
/// </summary>
public static class TestServiceExtensions
{
    private const string TestNodeId = "test-node";
    private const string TestStudioId = "test-studio";
    private const string TestEnvironment = "test";

    /// <summary>
    /// Registers the required Kernel services for Transport tests.
    /// This mocks what Kernel.AddKernelDefaults() would provide.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTestKernelServices(this IServiceCollection services)
    {
        // Register IGridContext as scoped (Kernel vNext pattern)
        services.AddScoped<IGridContext>(_ => new GridContext(TestNodeId, TestStudioId, TestEnvironment));
        return services;
    }

    /// <summary>
    /// Registers the required Kernel services for Transport tests with custom values.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="nodeId">The node identifier.</param>
    /// <param name="studioId">The studio identifier.</param>
    /// <param name="environment">The environment.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTestKernelServices(
        this IServiceCollection services,
        string nodeId,
        string studioId,
        string environment)
    {
        services.AddScoped<IGridContext>(_ => new GridContext(nodeId, studioId, environment));
        return services;
    }
}
