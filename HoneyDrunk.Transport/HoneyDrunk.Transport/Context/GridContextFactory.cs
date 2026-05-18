using HoneyDrunk.Kernel.Abstractions.Context;
using HoneyDrunk.Kernel.Abstractions.Identity;
using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.Exceptions;
using Microsoft.Extensions.Logging;

namespace HoneyDrunk.Transport.Context;

/// <summary>
/// Factory for creating Grid context snapshots from transport envelope metadata.
/// </summary>
/// <remarks>
/// Creates initialized, abstractions-only <see cref="IGridContext"/> snapshots so Transport can
/// propagate Grid context without depending on the concrete Kernel runtime package.
/// </remarks>
public sealed class GridContextFactory : IGridContextFactory
{
    private readonly ILogger<GridContextFactory>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GridContextFactory"/> class.
    /// </summary>
    /// <param name="logger">Optional logger used for non-fatal envelope metadata warnings.</param>
    public GridContextFactory(ILogger<GridContextFactory>? logger = null)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public IGridContext CreateFromEnvelope(
        ITransportEnvelope envelope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var correlationId = envelope.CorrelationId ?? envelope.MessageId;
        var causationId = envelope.CausationId ?? envelope.MessageId;
        var tenantId = ParseTenantIdOrInternal(envelope.TenantId, envelope.MessageId);
        var baggage = envelope.Headers is { Count: > 0 }
            ? new Dictionary<string, string>(envelope.Headers)
            : [];

        return new GridContextSnapshot(
            nodeId: ValidateRequiredEnvelopeValue(envelope.NodeId, nameof(envelope.NodeId), envelope.MessageId),
            studioId: ValidateRequiredEnvelopeValue(envelope.StudioId, nameof(envelope.StudioId), envelope.MessageId),
            environment: ValidateRequiredEnvelopeValue(envelope.Environment, nameof(envelope.Environment), envelope.MessageId),
            correlationId: correlationId,
            causationId: causationId,
            tenantId: tenantId,
            projectId: envelope.ProjectId,
            baggage: baggage,
            cancellation: cancellationToken,
            createdAtUtc: envelope.Timestamp);
    }

    private static string ValidateRequiredEnvelopeValue(
        string? value,
        string fieldName,
        string messageId)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        throw new EnvelopeValidationException(
            $"Transport envelope '{messageId}' is missing required Grid identity field '{fieldName}'.");
    }

    private TenantId ParseTenantIdOrInternal(string? value, string messageId)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return TenantId.Internal;
        }

        if (TenantId.TryParse(value, out var tenantId))
        {
            return tenantId;
        }

        _logger?.LogWarning(
            "Malformed tenant id on transport envelope {MessageId}; using internal tenant.",
            messageId);

        return TenantId.Internal;
    }
}
