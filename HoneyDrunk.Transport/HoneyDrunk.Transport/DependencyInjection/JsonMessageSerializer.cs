using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using HoneyDrunk.Transport.Abstractions;

namespace HoneyDrunk.Transport.DependencyInjection;

/// <summary>
/// Default JSON serializer implementation.
/// </summary>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by dependency injection")]
internal sealed class JsonMessageSerializer : IMessageSerializer
{
    private readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <inheritdoc/>
    public string ContentType => "application/json";

    /// <inheritdoc/>
    public ReadOnlyMemory<byte> Serialize<TMessage>(TMessage message)
        where TMessage : class
    {
        return JsonSerializer.SerializeToUtf8Bytes(message, _options);
    }

    /// <inheritdoc/>
    public TMessage Deserialize<TMessage>(ReadOnlyMemory<byte> payload)
        where TMessage : class
    {
        return JsonSerializer.Deserialize<TMessage>(payload.Span, _options)
            ?? throw new InvalidOperationException("Deserialization returned null");
    }

    /// <inheritdoc/>
    public object Deserialize(ReadOnlyMemory<byte> payload, Type messageType)
    {
        return JsonSerializer.Deserialize(payload.Span, messageType, _options)
            ?? throw new InvalidOperationException("Deserialization returned null");
    }
}
