# ADR-0001: Pin the preview Agent Framework hosting packages

**Status**: Accepted
**Date**: 2026-09-09
**Deciders**: Rodrigo Rojas (repository owner)

## Context

The weather agent must be reachable over two agent protocols: [A2A](https://a2a-protocol.org/latest/), for agent-to-agent and daemon callers, and [AG-UI](https://learn.microsoft.com/agent-framework/integrations/by-component/ui/ag-ui/), for a browser-based chat client. Microsoft Agent Framework's own ASP.NET Core hosting packages are what implement both protocol servers (`MapA2AHttpJson`/`MapA2AJsonRpc`, `MapAGUIServer`) and the durable hosted-session abstraction (`AgentSessionStore`) this application's Cosmos DB store implements.

This repository's `main` branch already has a stable-packages-only rule for its samples, recorded in two prior ADRs there — "Use OpenAI directly as the model provider for samples" and "Reach Microsoft Foundry through the OpenAI-compatible v1 route" (both under `docs/adr/` on `main`; not present on this branch, so referenced here by title only). Neither had to decide about *hosting* packages, because none of those samples expose an agent behind a protocol server — they call an agent in-process. This application does need that, and as of the versions verified against the framework's `main` source on 2026-09-09, `Microsoft.Agents.AI.Hosting`, `.Hosting.AspNetCore`, `.Hosting.A2A.AspNetCore`, and `.Hosting.AGUI.AspNetCore` exist **only** as the prerelease build `1.20.0-preview.260831.1`, and `A2A.AspNetCore` only as `1.0.0-preview2` — there is no stable release of any of them to depend on instead. The Agent Framework core itself (`Microsoft.Agents.AI`, `Microsoft.Agents.AI.OpenAI`) is GA at `1.20.0` and stays there.

## Decision

Pin the entire hosting train — every package whose only job is exposing the agent over A2A or AG-UI, plus the session-store abstraction they share — to one exact prerelease build, via central package management in `Directory.Packages.props`:

| Package | Version | Channel |
|---|---|---|
| `Microsoft.Agents.AI` | 1.20.0 | GA |
| `Microsoft.Agents.AI.OpenAI` | 1.20.0 | GA |
| `Microsoft.Agents.AI.Hosting` | 1.20.0-preview.260831.1 | Preview |
| `Microsoft.Agents.AI.Hosting.AspNetCore` | 1.20.0-preview.260831.1 | Preview |
| `Microsoft.Agents.AI.Hosting.A2A.AspNetCore` | 1.20.0-preview.260831.1 | Preview |
| `Microsoft.Agents.AI.Hosting.AGUI.AspNetCore` | 1.20.0-preview.260831.1 | Preview |
| `A2A.AspNetCore` | 1.0.0-preview2 | Preview |

Every preview version is **exact**, not a floating or `*`-suffixed range — `dotnet restore` resolves the identical build every time, rather than silently picking up whatever the next preview drop changed. The model-provider packages this application also depends on (`OpenAI`, `Microsoft.Extensions.AI`) stay on their GA versions, following the pattern the two prior ADRs already established; this decision only widens the carve-out to cover hosting.

## Consequences

**Positive:**

- A2A and AG-UI hosting work today, on the packages actually shipping the protocol servers, rather than this application hand-rolling either protocol.
- The carve-out is narrow and explicit: only the packages with no GA alternative move off the stable channel; the model-provider and core agent packages stay GA, so most of the dependency graph still carries the guarantees the stable-only rule exists for.
- Pinning to one exact build, rather than a version range, means a `dotnet restore` today and one run months from now resolve identically — the closest a prerelease dependency can come to the reproducibility a GA pin gives for free.

**Negative:**

- Preview packages can be unlisted from NuGet or change their public API shape between drops, which is precisely the risk the stable-only rule was written to avoid. If `1.20.0-preview.260831.1` is ever pulled, `dotnet restore` breaks with no code change on this application's side, and the fix is an upgrade, not a rollback.
- No semantic-versioning guarantee holds between preview builds of these packages the way it does for GA releases; upgrading to a newer preview needs the same scrutiny as a major-version bump would for a GA dependency, every time.
- `OPENAI001` and `MAAI001` (the Responses API and its stored-output-disabled adapter, both still evaluation-only) are a related but separate concern already suppressed at their call site in `AzureOpenAIProviderConfiguration`; this ADR is only about the hosting packages, not that suppression.

**Neutral:**

- This is a scoped exception for this application, not a change to the stable-only rule stated on `main` — new packages added to this solution later still default to GA unless they hit the same "no stable alternative exists" wall these did.

## Alternatives Considered

**Option 1: Wait for GA and hand-roll the protocol servers in the meantime.**

- Pros: the whole dependency graph stays GA, with no exception to record.
- Cons: A2A (JSON-RPC and HTTP+JSON bindings, task lifecycle, agent card) and AG-UI (its SSE event stream and thread/run model) are both non-trivial specifications. Reimplementing either is an ongoing maintenance burden — including keeping pace with spec changes — for code this application would delete the day the real packages go GA.

**Option 2: Support only one protocol, to shrink the preview surface.**

- Pros: dropping AG-UI removes `Microsoft.Agents.AI.Hosting.AGUI.AspNetCore` from the graph.
- Cons: `Microsoft.Agents.AI.Hosting.A2A.AspNetCore` and `A2A.AspNetCore` are still prerelease-only, so this doesn't avoid taking a preview dependency — it only removes one package from an already-necessary exception, while foreclosing a browser-based chat client as a deployment target this application is meant to support.

## Revisit when

`Microsoft.Agents.AI.Hosting.*` and `A2A.AspNetCore` each reach a stable release. At that point, re-pin `Directory.Packages.props` to the GA versions and drop this ADR's carve-out; a follow-up ADR is only needed if the GA API surface has changed enough that the migration itself requires a decision.

## References

- [Host agents with A2A](https://learn.microsoft.com/agent-framework/integrations/a2a) — the hosting package this decision pins
- [Getting Started with AG-UI](https://learn.microsoft.com/agent-framework/integrations/by-component/ui/ag-ui/getting-started) — the other hosting package this decision pins
- [A2A protocol specification](https://a2a-protocol.org/latest/)
- "Use OpenAI directly as the model provider for samples" and "Reach Microsoft Foundry through the OpenAI-compatible v1 route" — the `main` branch's stable-packages-only ADRs this decision departs from for hosting only (`git show main:docs/adr/0001-openai-as-model-provider.md` and `git show main:docs/adr/0002-foundry-via-openai-v1-route.md`)
- [`Directory.Packages.props`](../../weather-agent/Andes.Agents/Directory.Packages.props) — the pinned versions this ADR justifies
