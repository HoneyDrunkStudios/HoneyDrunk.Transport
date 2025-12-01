using HoneyDrunk.Transport.Abstractions;

namespace HoneyDrunk.Transport.InMemory;

/// <summary>
/// Topology capabilities for the in-memory transport (testing scenarios).
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated via DI")]
internal sealed class InMemoryTopology : ITransportTopology
{
    public string Name => "InMemory";

    public bool SupportsTopics => true;

    public bool SupportsSubscriptions => true;

    public bool SupportsSessions => false;

    public bool SupportsOrdering => true; // Single-threaded broker guarantees ordering

    public bool SupportsScheduledMessages => false;

    public bool SupportsBatching => true;

    public bool SupportsTransactions => false;

    public bool SupportsDeadLetterQueue => false;

    public bool SupportsPriority => false;

    public long? MaxMessageSize => null; // No limit for in-memory
}
