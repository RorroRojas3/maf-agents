# CLAUDE.md

Project memory for **maf-agents** — agents built with the [Microsoft Agent Framework](https://learn.microsoft.com/agent-framework/) in C# / .NET. It loads automatically every session and governs how Claude Code works in this repository.

This file lives at `.claude/CLAUDE.md`, **not** the repo root. Detailed standards in `.claude/rules/` auto-apply by file path; skills and subagents live under `.claude/`; `settings.json` pins `model: opus`, `effortLevel: xhigh`, and ten official plugins.

## About this repository

The purpose is to build and explore agents with the Microsoft Agent Framework on .NET. One solution exists, `weather-agent/Andes.Agents/Andes.Agents.slnx`, and it is a working ASP.NET Core host for a **weather agent** — read `docs/README.md` for the engineer-facing reference and `CHANGELOG.md` for what shipped.

An earlier shape of this repo — console samples under `samples/<NN-category>/` (`HelloAgent`, `ImageAgent`), two ADRs, a shared `config/appsettings.sample.json` and a root `maf-agents.slnx` — lives on `origin/main` and is **not present on this branch**. `git show origin/main:<path>` reads any of it. Its ADRs established two conventions this branch keeps: Foundry is reached through the OpenAI SDK on the resource's `/openai/v1/` route with an API key, and stable packages are preferred — with one recorded exception below.

### `weather-agent/Andes.Agents/` — the only code

Six projects, `Api → Service → Repository → Entity → Common` plus `Dto` (`Dto → Common`; `Service` and `Repository` reference `Dto`), laid out exactly as `.claude/rules/api-architecture.md` prescribes; its appendix is the folder-by-folder map and lists the three sanctioned deviations (Cosmos folders instead of EF ones, an embedded prompt, `api/conversations` as the wire route of the `Sessions` feature).

- **What it does.** Entra ID bearer auth on every route (Microsoft.Identity.Web); the agent exposed over **A2A** (HTTP+JSON and JSON-RPC at `/weather/a2a`, card at `/.well-known/agent-card.json`) and **AG-UI** (`/weather/ui`); sessions and messages in **Azure Cosmos DB** under the hierarchical key `[/userId, /sessionId]` (Entra `oid` + A2A `contextId` / AG-UI `threadId`); `api/conversations` for a caller's own sessions; OpenTelemetry → Azure Monitor with `gen_ai.*` spans; Key Vault when enabled; Scalar over the OpenAPI document; per-caller rate limiting; CORS allow-list; RFC 9457 problem details everywhere.
- **Model.** Two keyed `IChatClient` registrations reach the same Azure AI Foundry resource on its OpenAI-compatible `/openai/v1/` route, and `Api/Options/OpenAIEndpointOptions.cs` is the shared base that validates that route. **`AzureOpenAI` (required) is the Responses client that drives the agent** — deployment `rrp-gpt-5.6-luna`, stored output **disabled** so Cosmos is the only conversation state. **`MicrosoftFoundry` (optional) is the Chat Completions client** — a blank `Endpoint` registers nothing rather than failing startup, and nothing consumes it yet. Both go through `OpenAIChatClientFactory.Decorate`: `FunctionInvokingChatClient → OpenTelemetryChatClient`. The agent adds `OpenTelemetryAgent → UsageRecordingAgent → ChatClientAgent` (`Api/Configuration/AgentsConfiguration.cs`). Options here expose a computed URI as `GetEndpointUri()`, a **method**, because options validation reads every property and a blank endpoint would throw out of the getter before the required-field message could be produced.
- **One exception handler and one request log.** `Api/ExceptionHandlers/GlobalExceptionHandler.cs` is the only `IExceptionHandler`: a switch expression maps each exception to its problem+json shape, 500s carry no `detail` and no domain `type`, and 499 is returned only when the caller actually aborted. `Api/Middleware/RequestLoggingMiddleware.cs` writes one line per request. Both name the request by its **matched route pattern** (`Api/Observability/RequestDescriptor.cs`), so an id in the path never reaches a sink.
- **Weather data is a deterministic in-process stub** (`Service/Weather/WeatherService.cs`, owner decision): a fixed gazetteer, accent-insensitive search, seeded pseudo-random readings stable per place and local date. A real provider replaces the implementation behind `IWeatherService` without touching the tools.
- **Packages.** Agent Framework core is GA `1.20.0`; the hosting packages (`Microsoft.Agents.AI.Hosting*`, `A2A.AspNetCore`) exist **only in preview** and are pinned exactly by owner decision (`docs/adr/0001-preview-hosting-packages.md`). `OpenAI` stays at 2.12.0 because `Microsoft.Extensions.AI.OpenAI` 10.9.0 rejects 2.13. The Cosmos SDK's build check demands an explicit `Newtonsoft.Json` pin even though the app serializes with System.Text.Json.
- **Verified so far:** `dotnet build` (warnings as errors), `dotnet format --verify-no-changes`, and a smoke run with fake configuration — liveness, the anonymous card, 401/404 problem+json, OpenAPI/Scalar, forwarded-proto, the auth startup guard. **Not yet verified end to end:** a real agent turn over AG-UI/A2A, the Cosmos writes, the 409 on concurrent turns, App Insights spans. `docs/operations/runbook.md` has the steps.

### Invariants worth knowing before changing the session code

- The session store scopes every lookup by the caller's `oid` itself and is registered with `withIsolation: false`; the framework's isolation wrapper is deliberately **not** layered on top (its composed `"{key}::{id}"` string cannot be split into the two partition-key halves).
- Message ids are `{sessionId}:{sequence:D8}` and the session document is replaced with `IfMatchEtag`: two concurrent turns on one session end in a **409 `conversation-busy`**, never a silent overwrite, and the loser writes nothing. A turn that stored its messages but never saved the session is healed at the next lookup from `MAX(c.sequence)` — one cheap query per turn.
- `AzureAd:Scopes` or `AzureAd:AppPermissions` must be configured or startup throws (`AzureAdOptions.HasCallerRequirement`); `AzureAd:AllowAnyAuthenticatedCaller` is the explicit opt-out that `appsettings.Development.json` sets. Any app in the tenant can obtain a token for the audience — only a scope or app role proves it was granted access.
- **Nothing a caller supplied may reach a log sink.** No bodies, no prompts, no message text, no query values, and never the `oid`. Prompt capture on spans is off in every environment; `Telemetry:EnableSensitiveData` or `OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT=true` is the deliberate opt-in. A caller correlates a failure by the `traceId` on the problem response, which is the Application Insights operation id.
- `Session` is the domain word; `Conversation` appears only in the wire route and the `conversation-busy` problem type. `Chat` means the model client or wire role.
- Known limits: the A2A task store is in-memory (one instance or sticky sessions); forwarded headers trust any peer that reaches the container; `/health/ready` is anonymous and unmetered.

## Build and tooling reality

- **`Directory.Build.props` and `Directory.Packages.props` sit beside the `.slnx`.** Central package management is on — a `PackageReference` never carries a `Version`; add new versions to the props file. `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`, `AnalysisLevel latest-recommended` and `GenerateDocumentationFile` are all on, so `.editorconfig` (file-scoped namespaces, `_camelCase` fields, IDE0005 unused usings, formatting) **and** CS1591 missing-XML-doc are build errors. Every public type and member needs a `<summary>`; positional records need either no `<param>` tags or all of them.
- Evaluation-only APIs (`OPENAI001` for the Responses client, `MAAI001` for the stored-output-disabled adapter) are suppressed with a call-site `#pragma`, never project-wide.
- No `global.json`; SDK 10.0.401 builds it. `.gitattributes` forces `* text=auto eol=lf`.
- **No CI, no tests.** `dotnet build` and `dotnet format --verify-no-changes` are the local gates; the smoke procedure in the runbook is manual.
- On Windows a test server started from Git Bash is stopped by port (`netstat -ano | grep ":<port> "` then `taskkill //F //PID <pid>`); `pkill` and `taskkill //IM` leave it running and lock the DLL for the next build.

## Secrets

Nothing sensitive is committed. `appsettings.json` ships every key with secrets blank; local values go into `dotnet user-secrets` (the Api project has a `UserSecretsId`) or environment variables (`Foundry__ApiKey`, `CosmosDb__Key`, …). With `KeyVault:Enabled`, secrets named with `--` (`Foundry--ApiKey`) layer on top through `DefaultAzureCredential`. The Foundry API key has no managed-identity equivalent, which is the one accepted carve-out from the prefer-`DefaultAzureCredential` standard; Cosmos uses the shared credential whenever `CosmosDb:Key` is blank.

## Communication & comments (always)

**Responses**

- Lead with the answer or the change. No preamble, no filler, no restating the request.
- Do not re-summarize a plan or diff the user already saw; report only what changed or went wrong.
- Match length to substance — a one-line answer is a complete answer.

**Code comments**

Write for a senior engineer who knows the language and can read the code. A comment supplies what the code cannot — nothing else.

- **Default to no comment.** Add one only where a competent reader would still ask _why_. No per-type or per-member quota.
- **One or two lines.** Three needs a reason; past six the content belongs in `docs/` with a one-line pointer from the code.
- **Never reference PRD story ids, epics, or design frames.** Traceability belongs in the PR description and `docs/`; in source it goes stale and says nothing about the code in front of the reader. Test names describe behaviour, not story ids.
- **Never explain the language, framework, or API** — not what a primary constructor is, not how `IAsyncEnumerable` works, not what `nameof` returns.
- **Never narrate.** No "added X", "now uses Y". No restating the statement below it.
- **Do write**: why the non-obvious choice beat the obvious one, an invariant a caller must hold, a workaround with its link or issue, a spec or vendor constraint. State the cause once — not the argument, the alternatives weighed, or the history.
- **Compress rather than delete.** A real _why_ buried in a paragraph becomes one sentence; it does not disappear.

```csharp
// Bad — narrates, teaches the platform, cites a story
/// <summary>
/// US-118 asks the tool to check the image before the call. A primary constructor captures
/// the client, and IAsyncEnumerable lets the caller stream results as they arrive. Added
/// local validation here so we fail fast instead of round-tripping to the service.
/// </summary>

// Good — the one thing the code cannot say
/// <remarks>Checked locally: the service rejects masks over 4 MB with an opaque 400.</remarks>
```

## C# coding standards (always)

Full standards live in `.claude/rules/csharp.md` (auto-applies to `**/*.cs`), and `.editorconfig` encodes them for `dotnet format` and, through `EnforceCodeStyleInBuild`, for the build. The always-on core:

- Target the latest C# version (currently **C# 14**); prefer pattern matching, switch expressions, and `nameof(...)`.
- **File-scoped namespaces** everywhere — `.editorconfig` sets this at `error` severity and every file already complies.
- Prefer **primary constructors**; capture each injected dependency into a `private readonly` `_camelCase` field and use the field in method bodies.
- Prefer **collection expressions** (`[]`, `[1, 2, 3]`, `[.. items]`) over `new List<T>()`, `new T[] { }`, or `Array.Empty<T>()`.
- PascalCase for types, methods, and public members; `_camelCase` private fields (constants included); camelCase locals and parameters; `I`-prefixed interfaces.
- Declare variables non-nullable; validate `null` at entry points only; use `is null` / `is not null` — **never** `== null` / `!= null`.
- Suffix async methods with `Async`; **never** block with `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()`; no `async void` outside event handlers; flow a `CancellationToken` through long-running operations (see the `csharp-async` skill). `ConfigureAwait(false)` is used in `Service` and `Repository` and omitted in `Api` — follow the surrounding project.
- **Never log PII or secrets.** The `oid` is a personal identifier: it goes into partition keys, never into log messages or problem bodies.
- **XML docs** — a one-sentence `<summary>` on public API surface; interfaces carry the docs and implementations use `/// <inheritdoc />`. `<param>`/`<returns>` only where they add what the signature does not. `<remarks>` is for a caveat a caller must know, two sentences at most — not rationale, not history. `<example>`/`<code>` only where correct usage is genuinely non-obvious. `internal` and test types are not API surface: comment them only where a _why_ exists. **This overrides the `csharp-docs` skill wherever the two disagree** — that skill describes .NET's framework-reference house style, which is not this repository's.
- When reviewing, make only **high-confidence** suggestions; comment on _why_ a non-obvious design decision was made.

**Tests.** None exist yet. When they do: xUnit **v3** under `weather-agent/Andes.Agents/tests/`, named `MethodName_Scenario_ExpectedBehavior`, Arrange-Act-Assert structure but **no** `// Arrange` / `// Act` / `// Assert` comments, plain xUnit `Assert` (no FluentAssertions — v8+ is commercially licensed), isolation with **NSubstitute** (see the `csharp-xunit` skill). Agent code that calls a live model is not unit-tested.

## Skills

The roster (names + descriptions) is always in context; these are the routing notes it lacks.

- **`microsoft-agent-framework`** is the primary skill for this repository. The framework is in public preview and the Learn API reference lags the packages (it still shows `AgentThread`, `MapAGUI`, `UseClaimsBasedSessionIsolation`); ground advice in the `main` source and conceptual docs through `microsoft-docs`, and trust the compiler over both.
- `csharp-async`, `csharp-docs`, `csharp-xunit` and `ef-core` are preloaded by the `csharp-code-reviewer` subagent; each is invokable on its own. `ef-core` has nothing to act on — persistence is the raw Cosmos SDK.
- `prd` is preloaded by `prd-generator`; `technical-writing` (document-type templates) by `se-technical-writer`.
- The three `github-actions-*` skills are the lanes preloaded by `github-actions-reviewer`. **There is no `.github/` directory in this repository**, so all four are idle until someone adds workflows.

## Rules — `.claude/rules/`

Five files, auto-loaded when you edit a matching path; the globs live in each rule's frontmatter under a `paths:` list, and none carry a `description:`.

| Rule | `paths:` glob | Applies here? |
| --- | --- | --- |
| `csharp.md` | `**/*.cs` | **Yes** — the standards above. |
| `api-architecture.md` | `weather-agent/**` | **Yes** — where every file goes and what it is called; its appendix is this solution's folder map. |
| `aspnet-rest-apis.md` | `**/*.cs` | Loads on every C# edit; relevant to `Andes.Agents.Api`. |
| `azure-functions-csharp.md` | `**/*.cs` | Loads on every C# edit, but there are no Azure Functions here. Ignore it. |
| `terraform.md` | `**/*.tf` | No `.tf` files exist; never matches. |

When reviewing or planning without editing, `Read` the matching rule directly.

## MCP servers — see `@.mcp.json`

`.claude/settings.json` sets `enableAllProjectMcpServers: true`, so the servers in `@.mcp.json` are available. Two are defined:

- **`microsoft-learn`** (http) — ground version-specific .NET, Azure and Agent Framework answers in official docs (`microsoft_docs_search` → `microsoft_code_sample_search` → `microsoft_docs_fetch`) instead of memory.
- **`context7`** (npx) — docs outside learn.microsoft.com; resolve the library id first, then query.

`settings.json` also lists `terraform` under `enabledMcpjsonServers`, but `.mcp.json` does not define it, so that entry does nothing.

> **Trust gate:** a checked-in `.claude/settings.json` cannot approve its own repo's MCP servers while the folder is **untrusted** — the key is ignored and servers sit at "Pending approval" until the workspace trust dialog is accepted. To auto-approve regardless, add a name-based list to your **user-level** `~/.claude/settings.json`: `"enabledMcpjsonServers": ["microsoft-learn", "context7"]`. If a server shows **Rejected**, a stale per-project choice is cached — run `claude mcp reset-project-choices` in this repo.

## Delegation

Four subagents, all pinned to extra-high reasoning effort; `csharp-code-reviewer` and `github-actions-reviewer` run on `opus`, the other two on `sonnet`. Tools and preloaded skills live in each agent's frontmatter. Reviewer loops are capped: apply Critical/High findings, re-review only the changed files, at most two rounds, then surface anything still open to the user.

- **After implementing or modifying C# code**, delegate a quality review to `csharp-code-reviewer`. It reports findings; it does not edit files.
- **After a feature is implemented and the reviewer verdict passes**, delegate to `se-technical-writer` to update the Markdown under `docs/` and add the entry to the root `CHANGELOG.md` — [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) format, one `## [Unreleased]` section, one reader-facing entry per PR in the matching subsection. `docs/` follows `docs/README.md`'s index: `architecture/`, `conversations/`, `agent/`, `operations/`, `adr/`.
- **`github-actions-reviewer`** has nothing to review until a `.github/workflows/` tree exists. If workflows are added, route to it.
- **PRDs are not checked in.** `prd-generator` and the `prd` skill remain installed for a user who explicitly asks; nothing routes to them by default, and a PRD written that way is a scratch artifact.

## Common commands

```bash
# from the repo root
dotnet build weather-agent/Andes.Agents/Andes.Agents.slnx
dotnet format weather-agent/Andes.Agents/Andes.Agents.slnx --verify-no-changes   # read-only check
dotnet run --project weather-agent/Andes.Agents/Andes.Agents.Api                 # needs Foundry, Cosmos and AzureAd configured — see docs/operations/runbook.md
```

Both the build and the format check pass on the current tree. There is no `dotnet test` — no test project exists. There is no root solution, so every command names its path explicitly.
