using Microsoft.Extensions.DependencyInjection;

namespace HoneyDrunk.Transport.DependencyInjection;

/// <summary>
/// Default implementation of transport builder.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="TransportBuilder"/> class.
/// </remarks>
/// <param name="services">The service collection.</param>
internal sealed class TransportBuilder(IServiceCollection services) : ITransportBuilder
{
    /// <inheritdoc/>
    public IServiceCollection Services { get; } = services;
}
