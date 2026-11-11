# HoneyDrunk.Transport.SandboxNode

A console sample that exercises the HoneyDrunk.Transport stack end-to-end against
the in-memory transport. It is **not** a published package (`IsPackable=false`) and
is excluded from SonarQube analysis — it exists as an architecture-enforcement
**canary**, not a demo.

## What It Does

The sandbox wires up Kernel (`AddHoneyDrunkNode`) plus the in-memory transport
(`AddHoneyDrunkInMemoryTransport`), publishes a `SampleMessage`, consumes it through
the middleware pipeline, and verifies the current snapshot contract: Kernel owns
the DI-scoped runtime context; Transport creates an initialized `GridContextSnapshot`
from the envelope. The canary checks correlation, node/studio/environment, tenant,
project, causation and timestamp propagation, plus baggage isolation from the
envelope and DI context. Snapshot baggage remains mutable through `AddBaggage`;
isolation means mutations do not leak into the source. Any failed invariant exits non-zero.

## How To Run

From this directory (`tests/HoneyDrunk.Transport.SandboxNode/`):

```bash
# Normal mode: enforce all invariants (fails loudly on violation)
dotnet run

# Negative mode: verify fail-fast behavior for known-bad wiring
dotnet run -- --negative-mode
```

Negative mode can also be enabled with the `NEGATIVE_MODE=true` environment variable.

Requires the .NET 11 SDK. The sample uses only project references plus
HoneyDrunk.Kernel and the Generic Host, so no external broker or Azure resources
are needed.
