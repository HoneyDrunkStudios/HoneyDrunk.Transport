namespace HoneyDrunk.Transport.SandboxNode;

/// <summary>
/// Console + log decorations used by the sandbox canary so the box-drawing literals don't get
/// repeated inline (Sonar S1192).
/// </summary>
internal static class Banners
{
    /// <summary>Heavy 79-char divider used to delimit major scope blocks in the canary output.</summary>
    public const string Heavy = "═══════════════════════════════════════════════════════════════════════════════";

    /// <summary>Light 79-char divider used inside scope blocks.</summary>
    public const string Light = "───────────────────────────────────────────────────────────────────────────────";
}
