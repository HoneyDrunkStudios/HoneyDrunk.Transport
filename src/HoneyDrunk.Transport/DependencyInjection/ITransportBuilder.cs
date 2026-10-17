using Microsoft.Extensions.DependencyInjection;

namespace HoneyDrunk.Transport.DependencyInjection;

/// <summary>
/// Builder for configuring transport services.
/// </summary>
public interface ITransportBuilder
{
    /// <summary>
    /// Gets the service collection.
    /// </summary>
    IServiceCollection Services { get; }
}
