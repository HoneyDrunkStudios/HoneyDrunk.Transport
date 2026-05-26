using Azure.Storage.Queues;
using HoneyDrunk.Transport.StorageQueue.Configuration;
using HoneyDrunk.Transport.StorageQueue.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HoneyDrunk.Transport.Tests.Support;

/// <summary>
/// Test factory that returns pre-built <see cref="FakeQueueClient"/> instances instead
/// of opening real Azure connections. Wraps the production <see cref="QueueClientFactory"/>
/// so the rest of the processor pipeline behaves the same way.
/// </summary>
/// <param name="primary">The fake primary queue client.</param>
/// <param name="poison">The fake poison queue client.</param>
internal sealed class FakeQueueClientFactory(FakeQueueClient primary, FakeQueueClient poison)
    : QueueClientFactory(
        Options.Create(new StorageQueueOptions
        {
            ConnectionString = "UseDevelopmentStorage=true",
            QueueName = primary.Name,
            PoisonQueueName = poison.Name,
            CreateIfNotExists = false
        }),
        NullLogger<QueueClientFactory>.Instance)
{
    /// <summary>Gets the fake primary queue client.</summary>
    public FakeQueueClient Primary { get; } = primary;

    /// <summary>Gets the fake poison queue client.</summary>
    public FakeQueueClient Poison { get; } = poison;

    /// <inheritdoc />
    public override Task<QueueClient> GetOrCreatePrimaryQueueClientAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<QueueClient>(Primary);

    /// <inheritdoc />
    public override Task<QueueClient> GetOrCreatePoisonQueueClientAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<QueueClient>(Poison);

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        // Defer to the base which flips the disposed sentinel and clears its
        // private fields; without this CA2215 (call base.DisposeAsync) trips.
        await base.DisposeAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }
}
