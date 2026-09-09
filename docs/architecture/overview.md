# Architecture overview

## Overview

Andes Agents is an ASP.NET Core (.NET 10) host for a single hosted [Microsoft Agent Framework](https://learn.microsoft.com/agent-framework/) agent — the weather agent. It answers current-conditions and short-range-forecast questions over two agent protocols (A2A and AG-UI), persists every conversation to Azure Cosmos DB so a caller can resume it later, and exposes that history as a small REST resource. Every route requires a Microsoft Entra ID bearer token except agent discovery and health checks.

Read this page first for the shape of the solution. [Sessions and history](../conversations/sessions-and-history.md) and [Hosting and protocols](../agent/hosting-and-protocols.md) go deep on the two halves of a request; [Configuration](../operations/configuration.md) and the [Runbook](../operations/runbook.md) cover running it.

## Solution layout

The solution is `weather-agent/Andes.Agents/Andes.Agents.slnx`, six projects layered `Api → Service → Repository → Entity → Common`, with `Dto → Common` and both `Service` and `Repository` depending on `Dto`:

```
Andes.Agents.Api            ASP.NET Core host: endpoints, DI wiring, middleware
      │
Andes.Agents.Service         agent pipeline, session store/history provider, weather tools
      │
Andes.Agents.Repository      Cosmos DB access
      │           ╲
Andes.Agents.Entity  ╲       Cosmos document and state-bag records
      │                ╲
Andes.Agents.Common      Andes.Agents.Dto
(constants only)         (request/response contracts)
```

`Entity` never references `Dto`; `Common` references nothing. There's no `Abstractions` project and no controllers — every interface lives beside its one implementation (`ISessionRepository` and `SessionRepository` share a file), and every endpoint is a minimal-API module.

`Directory.Build.props` and `Directory.Packages.props`, beside the `.slnx`, hold settings and package versions shared by all six projects: central package management, `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`, and `GenerateDocumentationFile` (which also turns on `CS1591`, so an undocumented public member fails the build). There is no relational database and no EF Core anywhere in the solution — the only store is Azure Cosmos DB through the raw `Microsoft.Azure.Cosmos` SDK.

## Request flow

A turn of the weather agent — whether it arrives over A2A or AG-UI — moves through the same pipeline:

1. **Authentication and rate limiting.** The bearer token is validated (Microsoft.Identity.Web); `HttpContext.User` carries the caller's Entra object id (`oid`). The rate limiter partitions on that id.
2. **Session lookup.** The protocol handler resolves the hosted agent and calls `CosmosAgentSessionStore.GetSessionAsync(agent, continuationId)` — the A2A `contextId` or the AG-UI `threadId`. The store validates the id, point-reads the caller's session document, and either deserializes it (recording its Cosmos ETag) or creates a fresh session and seeds its history state.
3. **The agent runs.** `agent.RunStreamingAsync(...)` flows through, in order: `OpenTelemetryAgent` (opens the `invoke_agent` span) → `UsageRecordingAgent` (wraps the call to fold token usage into the session) → the inner `ChatClientAgent`. Before the model is called, `CosmosChatHistoryProvider.ProvideChatHistoryAsync` loads and replays the session's most recent messages. The chat client pipeline is `FunctionInvokingChatClient` → `OpenTelemetryChatClient` → the Azure AI Foundry Responses API client, with `store:false` so Cosmos DB is the only place conversation state lives. Tools run in-process. `CosmosChatHistoryProvider.StoreChatHistoryAsync` then appends the turn's messages to Cosmos DB in one transactional batch, using deterministic ids so a second writer for the same session collides instead of silently duplicating.
4. **Session save.** The protocol handler calls `SaveSessionAsync`, which creates or ETag-guarded-replaces the session document with the refreshed title, message count, cumulative usage, and serialized agent state. A stale ETag (another turn finished first) surfaces as a 409 `conversation-busy`.
5. **Conversation history.** `GET api/conversations` and its children read the same Cosmos DB documents directly, scoped to the caller's own `oid` — independent of the agent pipeline.

See [Sessions and history](../conversations/sessions-and-history.md) for the document shapes and conflict semantics, and [Hosting and protocols](../agent/hosting-and-protocols.md) for the protocol bindings and the agent pipeline in more detail.

## Where things live

| Project | Folder | Holds |
|---|---|---|
| `Api` | `Endpoints/` | `AgentEndpoints.cs` (A2A + AG-UI + the agent card), `SessionEndpoints.cs` (`api/conversations`), `WeatherAgentCard.cs` |
| | `Configuration/` | One `Add<Feature>` per feature (`AgentsConfiguration`, `AuthenticationConfiguration`, `CorsConfiguration`, `PersistenceConfiguration`, `SessionsConfiguration`, `WeatherConfiguration`, …) and `Configuration/Providers/` (`MicrosoftFoundryProviderConfiguration.cs`, `AzureOpenAIProviderConfiguration.cs`, `OpenAIChatClientFactory.cs`) |
| | `ExceptionHandlers/` | `GlobalExceptionHandler.cs` — the only `IExceptionHandler`, registered by `ExceptionHandlingConfiguration` |
| | `Health/` | `CosmosHealthCheck.cs`, `HealthRegistration.cs` |
| | `Middleware/` | `RequestLoggingMiddleware.cs`, `RequestLoggingRegistration.cs` |
| | `Observability/` | `TelemetryRegistration.cs`, `RequestDescriptor.cs` — names a request by its matched route pattern for logging |
| | `Options/` | Api-only `<Area>Options` classes (`OpenAIEndpointOptions` base with `MicrosoftFoundryOptions` and `AzureOpenAIOptions`, `AzureAdOptions`, `CosmosDbOptions`, `AgentCardOptions`, `TelemetryOptions`, `RequestLoggingOptions`, `RateLimitingOptions`, `CorsOptions`, `KeyVaultOptions`, `AzureOptions`, `ApiDocsOptions`) |
| | `Problems/` | `ProblemTypes.cs`, `ProblemDetailsRegistration.cs` (adds the `traceId`/`requestId` extensions), `ProblemResponseWriter.cs` |
| | `Startup/` | `CosmosBootstrapper.cs` — dev-only container provisioning after `Build()` |
| `Service` | `Sessions/` | `CosmosAgentSessionStore` (MAF `AgentSessionStore`), `CosmosChatHistoryProvider` (MAF `ChatHistoryProvider`), `SessionService` (the REST resource), `SessionMapper`, `SessionIdValidator`, `SessionsExceptions.cs` |
| | `Weather/` | `WeatherService` (the deterministic stub) + `Weather/Tools/WeatherToolProvider.cs` |
| | `Agents/` | `UsageRecordingAgent.cs` — a `DelegatingAIAgent` |
| | `Security/`, `Options/`, `Prompts/`, `Serialization/`, `Exceptions/` | Cross-cutting: caller identity, `SessionsOptions`, the embedded prompt + loader, session-state JSON options, the two solution-wide exceptions |
| `Repository` | `Cosmos/` | `CosmosContainers`, `CosmosQueries` (shared query/point-read helpers), `CosmosRecords.cs`, `CosmosResourceProvisioner`, `PartitionKeys` |
| | `Sessions/` | `SessionRepository`, `SessionMessageRepository`, `SessionsRecords.cs`, `SessionsExceptions.cs` |
| | `Serialization/` | `RepositoryJsonOptions`, `UtcDateTimeOffsetJsonConverter` |
| `Entity` | `Sessions/SessionsRecords.cs` | `SessionDocument`, `SessionMessageDocument`, `SessionUsage`, `SessionHistoryState` — the Cosmos documents and the state-bag record the store and history provider share |
| `Dto` | `Actions/Sessions/`, `Sessions/`, `Pagination/` | Paging query DTOs, response DTOs, `PaginatedResponseDto<T>` |
| `Common` | `Constants/` | Every constant catalog: `AgentNames`, `AuthorizationPolicies`, `ChatClientKeys`, `ClaimTypeNames`, `PromptNames`, `RateLimitPolicies`, `SessionIdRules`, `SessionStateKeys`, `TelemetryNames`, `WeatherLimits` |

Three deliberate departures from a typical layered API, all because the only store is Cosmos DB and the domain is small:

- **No EF Core, so no `DbContexts/`, `Configurations/`, or `Migrations/`.** `Repository/` instead holds `Cosmos/`, `Sessions/`, and `Serialization/`; the `CosmosContainerNames` record it needs comes from `Api/Options/CosmosDbOptions.cs` via `AddAndesPersistence`, so `Repository/` itself holds no options class.
- **The prompt is an embedded resource**, not a shipped file on disk: `Service/Prompts/weather-agent-instructions.md` is read by `IPromptTemplateLoader` through its logical resource name (`Common/Constants/PromptNames.cs`).
- **Routes are a wire contract, independent of the domain word.** The `Sessions` feature is served at `api/conversations`; the A2A and AG-UI hosts live at `weather/a2a` and `weather/ui`. "Session" is the domain word throughout the code (`SessionDocument`, `SessionService`); "conversation" appears only in the route and in `ConversationBusyException`, which mirrors the `conversation-busy` problem type a caller sees on the wire.
