# Architecture overview

## Overview

Andes Agents is an ASP.NET Core (.NET 10) host for a single hosted [Microsoft Agent Framework](https://learn.microsoft.com/agent-framework/) agent — the weather agent. It answers current-conditions and short-range-forecast questions over two agent protocols (A2A and AG-UI), persists every conversation to Azure Cosmos DB so a caller can resume it later, and exposes that history as a small REST resource. Every route requires a Microsoft Entra ID bearer token except agent discovery and health checks. The solution also owns a second, relational store, SQL Server 2025 through EF Core: an insurance-policy table that still exists at the database layer only (see [The `[Core].[Policy]` table](#the-corepolicy-table)), plus a database-owned agent catalog and a per-session usage summary that a background writer keeps for reporting (see [The agent catalog and session usage summaries](#the-agent-catalog-and-session-usage-summaries)). SQL Server is a required dependency at startup, not only at `/health/ready`.

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
Andes.Agents.Entity        │  Cosmos document/state-bag records + the SQL entities
      │                    │
Andes.Agents.Common   Andes.Agents.Dto
(constants only)      (request/response contracts)
```

`Entity` never references `Dto`; `Common` references nothing. There's no `Abstractions` project and no controllers — every endpoint is a minimal-API module. `Repository` is provider-first, and now holds two sibling provider folders. For sessions and messages: `ISessionRepository` and `ISessionMessageRepository` are store-agnostic contracts under `Repository/Sessions/Interfaces/`, and their one implementation, `CosmosSessionRepository`/`CosmosSessionMessageRepository`, lives in `Repository/Cosmos/Sessions/` — the interface and its implementation are deliberately in different files here, because a second store would add a sibling folder rather than touch this one. `Repository/Sql/` owns EF Core end to end (`PolicyDbContext`, its configuration, its migrations) for all three things it maps: the policy table, which nothing above the database layer reads or writes yet (see [The `[Core].[Policy]` table](#the-corepolicy-table)); the agent catalog; and the session usage summary — the latter two actively read and written (see [The agent catalog and session usage summaries](#the-agent-catalog-and-session-usage-summaries)). `Microsoft.Azure.Cosmos` and `Microsoft.EntityFrameworkCore.*` each resolve in exactly one project, `Repository`.

`Directory.Build.props` and `Directory.Packages.props`, beside the `.slnx`, hold settings and package versions shared by all six projects: central package management, `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`, and `GenerateDocumentationFile` (which also turns on `CS1591`, so an undocumented public member fails the build). The solution now has two stores: Azure Cosmos DB through the raw `Microsoft.Azure.Cosmos` SDK for sessions and messages, and SQL Server 2025 through EF Core 10 for the policy table, the agent catalog, and per-session usage summaries — see [Where things live](#where-things-live), [The `[Core].[Policy]` table](#the-corepolicy-table), and [The agent catalog and session usage summaries](#the-agent-catalog-and-session-usage-summaries). Validation throughout the solution is **FluentValidation**: every options class and DTO validator is an `AbstractValidator<T>` declared in the same file as the type it validates, wired into `IOptions<T>` through `Common/Validation/FluentValidateOptions.cs` and `.ValidateWithFluentValidation().ValidateOnStart()` — never `System.ComponentModel.DataAnnotations`.

## Request flow

A turn of the weather agent — whether it arrives over A2A or AG-UI — moves through the same pipeline:

1. **Authentication and rate limiting.** The bearer token is validated (Microsoft.Identity.Web); `HttpContext.User` carries the caller's Entra object id (`oid`). The rate limiter partitions on that id.
2. **Session lookup.** The protocol handler resolves the hosted agent and calls `PersistedAgentSessionStore.GetSessionAsync(agent, continuationId)` — the A2A `contextId` or the AG-UI `threadId`. The store validates the id, point-reads the caller's session document, and either deserializes it (recording its Cosmos ETag) or creates a fresh session and seeds its history state.
3. **The agent runs.** `agent.RunStreamingAsync(...)` flows through, in order: `OpenTelemetryAgent` (opens the `invoke_agent` span) → `UsageRecordingAgent` (wraps the call to fold token usage into the session) → the inner `ChatClientAgent`. Before the model is called, `PersistedChatHistoryProvider.ProvideChatHistoryAsync` loads and replays the session's most recent messages. The chat client pipeline is `FunctionInvokingChatClient` → `OpenTelemetryChatClient` → the Azure AI Foundry Responses API client, with `store:false` so Cosmos DB is the only place conversation state lives. Tools run in-process. `PersistedChatHistoryProvider.StoreChatHistoryAsync` then appends the turn's messages to Cosmos DB in one transactional batch, using deterministic ids so a second writer for the same session collides instead of silently duplicating.
4. **Session save.** The protocol handler calls `SaveSessionAsync`, which creates or ETag-guarded-replaces the session document with the refreshed title, message count, cumulative usage, and serialized agent state. A stale ETag (another turn finished first) surfaces as a 409 `conversation-busy`. Once the Cosmos DB write succeeds, the store also queues the turn's counts for a background writer that projects them into the SQL Server usage summary described below — never on this path itself.
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
| `Service` | `Sessions/` | `PersistedAgentSessionStore` (MAF `AgentSessionStore`), `PersistedChatHistoryProvider` (MAF `ChatHistoryProvider`), `SessionService` (the REST resource), `SessionMapper`, `SessionIdValidator`, `SessionSummaryChannel`/`SessionSummaryProcessor` (the SQL usage-reporting pipeline — see [Sessions and history](../conversations/sessions-and-history.md#session-usage-reporting)), `SessionsRecords.cs` (`SessionSummaryWork`), `SessionsExceptions.cs` |
| | `Weather/` | `WeatherService` (the deterministic stub) + `Weather/Tools/WeatherToolProvider.cs` |
| | `Agents/` | `UsageRecordingAgent.cs` — a `DelegatingAIAgent` |
| | `Caching/` | `AgentCatalogCache.cs` (`IAgentCatalogCache`), `CachingClasses.cs` (`AgentCatalog`) — the 24-hour cache the startup check and the summary writer both read |
| | `Security/`, `Options/`, `Prompts/`, `Serialization/`, `Exceptions/` | Cross-cutting: caller identity, `SessionsOptions`, the embedded prompt + loader, session-state JSON options, the two solution-wide exceptions |
| `Repository` | `Sessions/` | Store-agnostic: `Interfaces/{ISessionRepository,ISessionMessageRepository,ISessionSummaryRepository}.cs`, `SessionsRecords.cs` (adds `SessionRead`, `SessionSummaryWrite`), `SessionsExceptions.cs` (adds `SessionStoreUnavailableException`) — nothing here names a provider |
| | `Agents/` | Store-agnostic, same shape: `Interfaces/IAgentModelMappingRepository.cs`, `AgentsRecords.cs` (`ActiveAgentModel`, `TokenPrices` with `CostOf`) |
| | `Cosmos/` | Everything Cosmos-specific: `CosmosContainers`, `CosmosQueries`, `CosmosRecords.cs`, `PartitionKeys`, `CosmosPersistenceConfiguration.cs` (`AddAndesCosmosPersistence`/`AddAndesCosmosHealthCheck`), plus `HealthChecks/CosmosHealthCheck.cs`, `Options/CosmosDbOptions.cs`, `Provisioning/CosmosResourceProvisioner.cs`, `Serialization/{CosmosJsonOptions,UtcDateTimeOffsetJsonConverter}.cs`, and `Sessions/{CosmosSessionRepository,CosmosSessionMessageRepository}.cs` — the implementations of the contracts above |
| | `Sql/` | The second provider folder, EF Core end to end: `SqlPersistenceConfiguration.cs` (`AddAndesSqlPersistence`/`AddAndesSqlHealthCheck`, plus the shared `ConfigureSqlServer`), `SqlErrors.cs` (classifies a `SqlException`/`DbUpdateException` as a unique-key violation or a store-unavailable failure), `HealthChecks/SqlHealthCheck.cs`, `Provisioning/SqlSchemaMigrator.cs`, `Interceptors/AuditTimestampInterceptor.cs`, `Options/SqlDbOptions.cs`, `DbContexts/{PolicyDbContext,PolicyDbContextDesignTimeFactory}.cs`, `Configurations/{Policies/PolicyConfiguration,Agents/{AgentConfiguration,ModelConfiguration,AgentModelMappingConfiguration},Sessions/SessionSummaryConfiguration}.cs`, `Agents/SqlAgentModelMappingRepository.cs`, `Sessions/SqlSessionSummaryRepository.cs`, `Migrations/` — see [The `[Core].[Policy]` table](#the-corepolicy-table) and [The agent catalog and session usage summaries](#the-agent-catalog-and-session-usage-summaries) |
| `Entity` | `Base/BaseEntity.cs` | `Id` (`[Key]`, generated on add), `DateCreated`, `DateModified`, `RowVersion` (`[Timestamp]`) — shared by every relational entity; Cosmos documents don't use it |
| | `Sessions/SessionsRecords.cs` | `SessionDocument`, `SessionMessageDocument`, `SessionUsage`, `SessionUsageDetails`, `SessionHistoryState` — the Cosmos documents and the state-bag record the store and history provider share |
| | `Sessions/SessionSummary.cs` | The reporting entity mapped to `[Core].[Session]` |
| | `Agents/{Agent,Model,AgentModelMapping}.cs` | The agent-catalog entities mapped to `[Core.Ref]` |
| | `Policies/Policy.cs` | The insurance-policy entity, still unused above the database layer; mapped by `PolicyConfiguration` |
| `Dto` | `Actions/Sessions/`, `Sessions/`, `Pagination/` | Paging query DTOs, response DTOs, `PaginatedResponseDto<T>` |
| `Common` | `Constants/` | Every constant catalog: `AgentIds`, `AgentNames`, `AuthorizationPolicies`, `ChatClientKeys`, `ClaimTypeNames`, `PromptNames`, `RateLimitPolicies`, `SessionStateKeys`, `TelemetryNames`, `WeatherLimits` |
| | `Enums/` | `PolicyStatuses` — stored by name in `[Core].[Policy]` and enforced there by a check constraint, not by the CLR type |
| | `Validation/` | `FluentValidateOptions.cs` (the FluentValidation-to-`IValidateOptions<T>` adapter) and `OptionsValidationExtensions.cs` (`ValidateWithFluentValidation()`) — the one place `Common` takes package references |

Deliberate departures from a typical layered API, driven by the domain being small and, for the policy table, unused above the database layer so far:

- **`Repository/Cosmos/` has no `DbContexts/`, `Configurations/`, or `Migrations/`** — those exist only under `Repository/Sql/`, the one place `Microsoft.EntityFrameworkCore.*` resolves. `Repository/` stays provider-first either way: a store-agnostic `Sessions/` at the top (contracts, records, exceptions), and everything each store needs — including its own `Options/` — inside its own sibling folder.
- **The prompt is an embedded resource**, not a shipped file on disk: `Service/Prompts/weather-agent-instructions.md` is read by `IPromptTemplateLoader` through its logical resource name (`Common/Constants/PromptNames.cs`).
- **Routes are a wire contract, independent of the domain word.** The `Sessions` feature is served at `api/conversations`; the A2A and AG-UI hosts live at `weather/a2a` and `weather/ui`. "Session" is the domain word throughout the code (`SessionDocument`, `SessionService`); "conversation" appears only in the route and in `ConversationBusyException`, which mirrors the `conversation-busy` problem type a caller sees on the wire.
- **`CosmosBootstrapper` and `SqlBootstrapper` stay in `Api/Startup/`**, not in their provider folders. `Program.cs` runs `IStartupValidator.Validate()` before calling either, so a process about to fail on its own configuration never reaches out to Cosmos DB or SQL Server first; a hosted service registered from inside `Repository` would run after that guard instead of before it.
- **`Policy` has no repository, service, or endpoint above it yet.** Its database layer — mapping, migrations, health check — is complete and owner-decided to stand alone for now; nothing in `Service` or `Api` reads or writes one. `PolicyDbContext` itself is no longer policy-only: it also maps the agent catalog and the session usage summaries below, both of which the app does read and write.
- **`[Core].[Session]` is mapped by the entity `SessionSummary`**, not `Session` — `SessionDocument` and `ISessionRepository` already use that name for the Cosmos DB conversation. For the same reason, the queue that feeds it is `SessionSummaryChannel`, not `...Queue`: CA1711 fails the build on a public type name ending in `Queue`.

## Startup order

`Program.cs` sequences five phases after `WebApplication.Build()`, in this order: `IStartupValidator.Validate()` (every bound `Options` type, FluentValidation) → `EnsureCosmosResourcesAsync()` (Development-only container provisioning) → `MigrateSqlDatabaseAsync()` (Development-only migration) → `ValidateAgentCatalogAsync()` (warms the agent catalog cache and checks it against configuration) → `RunAsync()`. Validation runs first so a process about to fail on its own configuration never reaches either store; Cosmos before SQL is otherwise an arbitrary but fixed order. The catalog check runs last, after any migration that might have changed it, and fails startup — naming the agent and any deployment mismatch — before the app accepts traffic; see [The agent catalog and session usage summaries](#the-agent-catalog-and-session-usage-summaries). `/health/ready` still checks both stores — `cosmos` and `sql` — so a target environment's readiness gate isn't satisfied until whichever of the two it depends on for that check answers.

## The `[Core].[Policy]` table

`PolicyDbContext` maps the entity `Policy` to `[Core].[Policy]` — one of five entities it now maps in total; the other four are the agent catalog and session summary described [below](#the-agent-catalog-and-session-usage-summaries). The schema, the compatibility level, and the concurrency strategy are chosen the way they would be for a table this application actually served traffic through — see [ADR-0002](../adr/0002-sql-server-policy-store.md) for why. All five entities split their mapping the same way: the entity declares its column shape as attributes — `[Table]` with its schema, `[StringLength]` on every string (always `nvarchar`), `[Precision]` on decimals, and `[Key]`, `[DatabaseGenerated]` and `[Timestamp]` on `BaseEntity`, with `[Precision]` from `Microsoft.EntityFrameworkCore.Abstractions`, the only EF Core package `Entity` references — while its `IEntityTypeConfiguration<T>` under `Configurations/` holds the relationships, indexes, check constraints, value conversions and seed data.

| Column | SQL type | Notes |
|---|---|---|
| `Id` | `uniqueidentifier` | Clustered primary key; EF's client-side **sequential** GUID generator, not `Guid.CreateVersion7()` — SQL Server orders `uniqueidentifier` by its last six bytes first, so a version-7 GUID's leading timestamp bytes would scatter inserts across the clustered index instead of appending to it. |
| `PolicyNumber` | `nvarchar(32)` | Business key printed on policy documents. |
| `ProductCode` | `nvarchar(20)` | |
| `Status` | `nvarchar(16)` | `PolicyStatuses` stored by name (`HasConversion<string>()`), constrained to the enum's current members. |
| `HolderReference` | `nvarchar(64)` | The holder's id in the system of record. |
| `HolderName` | `nvarchar(200)` | Personal data — stored, never logged. |
| `EffectiveDate`, `ExpirationDate` | `date` | `DateOnly`. |
| `PremiumAmount`, `CoverageAmount` | `decimal(19,4)` | |
| `CurrencyCode` | `nvarchar(3)` | Upper-case ISO 4217; `CK_Policy_CurrencyCode` requires exactly three letters. |
| `DateCreated`, `DateModified` | `datetimeoffset` | Stamped by `AuditTimestampInterceptor` — on `Added`, a timestamp still at its default is stamped; on `Modified`, `DateModified` is stamped unless the caller already changed it from the value that was loaded, and `DateCreated` is never sent on an update. Policy code never sets either field itself, so it behaves exactly as before; the caller-supplied case exists for the session summary below, which copies the Cosmos DB session's own timestamps. The interceptor runs before `SaveChanges` detects changes, so it reads `ChangeTracker.Entries<BaseEntity>()` itself rather than trusting entity state set earlier in the pipeline. |
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

## The agent catalog and session usage summaries

`PolicyDbContext` also maps three reference tables under `[Core.Ref]` — data the application reads and never writes — and one reporting table under `[Core].[Session]`, mapped by the entity `SessionSummary`. See [ADR-0003](../adr/0003-database-owned-agent-catalog-and-session-usage.md) for why the catalog lives in the database and usage is projected into SQL Server by a background writer rather than on the turn itself.

### `[Core.Ref]` — which model each agent runs, and its price

| Table | Columns | Notes |
|---|---|---|
| `[Core.Ref].[Agent]` | `Name nvarchar(64)` | The registration name from `Common/Constants/AgentNames.cs`; unique (`IX_Agent_Name`). |
| `[Core.Ref].[Model]` | `Name nvarchar(100)`, `DeploymentName nvarchar(64)`, `ProviderName nvarchar(64)`, `InputPricePerMillionTokens`/`CachedInputPricePerMillionTokens`/`OutputPricePerMillionTokens` `decimal(19,9)` | USD list prices per million tokens; `DeploymentName` is unique (`IX_Model_DeploymentName`); `CK_Model_Prices` keeps all three `>= 0`. |
| `[Core.Ref].[AgentModelMapping]` | `AgentId`, `ModelId` FKs, `DateDeactivated datetimeoffset NULL` | A mapping's activation time is its own `DateCreated`; `CK_AgentModelMapping_ActivationWindow` requires `[DateDeactivated] IS NULL OR [DateDeactivated] >= [DateCreated]`. `IX_AgentModelMapping_AgentId` is unique and filtered on `[DateDeactivated] IS NULL`, so an agent has at most one active model at a time; `IX_AgentModelMapping_AgentId_DateCreated` covers an agent's full model history (the filtered index doesn't, since EF's FK convention ignores filters when it adds an index for the FK); `IX_AgentModelMapping_ModelId` is that FK convention index. |

All three FKs are `ON DELETE NO ACTION`, and every index name above comes from EF's own convention, not a hand-chosen one — an owner convention that also applies to `[Core].[Session]` below.

The migration that creates these tables seeds one row in each — the weather agent, `GPT 5.6 Luna` on deployment `rr-gpt-5.6-luna` (0.20 / 0.02 / 1.20 USD per million input, cached-input, and output tokens), and the active mapping between them. Agents are code: `AgentConfiguration` seeds each through `HasData`, keyed by `Common/Constants/AgentIds.cs` and named by `AgentNames.cs`, so hosting another agent means another pair of constants, a `HasData` row and a migration. The model and the mapping are data: they are hand-written `InsertData` calls in that one migration, with literal ids, and the EF model never knows them, so no later migration can update or delete what an operator has since changed. There is no `appsettings.json` section for any of this, and the application never writes it: changing a price or moving an agent to a different model is a database change made directly against `[Core.Ref]` (see the [runbook](../operations/runbook.md#changing-the-agent-catalog)).

`Service/Caching/AgentCatalogCache.cs` (`IAgentCatalogCache`) loads every active mapping, joined to its agent and model, in one query and caches the result in `IMemoryCache` for 24 hours; a failed load caches nothing, so the next read tries the store again, and `Invalidate()` forces an early reload — used by the summary writer below when it finds an agent it expects but the cached catalog doesn't have. `Api/Startup/AgentCatalogBootstrapper.cs` warms this cache at startup and refuses to start unless every agent in `AgentNames.All` has an active model whose `DeploymentName` matches `AzureOpenAI:Model`, case-insensitively — usage is attributed to and priced from the catalog's model, so a running instance and the catalog must agree on which deployment it actually calls. SQL Server is therefore a required dependency at startup, independently of `/health/ready`.

### `[Core].[Session]` — one row per session, for reporting

| Column | SQL type | Notes |
|---|---|---|
| `UserId`, `SessionId` | `uniqueidentifier` | The Entra `oid` and the protocol continuation id; `UserId` is stored for reporting and never logged. |
| `AgentId`, `ModelId` | `uniqueidentifier` FKs | The agent, and its active model when the latest counts were written. |
| `MessageCount` | `int` | |
| `InputTokens`, `CachedInputTokens`, `OutputTokens`, `ReasoningTokens`, `TotalTokens` | `bigint` | Cumulative over the session; cached tokens are already part of `InputTokens` and reasoning tokens part of `OutputTokens`, the way `Microsoft.Extensions.AI` reports them. |
| `EstimatedCost` | `decimal(19,9)` | USD; each write adds the priced delta, at the catalog's price when the write happens. |
| `DateDeleted` | `datetimeoffset NULL` | Stamped when the conversation is deleted; the row is kept as usage history and the stamp is never cleared. |
| `DateCreated`, `DateModified` | `datetimeoffset` | Copied from the Cosmos DB session document's own creation and last-message times — not from when the SQL row happened to be written. |

The unique `IX_Session_UserId_SessionId_DateCreated` gives one row per session *incarnation*: a continuation id reused after its conversation was deleted starts a new row, because its `DateCreated` differs. `IX_Session_DateCreated` serves reporting periods; the FKs get EF's own convention indexes; `CK_Session_Counts` and `CK_Session_EstimatedCost` keep every count and the cost non-negative. No title or message text is stored here — this table is for reporting, not conversation history, which stays in Cosmos DB (see [Sessions and history](../conversations/sessions-and-history.md)).

How a row actually gets written — the queue, the background writer, its merge and retry semantics, and what can be lost — is covered in [Session usage reporting](../conversations/sessions-and-history.md#session-usage-reporting), since none of it runs on the request path this page describes.
