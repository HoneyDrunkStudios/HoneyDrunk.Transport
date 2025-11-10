using HoneyDrunk.Transport.Abstractions;

namespace HoneyDrunk.Transport.Pipeline;

/// <summary>
/// Executes a composable message processing pipeline.
/// </summary>
public interface IMessagePipeline
{
    /// <summary>
    /// Processes an envelope through the pipeline.
    /// </summary>
    /// <param name="envelope">The message envelope to process.</param>
    /// <param name="context">The message context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of processing the message.</returns>
    Task<MessageProcessingResult> ProcessAsync(
        ITransportEnvelope envelope,
        MessageContext context,
        CancellationToken cancellationToken = default);
}
