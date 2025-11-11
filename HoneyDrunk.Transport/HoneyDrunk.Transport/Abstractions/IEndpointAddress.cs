namespace HoneyDrunk.Transport.Abstractions;

/// <summary>
/// Represents a logical transport endpoint address.
/// </summary>
public interface IEndpointAddress
{
    /// <summary>
    /// Gets the logical name of the endpoint.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the transport-specific address information (queue name, topic name, etc.).
    /// </summary>
    string Address { get; }

    /// <summary>
    /// Gets the optional transport-specific properties (partition key, session id, etc.).
    /// </summary>
    IReadOnlyDictionary<string, string> Properties { get; }
}
