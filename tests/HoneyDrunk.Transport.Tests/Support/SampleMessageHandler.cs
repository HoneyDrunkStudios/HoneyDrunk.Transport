namespace HoneyDrunk.Transport.Tests.Support;

/// <summary>
/// Test handler that records handling in the context properties.
/// </summary>
public sealed class SampleMessageHandler : HoneyDrunk.Transport.Abstractions.IMessageHandler<SampleMessage>
{
    /// <inheritdoc />
    public Task HandleAsync(SampleMessage message, HoneyDrunk.Transport.Abstractions.MessageContext context, CancellationToken cancellationToken)
    {
        context.Properties["handled"] = message.Value;
        return Task.CompletedTask;
    }
}
