# HoneyDrunk.Transport agent instructions

Own transport contracts, envelopes, broker adapters and middleware. Keep provider SDKs inside adapters and durable outbox persistence in its owning data implementation. Preserve ordering, cancellation, correlation and retry/idempotency behavior; a transport or outbox alone does not guarantee exactly-once end-to-end delivery.

Start with [README.md](README.md) and the relevant source/tests. Project files and lockfiles own SDK, framework and dependency versions.

Read the [shared engineering conventions](https://github.com/HoneyDrunkStudios/HoneyDrunk.Standards/blob/main/HoneyDrunk.Standards/docs/CONVENTIONS.md) and this repository's owning documentation before editing. Apply the parts relevant to this stack; preserve existing public contracts, dependency direction and repository-specific behavior. Verify shared capabilities in current code before reusing them; a catalog entry or scaffold is not an implemented integration.

Work within the selected request. Preserve unrelated changes and use a separate worktree when needed. Review the final diff, use Conventional Commits and ready-for-review PRs with exactly one accurate `Authorship:` line and a `Request:` line; include the authorship in commit trailers. Run meaningful checks for the affected behavior and report the reviewed/tested revision, failures and unrun checks. For documentation-only changes, check links, paths and instruction consistency. Preserve required checks and inspect actual latest-head Sonar new-code findings where analysis applies; do not suppress findings or weaken gates to obtain a pass. Legacy Grid Review is retired; do not restore its workers, queues or bypass labels. A configured replacement reviewer is not evidence of a completed review or enforcing merge check.

## Verification

From the repository root for code/build changes:

```sh
dotnet restore HoneyDrunk.Transport/HoneyDrunk.Transport.slnx
dotnet build HoneyDrunk.Transport/HoneyDrunk.Transport.slnx -c Release --no-restore
dotnet test HoneyDrunk.Transport/HoneyDrunk.Transport.slnx -c Release --no-build
```

Use the checked-in workflow and relevant test documentation for additional integration prerequisites, coverage and consumer checks. Do not use live resources or credentials merely to make a local check pass.

Read the [engineering guide](docs/engineering-guide.md) for repository-specific contracts and patterns.
