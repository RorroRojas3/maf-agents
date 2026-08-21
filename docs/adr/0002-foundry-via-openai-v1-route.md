# ADR-0002: Reach Microsoft Foundry through the OpenAI-compatible v1 route

**Status**: Accepted
**Date**: 2026-08-20
**Deciders**: Rodrigo Rojas (repository owner)

## Context

[`samples/02-agents/ImageAgent`](../../samples/02-agents/ImageAgent/) needs a `gpt-image-2` deployment to draw and edit images, and Foundry — not OpenAI itself — is where a `gpt-image-2` deployment lives for readers whose models are Azure-hosted. That puts this sample in different territory from [ADR-0001](0001-openai-as-model-provider.md), which decided how samples reach *OpenAI*; it never had to decide how a sample would reach *Foundry*, because none needed to until now.

The constraint ADR-0001 established still applies unchanged: **stable (GA) packages only, never `--prerelease`.** The obvious way to reach Foundry — `Microsoft.Agents.AI.Foundry` plus `Azure.AI.Projects` — is exactly the option ADR-0001 already ruled out for being prerelease-only, and it still is: Microsoft's own quickstart installs it with `dotnet add package Microsoft.Agents.AI.Foundry --prerelease`. So a second decision was needed: given the same constraint, is a GA-only path into Foundry possible at all?

It is. Since August 2025, Azure OpenAI in Microsoft Foundry ships a "next generation v1" data-plane API — the `/openai/v1/` route — built specifically so an unmodified `OpenAI` client can talk to a Foundry resource with only its `base_url`/`Endpoint` changed. Microsoft's [API lifecycle guide](https://learn.microsoft.com/azure/foundry/openai/api-version-lifecycle#api-evolution) lists this as a stated goal of the route: "OpenAI client support with minimal code changes to swap between OpenAI and Azure OpenAI when using key-based authentication."

## Decision

`ImageAgent` reaches its Foundry resource with the plain GA `OpenAIClient` from `Microsoft.Agents.AI.OpenAI` — the exact package ADR-0001 already pinned — rather than `Microsoft.Agents.AI.Foundry`:

```csharp
OpenAIClient client = new(
    new ApiKeyCredential(foundry.ApiKey),
    new OpenAIClientOptions { Endpoint = foundry.EndpointUri });

ResponsesClient responsesClient = client.GetResponsesClient();
IChatClient visionClient = responsesClient.AsIChatClient(foundry.ChatModel);
ImageClient imageClient = client.GetImageClient(foundry.ImageModel);
```

`foundry.EndpointUri` must end with `/openai/v1/` — validated locally before any network call, because getting this wrong doesn't fail loudly: the SDK builds a request against a route that answers 404, which reads as "model not found" rather than "wrong endpoint." No `api-version` query parameter is sent; the v1 route routes unversioned traffic to the latest GA API by design.

One client, one route, and two deployments off it: `GetResponsesClient()` drives the agent and (via `AsIChatClient`) backs the image-to-text tool, and `GetImageClient(...)` backs image generation and editing. `Foundry:ChatModel` and `Foundry:ImageModel` are deployment names on the target resource, not OpenAI catalogue names — the v1 route resolves whatever the reader named the deployment.

## Consequences

**Positive:**

- Zero new packages, and no prerelease dependency enters the graph. `Microsoft.Agents.AI.OpenAI` and `OpenAI`, already pinned by ADR-0001, are all this needs.
- One client-construction shape covers the whole repo: build an `OpenAIClient`, optionally point it at an `Endpoint`, pull a `ResponsesClient`/`ImageClient` off it. A reader who has seen [HelloAgent](../../samples/01-get-started/HelloAgent/) recognizes `ImageAgent`'s `Program.cs` immediately — only the credential and the `Endpoint` differ.
- Foundry-hosted deployments — vision-capable chat models, `gpt-image-2` — become reachable from this repo without loosening the stable-packages constraint.

**Negative:**

- The `/openai/v1/` shape is a convention the GA `OpenAIClient` neither enforces nor documents; a sample that skips validating it fails with a confusing 404 instead of a clear message. `FoundryOptions.Validate()` exists specifically to catch this before the first request goes out.
- Giving up `Microsoft.Agents.AI.Foundry` also gives up its Foundry-specific conveniences — `FoundryAgent`, server-side conversation/session management, Entra ID auth with automatic token refresh — for as long as the repo can't take a prerelease dependency.
- Authentication is an API key (`ApiKeyCredential`), the same trade-off ADR-0001 already accepted for OpenAI direct. The v1 route does support Entra ID for callers using the `AzureOpenAI()`-family client, but that client isn't part of this repo's GA provider set.

**Neutral:**

- Two Foundry deployments are mandatory, not optional: `gpt-image-2` only emits images — it accepts no tools and produces no text — so it cannot drive the agent or read an image back. A separate chat/vision deployment fills both roles. `ConfigurationLoader` validates both are present, collects every problem at once, and exits with configuration-error code `78` rather than failing on the first missing key.
- `GetResponsesClient()` is annotated `OPENAI001` (evaluation-only) in `OpenAI` 2.12.0. `ImageAgent` suppresses it at the call site, the same pattern `HelloAgent` already uses — not a repo-wide suppression.

## Alternatives Considered

**Option 1: `Microsoft.Agents.AI.Foundry` + `Azure.AI.Projects`** — the officially documented path, and the one ADR-0001 already declined.

- Pros: `FoundryAgent`, Entra ID auth, server-side agent definitions and managed conversation sessions.
- Cons: prerelease only, unchanged since ADR-0001. Taking it would mean adopting `--prerelease` for this one sample while every other sample stays GA-only — a split the repo's standing constraint doesn't allow without raising it with the repository owner first.

**Option 2: keep every sample on OpenAI direct, and skip Foundry-only capability**

- Pros: no new decision needed; ADR-0001 already covers it.
- Cons: forecloses a real and common setup for this framework's audience — reaching models through a Foundry resource the reader already has — and rules out `gpt-image-2` specifically, which was the reason this sample exists.

## Revisit when

`Microsoft.Agents.AI.Foundry` reaches GA. At that point, compare its native surface — particularly Entra ID auth and managed sessions — against the v1-route pattern here, and decide whether to migrate. That comparison, and its outcome, belongs in a new ADR rather than an edit to this one.

## References

- [Azure OpenAI in Microsoft Foundry Models v1 API — API evolution](https://learn.microsoft.com/azure/foundry/openai/api-version-lifecycle#api-evolution) — the documented goal of "OpenAI client support with minimal code changes"
- [Azure OpenAI in Azure AI Foundry Models API lifecycle — code changes](https://learn.microsoft.com/azure/foundry/openai/api-version-lifecycle#code-changes) — the `base_url` / `/openai/v1` pattern this ADR follows
- [Models sold directly by Azure — GPT-5.6](https://learn.microsoft.com/azure/foundry/foundry-models/concepts/models-sold-directly-by-azure#gpt-56) — why both calls in this sample go through the Responses API
- [ADR-0001: Use OpenAI directly as the model provider for samples](0001-openai-as-model-provider.md) — the constraint this decision operates under
- [`samples/02-agents/ImageAgent`](../../samples/02-agents/ImageAgent/) — the decision in code
