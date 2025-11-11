namespace HoneyDrunk.Transport.Abstractions;

/// <summary>
/// Serializes and deserializes message payloads.
/// </summary>
public interface IMessageSerializer
{
    /// <summary>
    /// Gets the content type identifier for this serializer (e.g., "application/json").
    /// </summary>
    string ContentType { get; }

    /// <summary>
    /// Serializes a message to bytes.
    /// </summary>
    /// <typeparam name="TMessage">The message type to serialize.</typeparam>
    /// <param name="message">The message instance.</param>
    /// <returns>The serialized message as a byte array.</returns>
    ReadOnlyMemory<byte> Serialize<TMessage>(TMessage message)
        where TMessage : class;

    /// <summary>
    /// Deserializes bytes to a strongly-typed message.
    /// </summary>
    /// <typeparam name="TMessage">The message type to deserialize to.</typeparam>
    /// <param name="payload">The serialized payload.</param>
    /// <returns>The deserialized message instance.</returns>
    TMessage Deserialize<TMessage>(ReadOnlyMemory<byte> payload)
        where TMessage : class;

    /// <summary>
    /// Deserializes bytes to a message of the specified type.
    /// </summary>
    /// <param name="payload">The serialized payload.</param>
    /// <param name="messageType">The target message type.</param>
    /// <returns>The deserialized message instance.</returns>
    object Deserialize(ReadOnlyMemory<byte> payload, Type messageType);
}
