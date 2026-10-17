using HoneyDrunk.Kernel.Abstractions.Context;

namespace HoneyDrunk.Transport.Abstractions;

/// <summary>
/// High-level typed message publisher for application-level messaging.
/// Provides a simplified API for publishing domain events and commands
/// without coupling to transport-specific envelope structures.
/// </summary>
/// <remarks>
/// This is the primary abstraction for Data and other layers to publish messages.
/// It accepts raw payloads and IGridContext, handling envelope creation internally.
/// For low-level transport operations, use <see cref="ITransportPublisher"/> directly.
/// </remarks>
public interface IMessagePublisher
{
    /// <summary>
    /// Publishes a typed message to the specified destination.
    /// </summary>
    /// <typeparam name="T">The message type.</typeparam>
    /// <param name="destination">The destination address (queue/topic name).</param>
    /// <param name="message">The message payload to publish.</param>
    /// <param name="gridContext">The Grid context for correlation and distributed tracing.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous publish operation.</returns>
    /// <remarks>
    /// The message will be serialized, wrapped in an envelope with Grid context metadata,
    /// and published to the underlying transport. Correlation, causation, node, and tenant
    /// information from the Grid context will be propagated automatically.
    /// </remarks>
    Task PublishAsync<T>(
        string destination,
        T message,
        IGridContext gridContext,
        CancellationToken cancellationToken = default)
        where T : class;

    /// <summary>
    /// Publishes multiple typed messages as a batch to the specified destination.
    /// </summary>
    /// <typeparam name="T">The message type.</typeparam>
    /// <param name="destination">The destination address (queue/topic name).</param>
    /// <param name="messages">The collection of messages to publish.</param>
    /// <param name="gridContext">The Grid context for correlation and distributed tracing.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous batch publish operation.</returns>
    /// <remarks>
    /// Each message will share the same Grid context metadata but will have unique message IDs.
    /// Batch publishing may be more efficient for high-throughput scenarios but is not atomic
    /// across all transports. Check transport-specific documentation for batch semantics.
    /// </remarks>
    Task PublishBatchAsync<T>(
        string destination,
        IEnumerable<T> messages,
        IGridContext gridContext,
        CancellationToken cancellationToken = default)
        where T : class;
}
