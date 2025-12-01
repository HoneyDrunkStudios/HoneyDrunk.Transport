using Azure.Storage.Blobs;
using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.AzureServiceBus.Configuration;
using System.Text;
using System.Text.Json;

namespace HoneyDrunk.Transport.AzureServiceBus.Internal;

/// <summary>
/// Default Blob Storage implementation of <see cref="IBlobFallbackStore"/>.
/// </summary>
internal sealed class DefaultBlobFallbackStore : IBlobFallbackStore
{
    public async Task<Uri> SaveAsync(
        ITransportEnvelope envelope,
        IEndpointAddress destination,
        Exception failure,
        BlobFallbackOptions options,
        CancellationToken cancellationToken)
    {
        BlobServiceClient blobServiceClient = !string.IsNullOrWhiteSpace(options.ConnectionString)
            ? new BlobServiceClient(options.ConnectionString)
            : !string.IsNullOrWhiteSpace(options.AccountUrl)
                ? new BlobServiceClient(new Uri(options.AccountUrl), new Azure.Identity.DefaultAzureCredential())
                : throw new InvalidOperationException("Blob fallback requires either ConnectionString or AccountUrl to be configured.");
        var container = blobServiceClient.GetBlobContainerClient(options.ContainerName);
        await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var prefix = string.IsNullOrWhiteSpace(options.BlobPrefix) ? "servicebus" : options.BlobPrefix!.Trim('/');
        var addressSafe = destination.Address.Replace('/', '_');
        var blobName = $"{prefix}/{addressSafe}/{now:yyyy/MM/dd/HH}/{envelope.MessageId}.json";

        var record = new BlobFallbackRecord
        {
            MessageId = envelope.MessageId,
            CorrelationId = envelope.CorrelationId,
            CausationId = envelope.CausationId,
            Timestamp = envelope.Timestamp,
            MessageType = envelope.MessageType,
            Headers = new Dictionary<string, string>(envelope.Headers),
            PayloadBase64 = Convert.ToBase64String(envelope.Payload.ToArray()),
            Destination = new BlobFallbackDestination
            {
                Address = destination.Address,
                Properties = new Dictionary<string, string>(destination.AdditionalProperties)
            },
            FailureTimestamp = now,
            FailureExceptionType = failure.GetType().FullName ?? failure.GetType().Name,
            FailureMessage = failure.Message
        };

        var json = JsonSerializer.Serialize(record);
        using var content = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var blob = container.GetBlobClient(blobName);

        await blob.UploadAsync(content, overwrite: true, cancellationToken);

        return blob.Uri;
    }
}
