using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.Primitives;

namespace HoneyDrunk.Transport.Tests.Support;

/// <summary>
/// Provides helpers for building test envelopes and addresses.
/// </summary>
internal static class TestData
{
    public static ITransportEnvelope CreateEnvelope<T>(T message, string? messageId = null)
        where T : class
    {
        var serializer = new HoneyDrunk.Transport.DependencyInjection.JsonMessageSerializer();
        var payload = serializer.Serialize(message);

        return new TransportEnvelope
        {
            MessageId = messageId ?? Guid.NewGuid().ToString("N"),
            MessageType = typeof(T).AssemblyQualifiedName ?? typeof(T).FullName!,
            Headers = new Dictionary<string, string>(),
            Payload = payload.ToArray(),
            CorrelationId = null,
            CausationId = null,
            Timestamp = DateTimeOffset.UtcNow
        };
    }

    public static IEndpointAddress Address(string name = "default", string address = "queue")
    {
        return new EndpointAddress
        {
            Name = name,
            Address = address,
            Properties = new Dictionary<string, string>()
        };
    }
}
