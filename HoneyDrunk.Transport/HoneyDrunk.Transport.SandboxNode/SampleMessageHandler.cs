using HoneyDrunk.Kernel.Abstractions.Context;
using HoneyDrunk.Transport.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HoneyDrunk.Transport.SandboxNode;

/// <summary>
/// Message handler that ENFORCES Kernel vNext invariants.
/// This is NOT a demo - it is an invariant verification gate.
/// If Transport violates the one-GridContext-per-scope rule, this handler exposes it.
/// </summary>
public sealed class SampleMessageHandler(
    ILogger<SampleMessageHandler> logger,
    InvariantVerificationResult result,
    IServiceProvider serviceProvider) : IMessageHandler<SampleMessage>
{
    public Task HandleAsync(SampleMessage message, MessageContext context, CancellationToken cancellationToken)
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

            // ═══════════════════════════════════════════════════════════════════════════
            // CRITICAL INVARIANT: Resolve GridContext from DI and compare identity
            // ═══════════════════════════════════════════════════════════════════════════
            var diGridContext = serviceProvider.GetRequiredService<IGridContext>();

            logger.LogInformation("  DI GridContext:");
            logger.LogInformation("    Type: {Type}", diGridContext.GetType().FullName);
            logger.LogInformation("    HashCode: {Hash}", diGridContext.GetHashCode());
            logger.LogInformation("    IsInitialized: {Init}", diGridContext.IsInitialized);

            logger.LogInformation("  MessageContext.GridContext:");
            logger.LogInformation("    Type: {Type}", messageGridContext.GetType().FullName);
            logger.LogInformation("    HashCode: {Hash}", messageGridContext.GetHashCode());
            logger.LogInformation("    IsInitialized: {Init}", messageGridContext.IsInitialized);

            // Record instance details for reporting
            result.DiContextTypeName = diGridContext.GetType().FullName;
            result.DiContextHashCode = diGridContext.GetHashCode();
            result.MessageContextTypeName = messageGridContext.GetType().FullName;
            result.MessageContextHashCode = messageGridContext.GetHashCode();

            // ═══════════════════════════════════════════════════════════════════════════
            // CRITICAL CHECK: Reference equality
            // If this fails, Transport is creating its own GridContext (VIOLATION)
            // ═══════════════════════════════════════════════════════════════════════════
            var sameInstance = ReferenceEquals(diGridContext, messageGridContext);
            result.InstanceIdentityVerified = sameInstance;

            if (sameInstance)
            {
                logger.LogInformation("  ✅ SAME INSTANCE: ReferenceEquals = true");
            }
            else
            {
                logger.LogError("  ❌ DIFFERENT INSTANCES: ReferenceEquals = false");
                logger.LogError("      This is an ARCHITECTURE VIOLATION!");
                logger.LogError("      Transport MUST use the DI-owned GridContext.");
            }

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
                catch (InvalidOperationException)
                {
                    // Expected in non-HTTP scenario (ScopedGridContextAccessor not set)
                    result.AccessorAvailable = false;
                    logger.LogInformation("  Accessor: Not available (non-HTTP scenario)");
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
        catch (Exception ex)
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
