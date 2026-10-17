using HoneyDrunk.Transport.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace HoneyDrunk.Transport.Tests.Core.Serialization;

/// <summary>
/// JSON message serializer tests using service registration.
/// </summary>
public sealed class JsonMessageSerializerTests
{
    /// <summary>
    /// Ensures a message round-trips through serialization.
    /// </summary>
    [Fact]
    public void Serialize_WithMessage_DeserializesCorrectly()
    {
        var services = new ServiceCollection();
        HoneyDrunk.Transport.DependencyInjection.ServiceCollectionExtensions.AddHoneyDrunkTransportCore(services);
        var providerFactory = new DefaultServiceProviderFactory();
        var sp = providerFactory.CreateServiceProvider(services);
        var s = sp.GetRequiredService<HoneyDrunk.Transport.Abstractions.IMessageSerializer>();

        var m = new SampleMessage { Value = "hello" };
        var payload = s.Serialize(m);
        var back = s.Deserialize<SampleMessage>(payload);
        Assert.Equal("hello", back.Value);

        var obj = s.Deserialize<SampleMessage>(payload);
        Assert.IsType<SampleMessage>(obj);
    }
}
