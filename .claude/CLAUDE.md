# CLAUDE.md

Project memory for **maf-agents** — agents built with the [Microsoft Agent Framework](https://learn.microsoft.com/agent-framework/) in C# / .NET. It loads automatically every session and governs how Claude Code works in this repository.

This file lives at `.claude/CLAUDE.md`, **not** the repo root. Detailed standards in `.claude/rules/` auto-apply by file path; skills and subagents live under `.claude/`; `settings.json` pins `model: opus`, `effortLevel: xhigh`, and ten official plugins.

## About this repository

The purpose is to build and explore agents with the Microsoft Agent Framework on .NET. One solution exists, `weather-agent/Andes.Agents/Andes.Agents.slnx`, and it is a working ASP.NET Core host for a **weather agent** — read `docs/README.md` for the engineer-facing reference and `CHANGELOG.md` for what shipped.

An earlier shape of this repo — console samples under `samples/<NN-category>/` (`HelloAgent`, `ImageAgent`), two ADRs, a shared `config/appsettings.sample.json` and a root `maf-agents.slnx` — lives on `origin/main` and is **not present on this branch**. `git show origin/main:<path>` reads any of it. Its ADRs established two conventions this branch keeps: Foundry is reached through the OpenAI SDK on the resource's `/openai/v1/` route with an API key, and stable packages are preferred — with one recorded exception below.

### `weather-agent/Andes.Agents/` — the only code

Six projects, `Api → Service → Repository → Entity → Common` plus `Dto` (`Dto → Common`; `Service` references `Dto`, **`Repository` does not**), laid out exactly as `.claude/rules/api-architecture.md` prescribes. That rule is portable and names no repository; this file is where **this** solution's folder map, deviations and adopted names live — see "How this solution instantiates the architecture rule" below. `Api` is the composition root and declares a `ProjectReference` to every project it names — `Service`, `Repository` and `Dto` — rather than reaching them transitively.

- **What it does.** Entra ID bearer auth on every route (Microsoft.Identity.Web); the agent exposed over **A2A** (HTTP+JSON and JSON-RPC at `/weather/a2a`, card at `/.well-known/agent-card.json`) and **AG-UI** (`/weather/ui`); sessions and messages in **Azure Cosmos DB** under the hierarchical key `[/userId, /sessionId]` (Entra `oid` + A2A `contextId` / AG-UI `threadId`); `api/conversations` for a caller's own sessions; **SQL Server 2025** through EF Core for an insurance-policy table, the agent catalog and per-session usage summaries; OpenTelemetry → Azure Monitor with `gen_ai.*` spans; Key Vault when enabled; Scalar over the OpenAPI document; per-caller rate limiting; CORS allow-list; RFC 9457 problem details everywhere.
- **Model.** Two keyed `IChatClient` registrations reach the same Azure AI Foundry resource on its OpenAI-compatible `/openai/v1/` route, and `Api/Options/OpenAIEndpointOptions.cs` is the shared base that validates that route. **`AzureOpenAI` (required) is the Responses client that drives the agent** — deployment `rr-gpt-5.6-luna`, stored output **disabled** so Cosmos is the only conversation state. **`MicrosoftFoundry` (optional) is the Chat Completions client** — a blank `Endpoint` registers nothing rather than failing startup, and nothing consumes it yet. Both go through `OpenAIChatClientFactory.Decorate`: `FunctionInvokingChatClient → OpenTelemetryChatClient`. The agent adds `OpenTelemetryAgent → UsageRecordingAgent → ChatClientAgent` (`Api/Configuration/AgentsConfiguration.cs`). Options here expose a computed URI as `GetEndpointUri()`, a **method** rather than a property — originally because DataAnnotations reflected over every property and a blank endpoint threw out of the getter before the required-field message could be produced. FluentValidation evaluates only the rules it is given, so that constraint is gone; the method shape simply stayed.
- **One exception handler and one request log.** `Api/ExceptionHandlers/GlobalExceptionHandler.cs` is the only `IExceptionHandler`: a switch expression maps each exception to its problem+json shape, 500s carry no `detail` and no domain `type`, and 499 is returned only when the caller actually aborted. `Api/Middleware/RequestLoggingMiddleware.cs` writes one line per request. Both name the request by its **matched route pattern** (`Api/Observability/RequestDescriptor.cs`), so an id in the path never reaches a sink.
- **Weather data is a deterministic in-process stub** (`Service/Weather/WeatherService.cs`, owner decision): a fixed gazetteer, accent-insensitive search, seeded pseudo-random readings stable per place and local date. A real provider replaces the implementation behind `IWeatherService` without touching the tools.
- **SQL Server database — SQL Server 2025 through EF Core 10.** `PolicyDbContext` maps five entities, all on `BaseEntity`: a sequential-GUID clustered key, `DateCreated`/`DateModified` stamped by `AuditTimestampInterceptor` unless the caller supplied them, and `rowversion` concurrency.
  - `[Core].[Policy]` — a unique `PolicyNumber`, a holder index covering the list columns, a `(Status, ExpirationDate)` index for renewal and expiry sweeps, and check constraints on status names, the coverage window, both amounts and the currency code. Database layer only, by owner decision: no repository, service or endpoint uses it.
  - `[Core.Ref].[Agent]`, `[Core.Ref].[Model]` (deployment, provider name, USD prices per million input, cached-input and output tokens) and `[Core.Ref].[AgentModelMapping]` (activation is `DateCreated`; a unique index filtered on `[DateDeactivated] IS NULL` allows one active model per agent). **The catalog is owned by the database**: the migration that creates it seeds the weather agent through `HasData` (keyed by `AgentIds`) and GPT 5.6 Luna with its mapping as hand-written `InsertData` the EF model never sees, the app never writes it, and `IAgentCatalogCache` reads it once into `IMemoryCache` for 24 hours (`Invalidate()` for a future writer).
  - `[Core].[Session]` (entity `SessionSummary`) — one row per session incarnation, unique on `(UserId, SessionId, DateCreated)`: cumulative message and token counts, an `EstimatedCost` priced from the catalog as the counts grow, and `DateDeleted` once the conversation is deleted.

  The connection string (`ConnectionStrings:DefaultConnection`) is required, `/health/ready` includes it, and **startup needs SQL**: `Api/Startup/AgentCatalogBootstrapper.cs` refuses to start unless every `AgentNames.All` has an active model whose deployment equals `AzureOpenAI:Model`.
- **Packages.** Agent Framework core is GA `1.20.0`; the hosting packages (`Microsoft.Agents.AI.Hosting*`, `A2A.AspNetCore`) exist **only in preview** and are pinned exactly by owner decision (`docs/adr/0001-preview-hosting-packages.md`). `OpenAI` stays at 2.12.0 because `Microsoft.Extensions.AI.OpenAI` 10.9.0 rejects 2.13. The Cosmos SDK's build check demands an explicit `Newtonsoft.Json` pin even though the app serializes with System.Text.Json. EF Core is `10.0.12` and resolves `Microsoft.Data.SqlClient` 6.1, which still bundles the Entra ID providers; SqlClient 7 would need `Microsoft.Data.SqlClient.Extensions.Azure` for `Authentication=Active Directory *`.
- **Verified so far:** `dotnet build` (warnings as errors), `dotnet format --verify-no-changes`, and a smoke run with fake configuration — liveness, the anonymous card, 401/404 problem+json, OpenAPI/Scalar, forwarded-proto, the auth startup guard. Against a SQL Server 2025 container: the migration applied at startup and via `SqlSchemaMigrator`, `has-pending-model-changes` clean, the unique index and every check constraint rejecting bad rows, audit stamps on sync and async saves, stale-`rowversion` writes rejected, 2601/2628 failures leaving no data in logs at `Information` or above, the `SqlDb` startup guard, and the readiness probe failing inside its 5 s timeout. Against an isolated LocalDB instance (SQL Server 2025): the `DateUpdated` → `DateModified` rename keeping data, the catalog and session migrations applied by `database update` and by the idempotent script run twice with `sqlcmd -I`, the seed rows, the filtered unique index and check constraints rejecting bad rows; a scratch app covering both usage state keys and a rollback, the one-query catalog cache, every summary merge case (priced deltas, older and equal writes, tombstones, incarnations, 12 concurrent upserts) and the processor (retries, type-only failure logs, drain and prompt shutdown); and the Api refusing to start on a missing mapping or a deployment mismatch. **Not yet verified end to end:** a real agent turn over AG-UI/A2A and the `[Core].[Session]` row it produces, the Cosmos writes, the 409 on concurrent turns, App Insights spans, Entra ID sign-in to SQL Server. `docs/operations/runbook.md` has the steps.

### Invariants worth knowing before changing the session code

- The session store scopes every lookup by the caller's `oid` itself and is registered with `withIsolation: false`; the framework's isolation wrapper is deliberately **not** layered on top (its composed `"{key}::{id}"` string cannot be split into the two partition-key halves).
- **Session ids are GUIDs** — exactly `D` or `N` form (`SessionIdValidator`), because `[Core].[Session].SessionId` is a `uniqueidentifier`; the hosts issue `N` when a client sends none. A non-GUID id is a 400 `validation-error` on AG-UI and `api/conversations`. The A2A server turns any other exception into a 500 or JSON-RPC −32603, so `AgentsConfiguration` wraps the store in `A2AErrorTranslatingSessionStore`, which rethrows it as `A2AException(InvalidParams)`: 400 on HTTP+JSON, −32602 on JSON-RPC.
- Message ids are `{sessionId}:{sequence:D8}` and the session document is replaced with `IfMatchEtag`: two concurrent turns on one session end in a **409 `conversation-busy`**, never a silent overwrite, and the loser writes nothing. A turn that stored its messages but never saved the session is healed at the next lookup from `MAX(c.sequence)` — one cheap query per turn.
- **Session summaries never touch the turn.** After a successful Cosmos save or delete, the store and `SessionService` enqueue on `ISessionSummaryChannel` (bounded at 10,000; a full channel drops with a warning), and `SessionSummaryProcessor` writes in order, pricing from the catalog cache. Cached and reasoning tokens live under their own state key, `andes.usage.details`, so an older build that rewrites only `andes.usage` carries them through. The upsert merges cumulative counts: a lower count is an older write and changes nothing, growth adds `TokenPrices.CostOf` of the delta, and a deletion stamps `DateDeleted` and never clears it. `SessionStoreUnavailableException` (EF retries exhausted, a `SqlException` of severity 17 — out of log, disk, memory or locks — or 20 and above, a timeout) is retried with backoff; any other failure is dropped and logged by exception **type names only**.
- `AzureAd:Scopes` or `AzureAd:AppPermissions` must be configured or startup throws (`AzureAdOptions.HasCallerRequirement`); `AzureAd:AllowAnyAuthenticatedCaller` is the explicit opt-out that `appsettings.Development.json` sets. Any app in the tenant can obtain a token for the audience — only a scope or app role proves it was granted access.
- **Nothing a caller supplied may reach a log sink.** No bodies, no prompts, no message text, no query values, and never the `oid`. Prompt capture on spans is off in every environment; `Telemetry:EnableSensitiveData` or `OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT=true` is the deliberate opt-in. A caller correlates a failure by the `traceId` on the problem response, which is the Application Insights operation id.
- `Session` is the domain word; `Conversation` appears only in the wire route and the `conversation-busy` problem type. `Chat` means the model client or wire role. A Service type that persists is `Persisted<Thing>` — never `Cosmos<Thing>`, which names a store Service cannot see.
- **`Repository` is provider-first and owns each store end to end.** Store-agnostic contracts sit in `Repository/<Feature>/Interfaces/` (`Sessions`, `Agents`); everything Cosmos-specific — client construction, options, serializer, health probe, repositories — sits under `Repository/Cosmos/` and is registered by `AddAndesCosmosPersistence`; everything EF Core and SQL Server sits under `Repository/Sql/` and is registered by `AddAndesSqlPersistence`. `Microsoft.Azure.Cosmos` and the EF Core runtime each resolve in exactly one project — `Entity` references only `Microsoft.EntityFrameworkCore.Abstractions`, for mapping attributes; another store is a sibling folder, not a refactor.
- **Schema changes are migrations.** Change the model and add a migration; the only hand edits are the type-name strings after a CLR rename (`api-architecture.md`) and seed rows the EF model must not own — the first model and mapping, written as `InsertData` into `CreateSessionReportingTables`, which a regeneration drops. `.editorconfig` marks `**/Sql/Migrations/*.cs` as generated code, because `dotnet ef` writes the migration class without an `<auto-generated>` header. `SqlDb:ApplyMigrationsOnStartup` is for development; production applies `dotnet ef migrations script --idempotent` or a migrations bundle from its pipeline, **before** the app deploys. Run that script, and any hand edit of `[Core.Ref]` rows, with `sqlcmd -I`: DDL and DML on a table with a filtered index need `QUOTED_IDENTIFIER ON`. `SqlPersistenceConfiguration.ConfigureSqlServer` is shared with `PolicyDbContextDesignTimeFactory`, so provider settings change in one place; its compatibility level 170 means SQL Server 2025 or Azure SQL only.
- **Owner's schema conventions:** indexes are named by EF, never by us; strings are `nvarchar`; ids, the `oid` included, are `uniqueidentifier`; timestamps are `DateCreated` and `DateModified`, never `DateUpdated`, `DateActivated` or `DateStarted`. Catalog-like data lives in the database, not in `appsettings.json`. **Entity mapping is hybrid:** the entity carries its column shape as attributes — `[Table(..., Schema = ...)]`, `[Key]`, `[DatabaseGenerated]`, `[Timestamp]`, `[StringLength]`, `[Precision]` — and its `IEntityTypeConfiguration<T>` carries relationships, indexes, check constraints, conversions and seed data; no length or precision constants. A string is `[StringLength]` and always `nvarchar` — never `[MaxLength]`, `[Unicode(false)]` or a fixed length. Agent ids are constants (`AgentIds`) and agents are `HasData`; a model or mapping row never is.
- **The SQL readiness probe runs `SELECT 1` on its own unpooled connection, never `CanConnectAsync`**, which sits inside the retrying execution strategy and a one-minute login-retry loop. Its `Connect Timeout` comes from the registration's timeout: SqlClient keeps a cancelled open running until that timeout, and the pooled open the probe first used ignored the token outright — a stopped Docker container held it for the full 15 s default. The price is a full SQL Server login per call on an anonymous, unmetered endpoint; coalescing concurrent probes or restricting who reaches `/health/ready` is the open follow-up.
- **A `SqlException` message quotes data** — the duplicate key for 2601/2627, the truncated value for 2628 — and the session summary's key holds the `oid`. Two guards keep it out of sinks: `ConfigureSqlServer` lowers EF's `SaveChangesFailed`, `QueryIterationFailed` and `CommandError` events from `Error` to `Debug`, and `SessionSummaryProcessor` logs a failure by exception type names only. EF sensitive-data logging stays off.
- **Validation is FluentValidation everywhere**, DataAnnotations validation nowhere (the entity mapping attributes above are EF metadata, not validation). Each validated type carries its `AbstractValidator<T>` in the same file; options bind with `.ValidateWithFluentValidation().ValidateOnStart()` against the adapter in `Common/Validation/`. The built-in `AddValidation()` is DataAnnotations-only and is deliberately **not** registered — it would add a filter to every endpoint, streaming routes included, with no attributes left to act on. `[AsParameters]` records are checked only where `ValidationEndpointFilter.Require<T>()` is attached.
- Known limits: the A2A task store is in-memory (one instance or sticky sessions); forwarded headers trust any peer that reaches the container; `/health/ready` is anonymous and unmetered; the AG-UI host saves nothing for a failed or abandoned stream, so that usage reaches neither store; a session summary queued at a crash or into a full channel is lost until that session's next save.

### How this solution instantiates the architecture rule

`.claude/rules/api-architecture.md` is written in placeholders and names no repository, so it can be copied into any solution unchanged. This section is its instance here.

`<Root>` = `Andes.Agents`; `<Prefix>` = `Andes` (`AddAndesKeyVault`, `AddAndesTelemetry`, `AddAndesAuthentication`, `AddAndesCors`, `AddAndesRateLimiting`, `AddAndesProblemDetails`, `AddAndesExceptionHandling`, `AddAndesHealthChecks`, `AddAndesOpenApi`, `AddAndesAzureCredential`, `AddAndesCosmosPersistence`, `AddAndesCosmosHealthCheck`, `AddAndesSqlPersistence`, `AddAndesSqlHealthCheck`); `AddCoreServices` registers the clock, the caller context and the prompt loader. `<Provider>` = `Cosmos` (sessions and messages) and `Sql` (policies, the agent catalog and session summaries). The folder-by-folder map is `docs/architecture/overview.md`.

**Sanctioned deviations** — where this solution departs from the rule.

- **The prompt is an embedded resource**, not a shipped `<None>` asset: `Service/Prompts/weather-agent-instructions.md` is read by `PromptTemplateLoader` through its logical name in `Common/Constants/PromptNames.cs`.
- **A route may not match its feature.** The `Sessions` feature is served at `api/conversations` by `SessionEndpoints`; the A2A and AG-UI hosts live in `AgentEndpoints` at `weather/a2a` and `weather/ui`, and the card at `/.well-known/agent-card.json`. The wire contract wins over the naming rule.
- **`Api/Options/OpenAIEndpointOptions.cs` carries no `SectionName`.** It is an abstract base, not a section: `AzureOpenAIOptions` and `MicrosoftFoundryOptions` derive from it and carry the section names, and `OpenAIEndpointOptionsValidator<T>` is the shared rule set each subclasses so failures name their own section.
- **`Problems/ProblemTypes.cs` is a constant catalog outside `Common/Constants/`.** It stays in Api because it is the wire contract of the problem responses; every other catalog obeys the rule.
- **One exception handler, not one per condition.** `GlobalExceptionHandler.cs` maps every exception in a single switch expression, so a new domain exception means an arm there and a type in `ProblemTypes.cs`.
- **The EF context is `PolicyDbContext`** (the owner's name), not `<RootShort>DbContext`, and it maps every SQL table, not only policies.
- **The session-summary queue is `SessionSummaryChannel`**, not `<Subject>Queue`: CA1711 is a build error on a public type whose name ends in `Queue`.
- **`[Core].[Session]` is mapped by `SessionSummary`**, not `Session`, because `ISessionRepository` and `SessionDocument` already name the Cosmos session store.

**Design notes** — these follow the rule; recorded because the reason is not obvious.

- **EF Core lives only in `Repository/Sql/`** (`Agents/`, `Sessions/`, `DbContexts/`, `Configurations/{Policies,Agents,Sessions}/`, `HealthChecks/`, `Interceptors/`, `Migrations/`, `Provisioning/`); `Repository/Cosmos/` stays on the raw SDK and has no `DbContexts/`, `Configurations/`, `Interceptors/` or `Migrations/`.
- **`Api/Startup/CosmosBootstrapper.cs`, `SqlBootstrapper.cs` and `AgentCatalogBootstrapper.cs` are in Api, not the provider folders.** `Program.cs` runs `IStartupValidator.Validate()` before provisioning or migrating, so nothing external is touched by a process about to fail on its own configuration; a hosted service in Repository would run after that guard. The catalog check runs last, after migrations, and knows which deployment each agent's chat client calls.
- **`AuditTimestampInterceptor` keeps timestamps the caller supplied** (a default `DateCreated`/`DateModified` on add; a `DateModified` that differs from its original on update), because `SessionSummary` copies the Cosmos session's times; every other entity leaves them to the interceptor.
- **`TokenPrices.CostOf` sits on the Repository record** rather than in a Service class: the priced delta depends on the stored counts, so `SqlSessionSummaryRepository` applies it inside its read-merge-save retry loop.
- **`CosmosContainerNames` is interface segregation**, not leftover indirection: `CosmosContainers` and `CosmosResourceProvisioner` need three container ids, not the endpoint, key and connection mode.
- **`SessionsOptionsValidator` is public** while every other options validator is internal, because the composition root in Api registers it across the project boundary.
- **`Repository` reaches `Common` through the chain** (`Repository → Entity → Common`) rather than declaring it. The composition-root clause applies to Api, where transitive reliance would hide a layering error; below it the declared chain is the contract, and `Service` uses `Common.Constants` the same way.

## Build and tooling reality

- **`Directory.Build.props` and `Directory.Packages.props` sit beside the `.slnx`.** Central package management is on — a `PackageReference` never carries a `Version`; add new versions to the props file. `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`, `AnalysisLevel latest-recommended` and `GenerateDocumentationFile` are all on, so `.editorconfig` (file-scoped namespaces, `_camelCase` fields, IDE0005 unused usings, formatting) **and** CS1591 missing-XML-doc are build errors. Every public type and member needs a `<summary>`; positional records need either no `<param>` tags or all of them. The recommended analyzers include CA1711 (no public `…Queue`/`…Collection` names), CA1816 and CA2215 (a `Dispose` override must call `GC.SuppressFinalize` and the base) and CA1859 (concrete types for private fields and locals).
- Evaluation-only APIs (`OPENAI001` for the Responses client, `MAAI001` for the stored-output-disabled adapter) are suppressed with a call-site `#pragma`, never project-wide.
- No `global.json`; SDK 10.0.401 builds it. `.gitattributes` forces `* text=auto eol=lf`.
- **`dotnet-tools.json` at the repo root pins `dotnet-ef` 10.0.12** to match the EF packages. Run `dotnet tool restore`, then `dotnet tool run dotnet-ef …`: a global `dotnet-ef` on PATH may be older. Repository is both target and startup project, so the tools never boot the Api host.
- **No CI, no tests.** `dotnet build` and `dotnet format --verify-no-changes` are the local gates; the smoke procedure in the runbook is manual.
- On Windows a test server started from Git Bash is stopped by port (`netstat -ano | grep ":<port> "` then `taskkill //F //PID <pid>`); `pkill` and `taskkill //IM` leave it running and lock the DLL for the next build.

## Secrets

Nothing sensitive is committed. `appsettings.json` ships every key with secrets blank; local values go into `dotnet user-secrets` (the Api project has a `UserSecretsId`) or environment variables (`Foundry__ApiKey`, `CosmosDb__Key`, `ConnectionStrings__DefaultConnection`, …). With `KeyVault:Enabled`, secrets named with `--` (`Foundry--ApiKey`, `ConnectionStrings--DefaultConnection`) layer on top through `DefaultAzureCredential`. The Foundry API key has no managed-identity equivalent, which is the one accepted carve-out from the prefer-`DefaultAzureCredential` standard; Cosmos uses the shared credential whenever `CosmosDb:Key` is blank. SQL Server's connection string follows the Azure shape — `ConnectionStrings:DefaultConnection`, not a key inside `SqlDb` — so an App Service connection string named `DefaultConnection` lands on it. It signs in however that string says: Windows authentication for the LocalDB `andes-agents` default in `appsettings.json`, SQL authentication for the Docker container, `Authentication=Active Directory Managed Identity` in Azure.

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
- **Never log PII or secrets.** The `oid` is a personal identifier: it goes into partition keys and the reporting store, never into log messages or problem bodies.
- **XML docs** — a one-sentence `<summary>` on public API surface; interfaces carry the docs and implementations use `/// <inheritdoc />`. `<param>`/`<returns>` only where they add what the signature does not. `<remarks>` is for a caveat a caller must know, two sentences at most — not rationale, not history. `<example>`/`<code>` only where correct usage is genuinely non-obvious. `internal` and test types are not API surface: comment them only where a _why_ exists. **This overrides the `csharp-docs` skill wherever the two disagree** — that skill describes .NET's framework-reference house style, which is not this repository's.
- When reviewing, make only **high-confidence** suggestions; comment on _why_ a non-obvious design decision was made.

**Tests.** None exist yet. When they do: xUnit **v3** under `weather-agent/Andes.Agents/tests/`, named `MethodName_Scenario_ExpectedBehavior`, Arrange-Act-Assert structure but **no** `// Arrange` / `// Act` / `// Assert` comments, plain xUnit `Assert` (no FluentAssertions — v8+ is commercially licensed), isolation with **NSubstitute** and **never Moq**, and integration tests against **Testcontainers** (a local test instance only where no image exists). Agent code that calls a live model is not unit-tested. The `csharp-xunit` skill suggests Moq and a fluent assertion library; `csharp.md` overrides it.

**LINQ.** Method syntax only — `.Where(...).Join(...).Select(...)`, never query syntax (`from … in … select`).

**Validation.** FluentValidation only — no `System.ComponentModel.DataAnnotations` validation anywhere; EF mapping attributes on entities are not validation. Each validated type declares its `AbstractValidator<T>` in the same file, which is the one sanctioned exception to file-name-equals-type-name.

## Skills

The roster (names + descriptions) is always in context; these are the routing notes it lacks.

- **`microsoft-agent-framework`** is the primary skill for this repository. The framework is in public preview and the Learn API reference lags the packages (it still shows `AgentThread`, `MapAGUI`, `UseClaimsBasedSessionIsolation`); ground advice in the `main` source and conceptual docs through `microsoft-docs`, and trust the compiler over both.
- `csharp-async`, `csharp-docs`, `csharp-xunit` and `ef-core` are preloaded by the `csharp-code-reviewer` subagent; each is invokable on its own. `ef-core` applies to `Repository/Sql/` only — Cosmos persistence is the raw SDK.
- `prd` is preloaded by `prd-generator`; `technical-writing` (document-type templates) by `se-technical-writer`.
- The three `github-actions-*` skills are the lanes preloaded by `github-actions-reviewer`. **There is no `.github/` directory in this repository**, so all four are idle until someone adds workflows.

## Rules — `.claude/rules/`

Five files, auto-loaded when you edit a matching path; the globs live in each rule's frontmatter under a `paths:` list, and none carry a `description:`.

| Rule | `paths:` glob | Applies here? |
| --- | --- | --- |
| `csharp.md` | `**/*.cs` | **Yes** — the standards above. |
| `api-architecture.md` | `**/*.cs`, `**/*.csproj` | **Yes** — where every file goes and what it is called. Portable and repo-agnostic; this solution's instance of it is in §"How this solution instantiates the architecture rule". |
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
dotnet run --project weather-agent/Andes.Agents/Andes.Agents.Api                 # needs Foundry, Cosmos, SQL Server and AzureAd configured — see docs/operations/runbook.md

# EF Core migrations for PolicyDbContext (Repository is target and startup project)
dotnet tool restore
dotnet tool run dotnet-ef migrations add <Verb><Subject> --project weather-agent/Andes.Agents/Andes.Agents.Repository --startup-project weather-agent/Andes.Agents/Andes.Agents.Repository --context PolicyDbContext --output-dir Sql/Migrations
dotnet tool run dotnet-ef migrations has-pending-model-changes --project weather-agent/Andes.Agents/Andes.Agents.Repository --startup-project weather-agent/Andes.Agents/Andes.Agents.Repository --context PolicyDbContext
dotnet tool run dotnet-ef migrations script --idempotent --project weather-agent/Andes.Agents/Andes.Agents.Repository --startup-project weather-agent/Andes.Agents/Andes.Agents.Repository --context PolicyDbContext -o migrations.sql
sqlcmd -S <server> -d <database> -I -b -i migrations.sql                          # -I: the filtered index needs QUOTED_IDENTIFIER ON
```

Both the build and the format check pass on the current tree. There is no `dotnet test` — no test project exists. There is no root solution, so every command names its path explicitly.
