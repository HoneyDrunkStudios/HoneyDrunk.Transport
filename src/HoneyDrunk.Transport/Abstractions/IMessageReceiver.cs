namespace HoneyDrunk.Transport.Abstractions;

/// <summary>
/// Receives and processes individual messages.
/// </summary>
public interface IMessageReceiver
{
    /// <summary>
    /// Processes a received message envelope.
    /// </summary>
    /// <param name="envelope">The message envelope to process.</param>
    /// <param name="transaction">The transaction context for this message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of processing the message.</returns>
    Task<MessageProcessingResult> ReceiveAsync(
        ITransportEnvelope envelope,
        ITransportTransaction transaction,
        CancellationToken cancellationToken = default);
}
