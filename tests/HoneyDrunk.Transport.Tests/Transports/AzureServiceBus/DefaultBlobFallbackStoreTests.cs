using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.AzureServiceBus.Configuration;
using HoneyDrunk.Transport.AzureServiceBus.Internal;
using HoneyDrunk.Transport.Tests.Support;

namespace HoneyDrunk.Transport.Tests.Transports.AzureServiceBus;

/// <summary>
/// Tests for DefaultBlobFallbackStore configuration validation.
/// </summary>
public sealed class DefaultBlobFallbackStoreTests
{
    /// <summary>
    /// SaveAsync should throw when both ConnectionString and AccountUrl are missing.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task SaveAsync_MissingCredentials_Throws()
    {
        var store = new DefaultBlobFallbackStore();
        var envelope = TestData.CreateEnvelope(new SampleMessage { Value = "x" });
        var destination = EndpointAddress.Create("q", "q");

        var options = new BlobFallbackOptions
        {
            Enabled = true,
            ConnectionString = null,
            AccountUrl = null,
            ContainerName = "transport-fallback"
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.SaveAsync(envelope, destination, new InvalidOperationException("publish"), options, CancellationToken.None));
    }
}
