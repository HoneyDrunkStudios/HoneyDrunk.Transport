using HoneyDrunk.Kernel.Abstractions.Context;
using HoneyDrunk.Kernel.Context;
using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HoneyDrunk.Transport.SandboxNode;

/// <summary>
/// Message handler that enforces the transport snapshot contract.
/// This is NOT a demo - it is an invariant verification gate.
/// Verifies propagated identity and baggage isolation from the envelope and DI context.
/// </summary>
public sealed class SampleMessageHandler(
    ILogger<SampleMessageHandler> logger,
    InvariantVerificationResult result,
    IServiceProvider serviceProvider) : IMessageHandler<SampleMessage>
{
    public Task HandleAsync(SampleMessage message, MessageContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("───────────────────────────────────────────────────────────────────────────────");
            logger.LogInformation("🔍 INVARIANT VERIFICATION IN HANDLER");
            logger.LogInformation("───────────────────────────────────────────────────────────────────────────────");

            var messageGridContext = context.GridContext;

            if (messageGridContext is null)
            {
                result.Exception = new InvalidOperationException(
                    "MessageContext.GridContext is NULL. Pipeline middleware failed to set context.");
                return Task.CompletedTask;
            }

            var diGridContext = serviceProvider.GetRequiredService<IGridContext>();
            var envelope = context.Envelope;
            result.SnapshotIdentityVerified = !diGridContext.IsInitialized &&
                messageGridContext is GridContextSnapshot &&
                !ReferenceEquals(diGridContext, messageGridContext) &&
                messageGridContext.NodeId == "sandbox-node" &&
                messageGridContext.StudioId == "honeydrunk-studios" &&
                messageGridContext.Environment == "sandbox" &&
                messageGridContext.TenantId.ToString() == "01ARZ3NDEKTSV4RRFFQ69G5FAV" &&
                messageGridContext.ProjectId == "project-alpha" &&
                messageGridContext.CausationId == envelope.CausationId &&
                messageGridContext.CreatedAtUtc == envelope.Timestamp &&
                ReferenceEquals(context.Properties["GridContext"], messageGridContext);

            var headersCopied = envelope.Headers.All(header =>
                messageGridContext.Baggage.TryGetValue(header.Key, out var value) && value == header.Value);

            // Snapshot baggage is mutable by contract, but must not alias either source.
            messageGridContext.AddBaggage("snapshot-probe", "message-only");
            if (diGridContext is GridContext runtimeContext && !runtimeContext.IsInitialized)
            {
                runtimeContext.Initialize("canary-di-only");
            }

            diGridContext.AddBaggage("di-probe", "scope-only");
            result.SnapshotIsolationVerified = headersCopied &&
                !envelope.Headers.ContainsKey("snapshot-probe") &&
                !diGridContext.Baggage.ContainsKey("snapshot-probe") &&
                !messageGridContext.Baggage.ContainsKey("di-probe");

            // ═══════════════════════════════════════════════════════════════════════════
            // Value verification (secondary check)
            // ═══════════════════════════════════════════════════════════════════════════
            result.IsInitialized = messageGridContext.IsInitialized;
            result.ActualCorrelationId = messageGridContext.IsInitialized ? messageGridContext.CorrelationId : "(uninitialized)";
            result.CorrelationIdMatch = messageGridContext.IsInitialized &&
                                          messageGridContext.CorrelationId == "sandbox-correlation-001";

            if (result.CorrelationIdMatch)
            {
                logger.LogInformation("  ✅ CorrelationId: {Id}", messageGridContext.CorrelationId);
            }
            else
            {
                logger.LogError("  ❌ CorrelationId mismatch: expected 'sandbox-correlation-001', got '{Id}'",
                    result.ActualCorrelationId);
            }

            // ═══════════════════════════════════════════════════════════════════════════
            // Accessor check (for non-HTTP, may not be available)
            // ═══════════════════════════════════════════════════════════════════════════
            var accessor = serviceProvider.GetService<IGridContextAccessor>();
            if (accessor != null)
            {
                result.AccessorAvailable = true;
                try
                {
                    var accessorContext = accessor.GridContext;
                    var accessorSame = ReferenceEquals(diGridContext, accessorContext);
                    result.AccessorIdentityVerified = accessorSame;

                    logger.LogInformation("  Accessor.GridContext:");
                    logger.LogInformation("    HashCode: {Hash}", accessorContext.GetHashCode());
                    logger.LogInformation("    Same as DI: {Same}", accessorSame);
                }
                catch (InvalidOperationException ex)
                {
                    // Expected in non-HTTP scenario (ScopedGridContextAccessor not set).
                    result.AccessorAvailable = false;
                    logger.LogDebug(ex, "  Accessor: Not available (non-HTTP scenario)");
                }
            }
            else
            {
                result.AccessorAvailable = false;
                logger.LogInformation("  Accessor: Not registered");
            }

            logger.LogInformation("───────────────────────────────────────────────────────────────────────────────");

            return Task.CompletedTask;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            result.Exception = ex;
            logger.LogError(ex, "Exception during invariant verification");
            return Task.CompletedTask;
        }
        finally
        {
            result.Complete();
        }
    }
}
