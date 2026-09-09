# Agent hosting and protocols

## Overview

The weather agent is registered once with Microsoft Agent Framework's dependency-injection hosting (`AddAIAgent`) and exposed over **two** agent protocols from that single registration: [A2A](https://a2a-protocol.org/latest/) for agent-to-agent and programmatic callers, and [AG-UI](https://learn.microsoft.com/agent-framework/integrations/by-component/ui/ag-ui/) for browser-based chat UIs. Both protocols share the same chat client, tools, prompt, session store, and telemetry — a caller's choice of protocol only changes how a turn gets in and how the response streams back out.

This page covers both protocol bindings, the agent card, the pipeline a turn runs through, the weather tools, and what gets recorded to telemetry. For the Cosmos DB storage those turns read and write, see [Sessions and history](../conversations/sessions-and-history.md).

## A2A

Registered by `.AddA2AServer()` on the hosted-agent builder and mapped in `AgentEndpoints.MapAgentEndpoints`. Both bindings serve `weather/a2a` and require the `AgentAccess` authorization policy plus the `AgentTurns` rate-limit policy:

- **HTTP+JSON** (`MapA2AHttpJson`) — REST-style, with method-specific paths under `weather/a2a`.
- **JSON-RPC 2.0** (`MapA2AJsonRpc`) — a single `POST weather/a2a`, method named in the request body (`message/send`, `message/stream`, …).

A2A's conversation identifier is `contextId`. A client either supplies one to continue a conversation, or omits it to start one — the host assigns a new id, which becomes the Cosmos DB `sessionId` the first time the turn is saved (see [Sessions and history](../conversations/sessions-and-history.md#session-id-validation) for what makes an id valid).

### Agent card

`GET /.well-known/agent-card.json` is **anonymous** — a client needs to discover how to authenticate before it can obtain a token — and is built once at startup by `WeatherAgentCard.Create`. It declares:

- Name, description, and version (`AgentCard:Version`, default `1.0.0`).
- `Capabilities.Streaming = true`.
- Both A2A interfaces (`HttpJson` and `JsonRpc`), resolved against `AgentCard:PublicBaseUrl`.
- One HTTP bearer security scheme (`entra-bearer`) and a security requirement referencing it — the card tells a client it needs an Entra ID access token, not what tenant or scope to request.
- One `AgentSkill` per capability the agent actually offers: **current weather** and **daily forecast**, each with a short example prompt.

## AG-UI

Registered by `services.AddAGUIServer()` and mapped at `weather/ui` (`MapAGUIServer`), also behind `AgentAccess` and `AgentTurns`. AG-UI's conversation identifier is `threadId`, playing the same role `contextId` plays for A2A: omit it to start a new conversation, or send back a previous one to continue it. A run also carries a `runId`; continuing a specific run uses `parentRunId`. Because the weather agent is registered with a hosted session store (see below), `MapAGUIServer` uses `threadId` alone to select the persisted Cosmos DB session — a client doesn't need to resend prior turns' messages.

The response is a server-sent-events stream of AG-UI's typed events (`RUN_STARTED`, `TEXT_MESSAGE_CONTENT`, `RUN_FINISHED`, `RUN_ERROR`, …); see the [runbook](../operations/runbook.md#smoke-test) for a worked request/response example.

## Agent pipeline

`AgentsConfiguration.CreateWeatherAgent` composes two nested pipelines, outermost to innermost. A turn enters the agent-level pipeline; the third stage, `ChatClientAgent`, in turn drives its own chat-client-level pipeline to actually call the model:

```
OpenTelemetryAgent            opens the invoke_agent span            Api/Configuration/AgentsConfiguration.cs (.AsBuilder())
  UsageRecordingAgent         folds token usage into the session     Service/Agents/UsageRecordingAgent.cs
    ChatClientAgent           prompt + tools + history provider — invokes the chat client below
      FunctionInvokingChatClient   runs the tool-call loop           Api/Configuration/Providers/AzureOpenAIProviderConfiguration.cs
        OpenTelemetryChatClient    opens the chat span               (both wrap the keyed "azure-openai" IChatClient)
          Azure AI Foundry Responses API client   the actual model call
```

**The chat client** (`ChatClientKeys.AzureOpenAI`) reaches the Microsoft Azure AI Foundry deployment `AzureOpenAI:Model` through the OpenAI SDK's Responses client on the resource's OpenAI-compatible `/openai/v1/` route, using an API key. It's built with `AsIChatClientWithStoredOutputDisabled` — `store:false` on every Responses API call — because `ChatClientAgent` refuses to pair a service-stateful client with a `ChatHistoryProvider` (`ThrowOnChatHistoryProviderConflict` defaults to `true`), and Cosmos DB, not the model provider, is this application's single source of truth for conversation state. See [ADR-0001](../adr/0001-preview-hosting-packages.md) for why the hosting packages this depends on are pinned to an exact preview build.

**A second keyed client, `ChatClientKeys.MicrosoftFoundry`**, reaches a Chat Completions deployment on the same resource and `/openai/v1/` route through `MicrosoftFoundry:*` configuration — same shared base options (`Api/Options/OpenAIEndpointOptions.cs`), same `Api/Configuration/Providers/OpenAIChatClientFactory.cs` that applies function invocation and OpenTelemetry. It's optional (a blank `MicrosoftFoundry:Endpoint` registers nothing) and nothing in this solution calls it yet — it exists for a future service that needs a plain model call rather than an agent turn. See [Configuration](../operations/configuration.md) for both providers' keys.

**The agent** (`ChatClientAgent`) is configured with `UseProvidedChatClientAsIs = true` — the keyed client already carries function invocation and OpenTelemetry, so the agent doesn't wrap them a second time. Its `ChatHistoryProvider` is `CosmosChatHistoryProvider`, and its `ChatOptions.Tools` come from `WeatherToolProvider.CreateTools()`.

**The wrappers** around `ChatClientAgent` add cross-cutting behavior without touching the agent itself: `OpenTelemetryAgent` opens the `invoke_agent` span, and `UsageRecordingAgent` (a `DelegatingAIAgent`) reads each response's token usage — from `AgentResponse.Usage` on a non-streaming run, or accumulated from `UsageContent` on a streaming one — and folds it into the session's `SessionUsage` running total, logging the same figures structurally. It runs in a `finally` block on the streaming path, so a turn the client abandoned mid-stream is still recorded and billed for.

## Tools

`WeatherToolProvider.CreateTools()` exposes three function tools, backed by `IWeatherService` (a deterministic in-process stub — see below):

| Tool | Purpose | Notable arguments |
|---|---|---|
| `search_location` | Resolve a free-text place name to coordinates. Must be called before either weather tool. | `query` — a city name, optionally with country (`"Paris"` or `"Paris, France"`). |
| `get_current_weather` | Current conditions at a coordinate. | `locationName`, `latitude`, `longitude` — from a prior `search_location` result. |
| `get_daily_forecast` | Day-by-day forecast, today included. | Same coordinate arguments, plus `days` (1–7, `WeatherLimits.MaxForecastDays`). |

Expected failures — a blank query, no matching place, an out-of-range coordinate or day count — come back as a `ToolError` result rather than a thrown exception, so the model can read what went wrong and correct its next call instead of the whole turn failing.

### The weather data is synthetic

`WeatherService` is a deterministic stub, not a live provider: a built-in gazetteer of 57 well-known cities answers `search_location` (case- and accent-insensitive, so "Sao Paulo" matches "São Paulo"); any other query is a miss, and the tool reports it as one rather than inventing a place. Conditions and forecasts for a resolved coordinate are computed from a hash of `(latitude, longitude, local calendar date)`, so the same question about the same place on the same day always gets the same numbers — follow-up questions within a conversation stay internally consistent — but nothing here reflects real weather. Swapping in a live provider later means implementing `IWeatherService` again; the tool surface and prompt don't change.

## Prompt

The system prompt is `Service/Prompts/weather-agent-instructions.md`, shipped as an **embedded resource** (`Common/Constants/PromptNames.cs` names its logical resource path) rather than a file read from disk at runtime, and loaded once per process by `IPromptTemplateLoader`. It tells the model to resolve a place with `search_location` before calling either weather tool, to ask before guessing when several candidates match, to report temperatures in both Celsius and Fahrenheit, and to never invent a reading or a place the tools didn't return.

## Telemetry

When `Telemetry:ConnectionString` is set, both the agent layer and the chat-client layer are instrumented under one activity source and meter name (`TelemetryNames.Source = "Andes.Agents"`), producing nested spans for a single turn:

```
invoke_agent weather-agent
  chat rrp-gpt-5.6-luna
    execute_tool search_location
    execute_tool get_current_weather
```

`gen_ai.client.token.usage` is recorded as a histogram alongside the spans; the same figures are also persisted per session and returned by `GET api/conversations/{id}` (see [Sessions and history](../conversations/sessions-and-history.md)), so usage is visible both in Application Insights and in the API. Prompts, completions, and tool arguments are recorded on spans only when `Telemetry:EnableSensitiveData` is `true` (or the environment variable `OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT=true` is set) — off by default in **every** environment, Development included, since a span is not somewhere conversation content belongs without an explicit opt-in. Full configuration is in [Configuration](../operations/configuration.md).

## Known limitations

- **A2A's task store is in-memory.** `tasks/get` and `tasks/cancel` only see tasks created on the same process, so a multi-instance deployment needs sticky sessions (or a future Cosmos DB–backed `ITaskStore`) until then.
- **Concurrent turns on one session are unsupported by design.** The second of two simultaneous turns on the same `contextId`/`threadId` gets a 409 `conversation-busy` rather than being queued or merged (see [Sessions and history](../conversations/sessions-and-history.md#conflict-and-heal-semantics)).
