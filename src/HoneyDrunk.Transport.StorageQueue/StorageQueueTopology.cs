using HoneyDrunk.Transport.Abstractions;

namespace HoneyDrunk.Transport.StorageQueue;

/// <summary>
/// Topology capabilities for Azure Storage Queue transport (cost-effective, high-volume queuing).
/// </summary>
internal sealed class StorageQueueTopology : ITransportTopology
{
    public string Name => "StorageQueue";

    public bool SupportsTopics => false; // Storage Queue is queue-only

    public bool SupportsSubscriptions => false;

    public bool SupportsSessions => false;

    public bool SupportsOrdering => false; // Storage Queue doesn't guarantee ordering

    public bool SupportsScheduledMessages => false; // Can simulate with visibility timeout

    public bool SupportsBatching => true;

    public bool SupportsTransactions => false;

    public bool SupportsDeadLetterQueue => false; // Manual implementation via poison queue

    public bool SupportsPriority => false;

    public long? MaxMessageSize => 64 * 1024; // 64 KB limit
}
