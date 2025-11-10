using HoneyDrunk.Transport.Abstractions;

namespace HoneyDrunk.Transport.Configuration;

/// <summary>
/// Configuration for the transport core system.
/// </summary>
public sealed class TransportCoreOptions
{
    /// <summary>
    /// Gets or sets the default serializer content type.
    /// </summary>
    public string DefaultSerializerType { get; set; } = "application/json";

    /// <summary>
    /// Gets or sets a value indicating whether to enable built-in telemetry.
    /// </summary>
    public bool EnableTelemetry { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to enable built-in logging middleware.
    /// </summary>
    public bool EnableLogging { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to enable correlation middleware.
    /// </summary>
    public bool EnableCorrelation { get; set; } = true;

    /// <summary>
    /// Gets or sets the global retry options (can be overridden per endpoint).
    /// </summary>
    public RetryOptions DefaultRetry { get; set; } = new();
}
