# HoneyDrunk.Transport.SandboxNode

A console sample that exercises the HoneyDrunk.Transport stack end-to-end against
the in-memory transport. It is **not** a published package (`IsPackable=false`) and
is excluded from SonarQube analysis — it exists as an architecture-enforcement
**canary**, not a demo.

## What It Does

The sandbox wires up Kernel (`AddHoneyDrunkNode`) plus the in-memory transport
(`AddHoneyDrunkInMemoryTransport`), publishes a `SampleMessage`, consumes it through
the middleware pipeline, and asserts the Kernel ⇄ Transport invariants — chiefly
that there is exactly one DI-owned `GridContext` per scope and that Transport never
creates its own on consume. If any invariant is violated the process exits non-zero.

## How To Run

From this directory:

```bash
# Normal mode: enforce all invariants (fails loudly on violation)
dotnet run

# Negative mode: verify fail-fast behavior for known-bad wiring
dotnet run -- --negative-mode
```

Negative mode can also be enabled with the `NEGATIVE_MODE=true` environment variable.

Requires the .NET 10 SDK. The sample uses only project references plus
HoneyDrunk.Kernel and the Generic Host, so no external broker or Azure resources
are needed.
