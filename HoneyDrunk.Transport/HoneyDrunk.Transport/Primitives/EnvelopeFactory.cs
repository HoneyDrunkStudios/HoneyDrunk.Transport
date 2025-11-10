using HoneyDrunk.Transport.Abstractions;

namespace HoneyDrunk.Transport.Primitives;

/// <summary>
/// Factory for creating transport envelopes from typed messages.
/// </summary>
public static class EnvelopeFactory
{
    /// <summary>
    /// Creates an envelope from a typed message and serialized payload.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <param name="payload">The serialized payload.</param>
    /// <param name="correlationId">Optional correlation identifier.</param>
    /// <param name="causationId">Optional causation identifier.</param>
    /// <param name="headers">Optional message headers.</param>
    /// <returns>A new transport envelope.</returns>
    public static ITransportEnvelope CreateEnvelope<TMessage>(
        ReadOnlyMemory<byte> payload,
        string? correlationId = null,
        string? causationId = null,
        IDictionary<string, string>? headers = null)
        where TMessage : class
    {
        return new TransportEnvelope
        {
            MessageId = Guid.NewGuid().ToString(),
            CorrelationId = correlationId,
            CausationId = causationId,
            Timestamp = DateTimeOffset.UtcNow,
            MessageType = typeof(TMessage).FullName ?? typeof(TMessage).Name,
            Headers = headers != null
                ? new Dictionary<string, string>(headers)
                : [],
            Payload = payload
        };
    }

    /// <summary>
    /// Creates an envelope with a specified message ID.
    /// </summary>
    /// <param name="messageId">The message identifier.</param>
    /// <param name="messageType">The message type name.</param>
    /// <param name="payload">The serialized payload.</param>
    /// <param name="correlationId">Optional correlation identifier.</param>
    /// <param name="causationId">Optional causation identifier.</param>
    /// <param name="headers">Optional message headers.</param>
    /// <returns>A new transport envelope.</returns>
    public static ITransportEnvelope CreateEnvelopeWithId(
        string messageId,
        string messageType,
        ReadOnlyMemory<byte> payload,
        string? correlationId = null,
        string? causationId = null,
        IDictionary<string, string>? headers = null)
    {
        return new TransportEnvelope
        {
            MessageId = messageId,
            CorrelationId = correlationId,
            CausationId = causationId,
            Timestamp = DateTimeOffset.UtcNow,
            MessageType = messageType,
            Headers = headers != null
                ? new Dictionary<string, string>(headers)
                : [],
            Payload = payload
        };
    }

    /// <summary>
    /// Creates a reply envelope derived from an original envelope.
    /// </summary>
    /// <param name="original">The original envelope to reply to.</param>
    /// <param name="replyMessageType">The reply message type name.</param>
    /// <param name="payload">The serialized reply payload.</param>
    /// <param name="additionalHeaders">Optional additional headers.</param>
    /// <returns>A new reply envelope.</returns>
    public static ITransportEnvelope CreateReply(
        ITransportEnvelope original,
        string replyMessageType,
        ReadOnlyMemory<byte> payload,
        IDictionary<string, string>? additionalHeaders = null)
    {
        var headers = new Dictionary<string, string>(original.Headers);
        if (additionalHeaders != null)
        {
            foreach (var kvp in additionalHeaders)
            {
                headers[kvp.Key] = kvp.Value;
            }
        }

        return new TransportEnvelope
        {
            MessageId = Guid.NewGuid().ToString(),
            CorrelationId = original.CorrelationId ?? original.MessageId,
            CausationId = original.MessageId,
            Timestamp = DateTimeOffset.UtcNow,
            MessageType = replyMessageType,
            Headers = headers,
            Payload = payload
        };
    }
}
