namespace HoneyDrunk.Transport.SandboxNode;

/// <summary>
/// Captures invariant verification results from the message handler.
/// Thread-safe for async completion signaling.
/// </summary>
public sealed class InvariantVerificationResult
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
