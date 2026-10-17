using HoneyDrunk.Transport.Abstractions;

namespace HoneyDrunk.Transport.AzureServiceBus;

/// <summary>
/// Topology capabilities for Azure Service Bus transport (full-featured enterprise messaging).
/// </summary>
internal sealed class ServiceBusTopology : ITransportTopology
{
    public string Name => "AzureServiceBus";

    public bool SupportsTopics => true;

    public bool SupportsSubscriptions => true;

    public bool SupportsSessions => true;

    public bool SupportsOrdering => true;

    public bool SupportsScheduledMessages => true;

    public bool SupportsBatching => true;

    public bool SupportsTransactions => true;

    public bool SupportsDeadLetterQueue => true;

    public bool SupportsPriority => false; // Service Bus doesn't support message priority

    public long? MaxMessageSize => 256 * 1024; // 256 KB for Standard tier (1 MB for Premium)
}
