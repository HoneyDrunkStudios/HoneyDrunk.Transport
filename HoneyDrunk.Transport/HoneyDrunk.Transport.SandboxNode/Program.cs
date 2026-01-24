using HoneyDrunk.Kernel.Abstractions.Context;
using HoneyDrunk.Kernel.Abstractions.Identity;
using HoneyDrunk.Kernel.Context;
using HoneyDrunk.Kernel.Hosting;
using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.InMemory;
using HoneyDrunk.Transport.InMemory.DependencyInjection;
using HoneyDrunk.Transport.SandboxNode;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// ═══════════════════════════════════════════════════════════════════════════════
// HoneyDrunk.Transport Sandbox - Architecture Enforcement Canary
// ═══════════════════════════════════════════════════════════════════════════════
//
// NON-NEGOTIABLE INVARIANTS (sandbox crashes if violated):
//   1) Exactly ONE GridContext per DI scope
//   2) That GridContext is DI-owned by Kernel
//   3) Transport MUST NOT create its own GridContext on consume
//   4) MessageContext.GridContext MUST reference the DI-owned GridContext
//   5) All accessors return the SAME instance
//
// This is a canary, not a demo. If Transport violates Kernel vNext invariants,
// this sandbox MUST fail loudly with a non-zero exit code.
//
// Usage:
//   dotnet run                          - Normal mode: enforce full invariants
//   dotnet run -- --negative-mode       - Negative mode: verify fail-fast behavior
// ═══════════════════════════════════════════════════════════════════════════════

var negativeMode = args.Contains("--negative-mode") ||
                   Environment.GetEnvironmentVariable("NEGATIVE_MODE")?.ToLowerInvariant() == "true";

Console.WriteLine();
Console.WriteLine("╔═══════════════════════════════════════════════════════════════════════════════╗");
Console.WriteLine("║       🍯 HoneyDrunk.Transport Sandbox - Architecture Enforcement Canary       ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════════════════════════╝");
Console.WriteLine($"Mode: {(negativeMode ? "NEGATIVE (fail-fast verification)" : "NORMAL (invariant enforcement)")}");
Console.WriteLine();

try
{
    if (negativeMode)
    {
        await RunNegativeModeAsync();
    }
    else
    {
        await RunNormalModeAsync();
    }
}
catch (Exception ex)
{
    Console.WriteLine();
    Console.WriteLine("╔═══════════════════════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║                    ❌ SANDBOX FAILED - ARCHITECTURE VIOLATION                 ║");
    Console.WriteLine("╚═══════════════════════════════════════════════════════════════════════════════╝");
    Console.WriteLine($"Exception: {ex.GetType().Name}");
    Console.WriteLine($"Message: {ex.Message}");
    if (ex.InnerException != null)
    {
        Console.WriteLine($"Inner: {ex.InnerException.Message}");
    }
    Environment.ExitCode = 1;
}

// ═══════════════════════════════════════════════════════════════════════════════
// NORMAL MODE: Enforce all Kernel vNext invariants
// ═══════════════════════════════════════════════════════════════════════════════
static async Task RunNormalModeAsync()
{
    const string queueAddress = "sandbox-queue";

    var builder = Host.CreateApplicationBuilder();

    builder.Logging.ClearProviders();
    builder.Logging.AddSimpleConsole(options =>
    {
        options.SingleLine = false;
        options.TimestampFormat = "HH:mm:ss.fff ";
        options.IncludeScopes = true;
    });
    builder.Logging.SetMinimumLevel(LogLevel.Information);

    // ═══════════════════════════════════════════════════════════════════════════
    // KERNEL REGISTRATION (required)
    // ═══════════════════════════════════════════════════════════════════════════
    builder.Services.AddHoneyDrunkNode(options =>
    {
        options.NodeId = new NodeId("sandbox-node");
        options.SectorId = new SectorId("core");
        options.EnvironmentId = new EnvironmentId("sandbox");
        options.StudioId = "honeydrunk-studios";
    });

    // Register Transport + InMemory
    builder.Services.AddHoneyDrunkInMemoryTransport(queueAddress, queueAddress, options =>
    {
        options.MaxConcurrency = 1;
    });

    // Register handler with invariant enforcement
    builder.Services.AddSingleton<InvariantVerificationResult>();
    builder.Services.AddScoped<IMessageHandler<SampleMessage>, SampleMessageHandler>();

    var host = builder.Build();
    var logger = host.Services.GetRequiredService<ILogger<Program>>();
    var verificationResult = host.Services.GetRequiredService<InvariantVerificationResult>();

    var consumer = host.Services.GetRequiredService<ITransportConsumer>();

    // ─────────────────────────────────────────────────────────────────────────────
    // PUBLISHER SCOPE
    // ─────────────────────────────────────────────────────────────────────────────
    logger.LogInformation("═══════════════════════════════════════════════════════════════════════════════");
    logger.LogInformation("📤 PUBLISHER: Initializing Kernel GridContext and publishing message");
    logger.LogInformation("═══════════════════════════════════════════════════════════════════════════════");

    using (var publishScope = host.Services.CreateScope())
    {
        var scopedPublisher = publishScope.ServiceProvider.GetRequiredService<IMessagePublisher>();

        // Get DI-owned GridContext and initialize it
        // NOTE: Cast required because Kernel exposes Initialize() on concrete GridContext
        var diContext = publishScope.ServiceProvider.GetRequiredService<IGridContext>();
        if (diContext is not GridContext kernelContext)
        {
            throw new InvalidOperationException(
                $"DI resolved IGridContext is not Kernel's GridContext. Got: {diContext.GetType().FullName}");
        }

        kernelContext.Initialize(
            correlationId: "sandbox-correlation-001",
            causationId: "sandbox-causation-root",
            tenantId: "tenant-acme",
            projectId: "project-alpha",
            baggage: new Dictionary<string, string>
            {
                ["trace-source"] = "sandbox-node",
                ["custom-tag"] = "verification-test"
            });

        logger.LogInformation("  Kernel GridContext initialized:");
        logger.LogInformation("    CorrelationId: {CorrelationId}", kernelContext.CorrelationId);
        logger.LogInformation("    NodeId: {NodeId}", kernelContext.NodeId);
        logger.LogInformation("    IsInitialized: {IsInitialized}", kernelContext.IsInitialized);

        var message = new SampleMessage
        {
            Content = "Invariant verification message",
            Tag = "canary-test"
        };

        await scopedPublisher.PublishAsync(queueAddress, message, kernelContext, CancellationToken.None);
        logger.LogInformation("  ✅ Message published");
    }

    logger.LogInformation("");

    // ─────────────────────────────────────────────────────────────────────────────
    // CONSUMER: Start and wait for handler to complete
    // ─────────────────────────────────────────────────────────────────────────────
    logger.LogInformation("═══════════════════════════════════════════════════════════════════════════════");
    logger.LogInformation("📥 CONSUMER: Processing message (invariant enforcement active)");
    logger.LogInformation("═══════════════════════════════════════════════════════════════════════════════");

    await consumer.StartAsync(CancellationToken.None);

    // Store the task once for correct comparison
    var completionTask = verificationResult.WaitAsync();
    var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10));
    var completedTask = await Task.WhenAny(completionTask, timeoutTask);

    await consumer.StopAsync(CancellationToken.None);

    if (completedTask != completionTask)
    {
        throw new TimeoutException("Message handler did not complete within timeout.");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // VERIFY INVARIANTS
    // ─────────────────────────────────────────────────────────────────────────────
    logger.LogInformation("");
    logger.LogInformation("═══════════════════════════════════════════════════════════════════════════════");
    logger.LogInformation("🔍 INVARIANT VERIFICATION RESULTS");
    logger.LogInformation("═══════════════════════════════════════════════════════════════════════════════");

    if (verificationResult.Exception != null)
    {
        throw new InvalidOperationException(
            "Handler threw exception during invariant verification.",
            verificationResult.Exception);
    }

    // Check all invariants
    var allPassed = true;

    // Invariant 1: GridContext values propagated correctly
    if (verificationResult.CorrelationIdMatch)
    {
        logger.LogInformation("  ✅ CorrelationId propagated correctly");
    }
    else
    {
        logger.LogError("  ❌ CorrelationId mismatch: expected 'sandbox-correlation-001', got '{Actual}'",
            verificationResult.ActualCorrelationId);
        allPassed = false;
    }

    // Invariant 2: Context is initialized
    if (verificationResult.IsInitialized)
    {
        logger.LogInformation("  ✅ GridContext.IsInitialized = true");
    }
    else
    {
        logger.LogError("  ❌ GridContext.IsInitialized = false");
        allPassed = false;
    }

    // Invariant 3 (CRITICAL): MessageContext.GridContext == DI GridContext
    if (verificationResult.InstanceIdentityVerified)
    {
        logger.LogInformation("  ✅ ReferenceEquals(DI GridContext, MessageContext.GridContext) = true");
    }
    else
    {
        logger.LogError("  ❌ INSTANCE DIVERGENCE DETECTED!");
        logger.LogError("      DI GridContext:             {DiType} @ {DiHash}",
            verificationResult.DiContextTypeName, verificationResult.DiContextHashCode);
        logger.LogError("      MessageContext.GridContext: {McType} @ {McHash}",
            verificationResult.MessageContextTypeName, verificationResult.MessageContextHashCode);
        allPassed = false;
    }

    // Invariant 4: Accessor returns same instance (if available)
    if (verificationResult.AccessorAvailable)
    {
        if (verificationResult.AccessorIdentityVerified)
        {
            logger.LogInformation("  ✅ ReferenceEquals(DI GridContext, Accessor.GridContext) = true");
        }
        else
        {
            logger.LogError("  ❌ Accessor returned different instance!");
            allPassed = false;
        }
    }
    else
    {
        logger.LogInformation("  ℹ️ IGridContextAccessor not testable (non-HTTP scenario, expected)");
    }

    logger.LogInformation("───────────────────────────────────────────────────────────────────────────────");

    if (!allPassed)
    {
        throw new InvalidOperationException(
            "GridContext instance divergence detected in Transport consume pipeline. " +
            "Transport MUST use the DI-owned GridContext, not create its own.");
    }

    logger.LogInformation("🎉 ALL INVARIANTS PASSED - Kernel + Transport integration verified!");
    logger.LogInformation("═══════════════════════════════════════════════════════════════════════════════");
}

// ═══════════════════════════════════════════════════════════════════════════════
// NEGATIVE MODE: Verify fail-fast behavior for architecture violations
// ═══════════════════════════════════════════════════════════════════════════════
static async Task RunNegativeModeAsync()
{
    Console.WriteLine("Testing fail-fast scenarios with REAL Kernel + Transport wiring...");
    Console.WriteLine();

    var testsPassed = 0;
    var testsFailed = 0;

    // ─────────────────────────────────────────────────────────────────────────────
    // TEST 1: Transport without Kernel registration must not provide GridContext
    // ─────────────────────────────────────────────────────────────────────────────
    Console.WriteLine("───────────────────────────────────────────────────────────────────────────────");
    Console.WriteLine("TEST 1: Transport without Kernel registration");
    Console.WriteLine("───────────────────────────────────────────────────────────────────────────────");

    try
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));

        // Register ONLY Transport - no Kernel
        services.AddHoneyDrunkInMemoryTransport("test", "test");

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        // Attempting to resolve IGridContext without Kernel should return null or throw
        var gridContext = scope.ServiceProvider.GetService<IGridContext>();

        if (gridContext == null)
        {
            Console.WriteLine("✅ PASSED: IGridContext not registered without Kernel");
            testsPassed++;
        }
        else
        {
            Console.WriteLine($"❌ FAILED: Got IGridContext without Kernel: {gridContext.GetType().FullName}");
            testsFailed++;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"✅ PASSED: Got expected exception: {ex.Message}");
        testsPassed++;
    }

    Console.WriteLine();

    // ─────────────────────────────────────────────────────────────────────────────
    // TEST 2: Publish with uninitialized Kernel GridContext must throw
    // ─────────────────────────────────────────────────────────────────────────────
    Console.WriteLine("───────────────────────────────────────────────────────────────────────────────");
    Console.WriteLine("TEST 2: Publish with uninitialized Kernel GridContext");
    Console.WriteLine("───────────────────────────────────────────────────────────────────────────────");

    try
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));

        services.AddHoneyDrunkNode(options =>
        {
            options.NodeId = new NodeId("test-node");
            options.SectorId = new SectorId("core");
            options.EnvironmentId = new EnvironmentId("test");
            options.StudioId = "test-studio";
        });

        services.AddHoneyDrunkInMemoryTransport("test", "test");

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var publisher = scope.ServiceProvider.GetRequiredService<IMessagePublisher>();
        var uninitializedContext = scope.ServiceProvider.GetRequiredService<IGridContext>();

        Console.WriteLine($"  IsInitialized: {uninitializedContext.IsInitialized}");

        await publisher.PublishAsync(
            "test-queue",
            new SampleMessage { Content = "Should fail" },
            uninitializedContext,
            CancellationToken.None);

        Console.WriteLine("❌ FAILED: Expected exception but publish succeeded");
        testsFailed++;
    }
    catch (InvalidOperationException ex)
    {
        Console.WriteLine($"✅ PASSED: Got expected InvalidOperationException: {ex.Message}");
        testsPassed++;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ FAILED: Got unexpected exception type: {ex.GetType().Name}: {ex.Message}");
        testsFailed++;
    }

    Console.WriteLine();

    // ─────────────────────────────────────────────────────────────────────────────
    // TEST 3: Accessing CorrelationId without initialization must throw
    // ─────────────────────────────────────────────────────────────────────────────
    Console.WriteLine("───────────────────────────────────────────────────────────────────────────────");
    Console.WriteLine("TEST 3: Kernel GridContext property access without initialization");
    Console.WriteLine("───────────────────────────────────────────────────────────────────────────────");

    try
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));

        services.AddHoneyDrunkNode(options =>
        {
            options.NodeId = new NodeId("test-node");
            options.SectorId = new SectorId("core");
            options.EnvironmentId = new EnvironmentId("test");
            options.StudioId = "test-studio";
        });

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var kernelContext = scope.ServiceProvider.GetRequiredService<IGridContext>();
        Console.WriteLine($"  IsInitialized: {kernelContext.IsInitialized}");

        // This should throw
        var correlationId = kernelContext.CorrelationId;
        Console.WriteLine($"❌ FAILED: Expected exception but got CorrelationId: {correlationId}");
        testsFailed++;
    }
    catch (InvalidOperationException ex)
    {
        Console.WriteLine($"✅ PASSED: Got expected exception: {ex.Message}");
        testsPassed++;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ FAILED: Got unexpected exception type: {ex.GetType().Name}: {ex.Message}");
        testsFailed++;
    }

    Console.WriteLine();

    // ─────────────────────────────────────────────────────────────────────────────
    // TEST 4: Verify GridContext is scoped (different per scope)
    // ─────────────────────────────────────────────────────────────────────────────
    Console.WriteLine("───────────────────────────────────────────────────────────────────────────────");
    Console.WriteLine("TEST 4: Verify Kernel GridContext is scoped (different instance per scope)");
    Console.WriteLine("───────────────────────────────────────────────────────────────────────────────");

    try
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));

        services.AddHoneyDrunkNode(options =>
        {
            options.NodeId = new NodeId("test-node");
            options.SectorId = new SectorId("core");
            options.EnvironmentId = new EnvironmentId("test");
            options.StudioId = "test-studio";
        });

        var provider = services.BuildServiceProvider();

        IGridContext context1;
        IGridContext context2;

        using (var scope1 = provider.CreateScope())
        {
            context1 = scope1.ServiceProvider.GetRequiredService<IGridContext>();
        }

        using (var scope2 = provider.CreateScope())
        {
            context2 = scope2.ServiceProvider.GetRequiredService<IGridContext>();
        }

        var areDifferent = !ReferenceEquals(context1, context2);
        Console.WriteLine($"  Scope 1 instance: {context1.GetHashCode()}");
        Console.WriteLine($"  Scope 2 instance: {context2.GetHashCode()}");
        Console.WriteLine($"  Are different: {areDifferent}");

        if (areDifferent)
        {
            Console.WriteLine("✅ PASSED: Different scopes have different GridContext instances");
            testsPassed++;
        }
        else
        {
            Console.WriteLine("❌ FAILED: Expected different instances but got same");
            testsFailed++;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ FAILED: Got unexpected exception: {ex.GetType().Name}: {ex.Message}");
        testsFailed++;
    }

    Console.WriteLine();

    // ─────────────────────────────────────────────────────────────────────────────
    // Summary
    // ─────────────────────────────────────────────────────────────────────────────
    Console.WriteLine("═══════════════════════════════════════════════════════════════════════════════");
    Console.WriteLine($"NEGATIVE MODE SUMMARY: {testsPassed} passed, {testsFailed} failed");
    Console.WriteLine("═══════════════════════════════════════════════════════════════════════════════");

    if (testsFailed > 0)
    {
        throw new InvalidOperationException($"{testsFailed} negative test(s) failed.");
    }

    await Task.CompletedTask;
}

// ═══════════════════════════════════════════════════════════════════════════════
// Verification Types
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Captures invariant verification results from the message handler.
/// Thread-safe for async completion signaling.
/// </summary>
#pragma warning disable CA1050 // Declare types in namespaces
public sealed class InvariantVerificationResult
#pragma warning restore CA1050 // Declare types in namespaces
{
    private readonly TaskCompletionSource<bool> _tcs = new();

    // Value checks
    public bool CorrelationIdMatch { get; set; }
    public string? ActualCorrelationId { get; set; }
    public bool IsInitialized { get; set; }

    // Instance identity checks (CRITICAL)
    public bool InstanceIdentityVerified { get; set; }
    public string? DiContextTypeName { get; set; }
    public int DiContextHashCode { get; set; }
    public string? MessageContextTypeName { get; set; }
    public int MessageContextHashCode { get; set; }

    // Accessor checks
    public bool AccessorAvailable { get; set; }
    public bool AccessorIdentityVerified { get; set; }

    // Exception capture
    public Exception? Exception { get; set; }

    public void Complete() => _tcs.TrySetResult(true);
    public Task WaitAsync() => _tcs.Task;
}
