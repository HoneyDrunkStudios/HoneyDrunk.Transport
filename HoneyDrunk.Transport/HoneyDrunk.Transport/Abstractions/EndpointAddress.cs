namespace HoneyDrunk.Transport.Abstractions;

/// <summary>
/// Default implementation of endpoint address.
/// </summary>
public sealed class EndpointAddress : IEndpointAddress
{
    /// <summary>
    /// Gets or initializes the logical name of the endpoint.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets or initializes the transport-specific address.
    /// </summary>
    public string Address { get; init; } = string.Empty;

    /// <summary>
    /// Gets or initializes the optional transport-specific properties.
    /// </summary>
    public IReadOnlyDictionary<string, string> Properties { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Creates an endpoint address with the specified name and address.
    /// </summary>
    /// <param name="name">The logical name.</param>
    /// <param name="address">The physical address.</param>
    /// <returns>A new endpoint address instance.</returns>
    public static IEndpointAddress Create(string name, string address)
    {
        return new EndpointAddress
        {
            Name = name,
            Address = address,
            Properties = new Dictionary<string, string>()
        };
    }

    /// <summary>
    /// Creates an endpoint address with the specified name, address, and properties.
    /// </summary>
    /// <param name="name">The logical name.</param>
    /// <param name="address">The physical address.</param>
    /// <param name="properties">The transport-specific properties.</param>
    /// <returns>A new endpoint address instance.</returns>
    public static IEndpointAddress Create(string name, string address, IDictionary<string, string> properties)
    {
        return new EndpointAddress
        {
            Name = name,
            Address = address,
            Properties = new Dictionary<string, string>(properties)
        };
    }
}
