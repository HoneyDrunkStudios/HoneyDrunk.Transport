namespace HoneyDrunk.Transport.Tests.Core.Configuration;

/// <summary>
/// EndpointAddress factory tests.
/// </summary>
public sealed class EndpointAddressTests
{
    /// <summary>
    /// Ensures name and address are set.
    /// </summary>
    [Fact]
    public void Create_WithNameAndAddress_SetsPropertiesCorrectly()
    {
        var addr = HoneyDrunk.Transport.Abstractions.EndpointAddress.Create("orders", "queue-orders");
        Assert.Equal("orders", addr.Name);
        Assert.Equal("queue-orders", addr.Address);
        Assert.Empty(addr.Properties);
    }

    /// <summary>
    /// Ensures properties dictionary is copied.
    /// </summary>
    [Fact]
    public void Create_WithProperties_CopiesDictionaryValues()
    {
        var props = new Dictionary<string, string> { ["a"] = "1" };
        var addr = HoneyDrunk.Transport.Abstractions.EndpointAddress.Create("orders", "queue-orders", props);
        Assert.Equal("1", addr.Properties["a"]);
        props["a"] = "2";
        Assert.Equal("1", addr.Properties["a"]);
    }
}
