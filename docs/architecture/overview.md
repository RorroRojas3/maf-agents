# Architecture overview

## Overview

Andes Agents is an ASP.NET Core (.NET 10) host for a single hosted [Microsoft Agent Framework](https://learn.microsoft.com/agent-framework/) agent — the weather agent. It answers current-conditions and short-range-forecast questions over two agent protocols (A2A and AG-UI), persists every conversation to Azure Cosmos DB so a caller can resume it later, and exposes that history as a small REST resource. Every route requires a Microsoft Entra ID bearer token except agent discovery and health checks. The solution also owns a second, relational store — an insurance-policy table in SQL Server 2025 through EF Core — that today exists at the database layer only: see [The `[Core].[Policy]` table](#the-corepolicy-table).

Read this page first for the shape of the solution. [Sessions and history](../conversations/sessions-and-history.md) and [Hosting and protocols](../agent/hosting-and-protocols.md) go deep on the two halves of a request; [Configuration](../operations/configuration.md) and the [Runbook](../operations/runbook.md) cover running it.

## Solution layout

The solution is `weather-agent/Andes.Agents/Andes.Agents.slnx`, six projects layered `Api → Service → Repository → Entity → Common`, with `Dto → Common` and `Service` depending on `Dto`. `Api` names every project whose types appear in its source — `Service`, `Repository` and `Dto` — rather than relying on the transitive reference through `Service` to make its own use of `Dto` types compile:

```
Andes.Agents.Api            ASP.NET Core host: endpoints, DI wiring, middleware
      │
Andes.Agents.Service ──────┐  agent pipeline, session store/history provider, weather tools
      │                    │
Andes.Agents.Repository    │  Cosmos DB access + SQL Server access (EF Core)
      │                    │
Andes.Agents.Entity        │  Cosmos document/state-bag records + the Policy entity
      │                    │
Andes.Agents.Common   Andes.Agents.Dto
(constants only)      (request/response contracts)
```

`Entity` never references `Dto`; `Common` references nothing. There's no `Abstractions` project and no controllers — every endpoint is a minimal-API module. `Repository` is provider-first, and now holds two sibling provider folders. For sessions and messages: `ISessionRepository` and `ISessionMessageRepository` are store-agnostic contracts under `Repository/Sessions/Interfaces/`, and their one implementation, `CosmosSessionRepository`/`CosmosSessionMessageRepository`, lives in `Repository/Cosmos/Sessions/` — the interface and its implementation are deliberately in different files here, because a second store would add a sibling folder rather than touch this one. For the policy database, `Repository/Sql/` owns EF Core end to end (`PolicyDbContext`, its configuration, its migrations); nothing reads or writes through it yet — see [The `[Core].[Policy]` table](#the-corepolicy-table). `Microsoft.Azure.Cosmos` and `Microsoft.EntityFrameworkCore.*` each resolve in exactly one project, `Repository`.

`Directory.Build.props` and `Directory.Packages.props`, beside the `.slnx`, hold settings and package versions shared by all six projects: central package management, `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`, and `GenerateDocumentationFile` (which also turns on `CS1591`, so an undocumented public member fails the build). The solution now has two stores: Azure Cosmos DB through the raw `Microsoft.Azure.Cosmos` SDK for sessions and messages, and SQL Server 2025 through EF Core 10 for the policy table — see [Where things live](#where-things-live) and [The `[Core].[Policy]` table](#the-corepolicy-table). Validation throughout the solution is **FluentValidation**: every options class and DTO validator is an `AbstractValidator<T>` declared in the same file as the type it validates, wired into `IOptions<T>` through `Common/Validation/FluentValidateOptions.cs` and `.ValidateWithFluentValidation().ValidateOnStart()` — never `System.ComponentModel.DataAnnotations`.

## Request flow

A turn of the weather agent — whether it arrives over A2A or AG-UI — moves through the same pipeline:

1. **Authentication and rate limiting.** The bearer token is validated (Microsoft.Identity.Web); `HttpContext.User` carries the caller's Entra object id (`oid`). The rate limiter partitions on that id.
2. **Session lookup.** The protocol handler resolves the hosted agent and calls `PersistedAgentSessionStore.GetSessionAsync(agent, continuationId)` — the A2A `contextId` or the AG-UI `threadId`. The store validates the id, point-reads the caller's session document, and either deserializes it (recording its Cosmos ETag) or creates a fresh session and seeds its history state.
3. **The agent runs.** `agent.RunStreamingAsync(...)` flows through, in order: `OpenTelemetryAgent` (opens the `invoke_agent` span) → `UsageRecordingAgent` (wraps the call to fold token usage into the session) → the inner `ChatClientAgent`. Before the model is called, `PersistedChatHistoryProvider.ProvideChatHistoryAsync` loads and replays the session's most recent messages. The chat client pipeline is `FunctionInvokingChatClient` → `OpenTelemetryChatClient` → the Azure AI Foundry Responses API client, with `store:false` so Cosmos DB is the only place conversation state lives. Tools run in-process. `PersistedChatHistoryProvider.StoreChatHistoryAsync` then appends the turn's messages to Cosmos DB in one transactional batch, using deterministic ids so a second writer for the same session collides instead of silently duplicating.
4. **Session save.** The protocol handler calls `SaveSessionAsync`, which creates or ETag-guarded-replaces the session document with the refreshed title, message count, cumulative usage, and serialized agent state. A stale ETag (another turn finished first) surfaces as a 409 `conversation-busy`.
5. **Conversation history.** `GET api/conversations` and its children read the same Cosmos DB documents directly, scoped to the caller's own `oid` — independent of the agent pipeline.

See [Sessions and history](../conversations/sessions-and-history.md) for the document shapes and conflict semantics, and [Hosting and protocols](../agent/hosting-and-protocols.md) for the protocol bindings and the agent pipeline in more detail.

## Where things live

| Project | Folder | Holds |
|---|---|---|
| `Api` | `Endpoints/` | `AgentEndpoints.cs` (A2A + AG-UI + the agent card), `SessionEndpoints.cs` (`api/conversations`), `WeatherAgentCard.cs` |
| | `Configuration/` | One `Add<Feature>` per feature (`AgentsConfiguration`, `AuthenticationConfiguration`, `CorsConfiguration`, `SessionsConfiguration`, `WeatherConfiguration`, …) and `Configuration/Providers/` (`MicrosoftFoundryProviderConfiguration.cs`, `AzureOpenAIProviderConfiguration.cs`, `OpenAIChatClientFactory.cs`) — persistence has no entry here; `Program.cs` calls the `Repository/Cosmos/` provider's own `AddAndesCosmosPersistence` directly |
| | `ExceptionHandlers/` | `GlobalExceptionHandler.cs` — the only `IExceptionHandler`, registered by `ExceptionHandlingConfiguration`; also maps `FluentValidation.ValidationException` to a 400 with a per-property `errors` object |
| | `Filters/` | `ValidationEndpointFilter.cs` — the static `Require<T>()` factory attached to the two `[AsParameters]` endpoints in `SessionEndpoints.cs` |
| | `Health/` | `HealthRegistration.cs` — the policy (check name, tag, routes); the probes themselves, `CosmosHealthCheck.cs` and `SqlHealthCheck.cs`, live with their providers in `Repository/{Cosmos,Sql}/HealthChecks/` |
| | `Middleware/` | `RequestLoggingMiddleware.cs`, `RequestLoggingRegistration.cs` |
| | `Observability/` | `TelemetryRegistration.cs`, `RequestDescriptor.cs` — names a request by its matched route pattern for logging |
| | `Options/` | Api-only `<Area>Options` classes (`OpenAIEndpointOptions` base with `MicrosoftFoundryOptions` and `AzureOpenAIOptions`, `AzureAdOptions`, `AgentCardOptions`, `TelemetryOptions`, `RequestLoggingOptions`, `RateLimitingOptions`, `CorsOptions`, `KeyVaultOptions`, `AzureOptions`, `ApiDocsOptions`) — `CosmosDbOptions` moved to `Repository/Cosmos/Options/`, the store's own project |
| | `Problems/` | `ProblemTypes.cs`, `ProblemDetailsRegistration.cs` (adds the `traceId`/`requestId` extensions), `ProblemResponseWriter.cs` (its `WriteAsync` takes an optional per-property `errors` map) |
| | `Startup/` | `CosmosBootstrapper.cs`, `SqlBootstrapper.cs` — dev-only provisioning after `Build()`; both stay in Api rather than their provider folder so they always run after the startup validator (see [Startup order](#startup-order) below) |
| `Service` | `Sessions/` | `PersistedAgentSessionStore` (MAF `AgentSessionStore`), `PersistedChatHistoryProvider` (MAF `ChatHistoryProvider`), `SessionService` (the REST resource), `SessionMapper`, `SessionIdValidator`, `SessionsExceptions.cs` |
| | `Weather/` | `WeatherService` (the deterministic stub) + `Weather/Tools/WeatherToolProvider.cs` |
| | `Agents/` | `UsageRecordingAgent.cs` — a `DelegatingAIAgent` |
| | `Security/`, `Options/`, `Prompts/`, `Serialization/`, `Exceptions/` | Cross-cutting: caller identity, `SessionsOptions`, the embedded prompt + loader, session-state JSON options, the two solution-wide exceptions |
| `Repository` | `Sessions/` | Store-agnostic: `Interfaces/{ISessionRepository,ISessionMessageRepository}.cs`, `SessionsRecords.cs`, `SessionsExceptions.cs` — nothing here names a provider |
| | `Cosmos/` | Everything Cosmos-specific: `CosmosContainers`, `CosmosQueries`, `CosmosRecords.cs`, `PartitionKeys`, `CosmosPersistenceConfiguration.cs` (`AddAndesCosmosPersistence`/`AddAndesCosmosHealthCheck`), plus `HealthChecks/CosmosHealthCheck.cs`, `Options/CosmosDbOptions.cs`, `Provisioning/CosmosResourceProvisioner.cs`, `Serialization/{CosmosJsonOptions,UtcDateTimeOffsetJsonConverter}.cs`, and `Sessions/{CosmosSessionRepository,CosmosSessionMessageRepository}.cs` — the implementations of the contracts above |
| | `Sql/` | The second provider folder, EF Core end to end: `SqlPersistenceConfiguration.cs` (`AddAndesSqlPersistence`/`AddAndesSqlHealthCheck`, plus the shared `ConfigureSqlServer`), `HealthChecks/SqlHealthCheck.cs`, `Provisioning/SqlSchemaMigrator.cs`, `Interceptors/AuditTimestampInterceptor.cs`, `Options/SqlDbOptions.cs`, `DbContexts/{PolicyDbContext,PolicyDbContextDesignTimeFactory}.cs`, `Configurations/Policies/PolicyConfiguration.cs`, `Migrations/` — see [The `[Core].[Policy]` table](#the-corepolicy-table) |
| `Entity` | `Base/BaseEntity.cs` | `Id`, `DateCreated`, `DateUpdated`, `RowVersion` — shared by every relational entity; Cosmos documents don't use it |
| | `Sessions/SessionsRecords.cs` | `SessionDocument`, `SessionMessageDocument`, `SessionUsage`, `SessionHistoryState` — the Cosmos documents and the state-bag record the store and history provider share |
| | `Policies/Policy.cs` | The one relational entity, mapped by `PolicyConfiguration` |
| `Dto` | `Actions/Sessions/`, `Sessions/`, `Pagination/` | Paging query DTOs, response DTOs, `PaginatedResponseDto<T>` |
| `Common` | `Constants/` | Every constant catalog: `AgentNames`, `AuthorizationPolicies`, `ChatClientKeys`, `ClaimTypeNames`, `PolicyLimits`, `PromptNames`, `RateLimitPolicies`, `SessionIdRules`, `SessionStateKeys`, `TelemetryNames`, `WeatherLimits` |
| | `Enums/` | `PolicyStatuses` — stored by name in `[Core].[Policy]` and enforced there by a check constraint, not by the CLR type |
| | `Validation/` | `FluentValidateOptions.cs` (the FluentValidation-to-`IValidateOptions<T>` adapter) and `OptionsValidationExtensions.cs` (`ValidateWithFluentValidation()`) — the one place `Common` takes package references |

Deliberate departures from a typical layered API, driven by the domain being small and, for the policy table, unused above the database layer so far:

- **`Repository/Cosmos/` has no `DbContexts/`, `Configurations/`, or `Migrations/`** — those exist only under `Repository/Sql/`, the one place `Microsoft.EntityFrameworkCore.*` resolves. `Repository/` stays provider-first either way: a store-agnostic `Sessions/` at the top (contracts, records, exceptions), and everything each store needs — including its own `Options/` — inside its own sibling folder.
- **The prompt is an embedded resource**, not a shipped file on disk: `Service/Prompts/weather-agent-instructions.md` is read by `IPromptTemplateLoader` through its logical resource name (`Common/Constants/PromptNames.cs`).
- **Routes are a wire contract, independent of the domain word.** The `Sessions` feature is served at `api/conversations`; the A2A and AG-UI hosts live at `weather/a2a` and `weather/ui`. "Session" is the domain word throughout the code (`SessionDocument`, `SessionService`); "conversation" appears only in the route and in `ConversationBusyException`, which mirrors the `conversation-busy` problem type a caller sees on the wire.
- **`CosmosBootstrapper` and `SqlBootstrapper` stay in `Api/Startup/`**, not in their provider folders. `Program.cs` runs `IStartupValidator.Validate()` before calling either, so a process about to fail on its own configuration never reaches out to Cosmos DB or SQL Server first; a hosted service registered from inside `Repository` would run after that guard instead of before it.
- **`PolicyDbContext` has no repository, service, or endpoint above it yet.** The database layer — context, mapping, migrations, health check — is complete and owner-decided to stand alone for now; nothing in `Service` or `Api` reads or writes a `Policy`.

## Startup order

`Program.cs` sequences four phases after `WebApplication.Build()`, in this order: `IStartupValidator.Validate()` (every bound `Options` type, FluentValidation) → `EnsureCosmosResourcesAsync()` (Development-only container provisioning) → `MigrateSqlDatabaseAsync()` (Development-only migration) → `RunAsync()`. Validation runs first so a process about to fail on its own configuration never reaches either store; Cosmos before SQL is otherwise an arbitrary but fixed order. `/health/ready` now checks both stores — `cosmos` and `sql` — so a target environment's readiness gate isn't satisfied until whichever of the two it depends on for that check answers.

## The `[Core].[Policy]` table

`PolicyDbContext` maps one entity, `Policy`, to `[Core].[Policy]`. The schema, the compatibility level, and the concurrency strategy are chosen the way they would be for a table this application actually served traffic through — see [ADR-0002](../adr/0002-sql-server-policy-store.md) for why.

| Column | SQL type | Notes |
|---|---|---|
| `Id` | `uniqueidentifier` | Clustered primary key; EF's client-side **sequential** GUID generator, not `Guid.CreateVersion7()` — SQL Server orders `uniqueidentifier` by its last six bytes first, so a version-7 GUID's leading timestamp bytes would scatter inserts across the clustered index instead of appending to it. |
| `PolicyNumber` | `varchar(32)` | Business key printed on policy documents. |
| `ProductCode` | `varchar(20)` | |
| `Status` | `varchar(16)` | `PolicyStatuses` stored by name (`HasConversion<string>()`), constrained to the enum's current members. |
| `HolderReference` | `varchar(64)` | The holder's id in the system of record. |
| `HolderName` | `nvarchar(200)` | Personal data — stored, never logged. |
| `EffectiveDate`, `ExpirationDate` | `date` | `DateOnly`. |
| `PremiumAmount`, `CoverageAmount` | `decimal(19,4)` | |
| `CurrencyCode` | `char(3)` | Fixed-length, upper-case ISO 4217. |
| `DateCreated`, `DateUpdated` | `datetimeoffset` | Stamped by `AuditTimestampInterceptor` on every save — `Added` sets both, `Modified` moves only `DateUpdated`; the interceptor runs before `SaveChanges` detects changes, so it reads `ChangeTracker.Entries<BaseEntity>()` itself rather than trusting entity state set earlier in the pipeline. |
| `RowVersion` | `rowversion` | Optimistic concurrency token; a stale write throws `DbUpdateConcurrencyException` rather than overwriting a concurrent change. |

**Indexes** — each exists for one named query, since every index taxes every write:

1. **`PK_Policy`** (clustered, on `Id`) — point reads by primary key; sequential GUIDs keep inserts appending at the end of the B-tree instead of fragmenting it.
2. **`IX_Policy_PolicyNumber`** (unique) — lookup by the number printed on documents; the uniqueness constraint is enforced here, not just validated in code.
3. **`IX_Policy_HolderReference`** with `INCLUDE (PolicyNumber, Status, EffectiveDate, ExpirationDate)` — a holder's policy list, answered entirely from the index with no key lookup back to the clustered index.
4. **`IX_Policy_Status_ExpirationDate`** (composite, equality column first) — renewal, lapse, and expiry sweeps (`Status = @status AND ExpirationDate < @cutoff`). Not a filtered index on `Status = 'Active'`: SQL Server skips a filtered index when the status arrives as a query parameter rather than a literal.

**Check constraints** enforce five invariants the CLR types alone can't:

- `CK_Policy_Status` — `Status` (compared under a binary collation) is one of the current `PolicyStatuses` member names, generated from `Enum.GetNames<PolicyStatuses>()` so adding a member is a schema change caught by `migrations has-pending-model-changes`, not a silent gap.
- `CK_Policy_CoverageWindow` — `ExpirationDate > EffectiveDate`.
- `CK_Policy_PremiumAmount` — `PremiumAmount >= 0`.
- `CK_Policy_CoverageAmount` — `CoverageAmount > 0`.
- `CK_Policy_CurrencyCode` — `CurrencyCode`, compared under `Latin1_General_100_BIN2` (binary, so `'usd'` doesn't pass as `'USD'`), matches three upper-case letters.

**Compatibility level and migrations history.** `SqlPersistenceConfiguration.ConfigureSqlServer` — shared by the runtime registration and `PolicyDbContextDesignTimeFactory`, so migrations are always generated against what the app actually runs — sets `UseCompatibilityLevel(170)` explicitly (EF Core otherwise assumes 150), which means SQL Server 2025 or Azure SQL only, and points `MigrationsHistoryTable` at `__EFMigrationsHistory` inside the `Core` schema, so everything the context owns — data and migration history alike — sits under one schema a grant can target. The same method turns EF's `SaveChangesFailed`, `QueryIterationFailed`, and `CommandError` diagnostics down from `Error` to `Debug`: a `SqlException` from a duplicate key (2601/2627) or a truncated value (2628) quotes the offending data in its message, and the exception itself still reaches the caller — only the log line that would otherwise carry that text at `Error` is suppressed. EF sensitive-data logging stays off regardless.
