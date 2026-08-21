# ADR-0001: Use OpenAI directly as the model provider for samples

**Status**: Accepted
**Date**: 2026-08-09
**Deciders**: Rodrigo Rojas (repository owner)

> Extended, not superseded, by [ADR-0002](0002-foundry-via-openai-v1-route.md): a later sample
> reaches Microsoft Foundry within the same GA-packages-only constraint, through the
> OpenAI-compatible `/openai/v1/` route rather than through OpenAI itself.

## Context

Every sample in this repo is a console app that calls a real model, so each one needs a model provider and a client to reach it. Microsoft Agent Framework supports several — Microsoft Foundry, Azure OpenAI, OpenAI, Anthropic, Ollama — and the choice is the same for all samples, because a reader who learns one client-construction pattern should not have to relearn it in the next folder.

One constraint governs the choice: **stable (GA) packages only, never `--prerelease`.** A sample is only useful if someone can clone the repo months from now and have `dotnet build` succeed against the versions written down in `Directory.Packages.props`. Preview packages break that promise twice over — they can be unlisted, and their API shape can change between drops, which turns a teaching sample into a maintenance burden.

The framework core itself clears that bar: `Microsoft.Agents.AI` is GA at 1.17.0. The provider packages are where the channels diverge, and that divergence — not a preference for one vendor — is what decided this.

## Decision

Samples build their agents on **OpenAI directly**, through `Microsoft.Agents.AI.OpenAI` and the OpenAI Responses client.

| Package | Version | Channel |
| --- | --- | --- |
| `Microsoft.Agents.AI` | 1.17.0 | GA |
| `Microsoft.Agents.AI.OpenAI` | 1.17.0 | GA |
| `OpenAI` | 2.12.0 | GA |

The load-bearing detail is transitive: the nuspec for `Microsoft.Agents.AI.OpenAI` declares a dependency on `OpenAI` and **not** on `Azure.AI.OpenAI`. Nothing prerelease enters the graph by the back door, so this path is GA end to end.

Within that path, samples use the Responses client (`client.GetResponsesClient()`) rather than Chat Completions, because Responses carries the full hosted-tool surface that later samples will need.

## Consequences

**Positive:**

- The entire dependency graph is GA. `dotnet restore` never needs `--prerelease`, and the versions pinned in `Directory.Packages.props` will still resolve later.
- Running a sample needs an API key and nothing else — no Azure subscription, no resource deployment, no role assignment. That is the lowest possible barrier for someone who just wants to read and run the code.
- The Responses API is available today, so hosted tools, sessions, and the rest of the surface later samples depend on are reachable without changing providers.

**Negative:**

- This departs from the official quickstart, which leads with `dotnet add package Microsoft.Agents.AI.Foundry --prerelease`. A reader targeting Foundry has to translate the client-construction lines — the `AIAgent` surface above them is identical, but the first few lines of `Program.cs` differ from the docs they may have read first.
- An API key replaces `DefaultAzureCredential`, giving up managed identity and Key Vault. This is a deliberate carve-out from the repo's C# standard, recorded in `.claude/CLAUDE.md`; the intent behind that standard — no secret is ever committed — still holds absolutely.
- Azure-side capabilities that only exist behind a Foundry project endpoint (its catalog models and platform tools such as file search, SharePoint, and Fabric IQ) are out of reach for these samples.

**Neutral:**

- Key handling moves into configuration. Samples read `OpenAI:ApiKey` from `dotnet user-secrets`, which stores outside the repository tree, or `OPENAI_API_KEY` from the environment.
- Swapping providers later is a small, local change. Agents are built and used through `AIAgent`, so only client construction is provider-specific.

## Alternatives Considered

**Option 1: Microsoft Foundry** (`Microsoft.Agents.AI.Foundry` + `Azure.AI.Projects`) — the first choice, and what the official Agent Framework quickstart leads with.

- Pros: the richest tool surface of the three, Entra-based auth with no API key, and the path most closely matched to the documentation a reader arrives with.
- Cons: prerelease only. The current `Microsoft.Agents.AI.Foundry` is 1.17.0-preview.260804.1, and `Azure.AI.Projects` has no stable release either. Adopting it means adopting `--prerelease` for every sample.

**Option 2: Azure OpenAI** (`Azure.AI.OpenAI`) — the runner-up, and the option that would have preserved `DefaultAzureCredential`.

- Pros: keeps credentials out of configuration entirely, and keeps model access inside an Azure subscription.
- Cons: the last stable release is 2.1.0, from December 2024. It depends on `OpenAI` 2.1.0 while Agent Framework requires 2.10.0, so the two cannot be satisfied together inside the stable channel — and 2.1.0 predates the Responses API entirely, so even if the versions lined up, the client the samples are built around would not exist. Every release since is beta, from 2.2.0-beta.5 through 2.9.0-beta.1.

## Revisit when

This decision is a consequence of package channels, not of a judgment about the providers, so it should be reopened when the channels change:

- `Microsoft.Agents.AI.Foundry` reaches GA, or
- `Azure.AI.OpenAI` 2.2 or later reaches GA — that is, a stable release that both targets the Responses API and depends on `OpenAI` 2.10.0 or later.

Either event warrants a new ADR superseding this one. Until then, treat a proposal to add a prerelease provider package as a question to raise with the repository owner rather than a change to make.

## References

- [Microsoft Agent Framework overview](https://learn.microsoft.com/agent-framework/overview/) — the quickstart that installs `Microsoft.Agents.AI.Foundry --prerelease`
- [Agent Framework providers](https://learn.microsoft.com/agent-framework/agents/providers/)
- [Microsoft Foundry SDKs and endpoints](https://learn.microsoft.com/azure/foundry/how-to/develop/sdk-overview) — Foundry's `FoundryChatClient` and project-endpoint Responses API
- [`Directory.Packages.props`](../../Directory.Packages.props) — the pinned versions this ADR justifies
- [`samples/01-get-started/HelloAgent`](../../samples/01-get-started/HelloAgent/) — the decision in code
