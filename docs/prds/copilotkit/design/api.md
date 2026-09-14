# Design A: API hardening and history

The API work the chat depends on — proposed epic 1 of the [CopilotKit chat PRD](../prd.md). Nothing here is implemented yet. It follows `.claude/rules/api-architecture.md` and `.claude/rules/csharp.md`; sources are in [sources.md](../sources.md).

## Why

The chat connects the browser straight to `POST /weather/ui`, so the API is the only server-side filter between a user's browser and the model. Four gaps stand in the way today:

| Today | Consequence | This design |
| --- | --- | --- |
| The AG-UI host stores every message the client sends and passes client tools to the model | Prompt injection is persisted into the conversation; resending history duplicates it | [A1](#a1-ag-ui-input-guard): an input guard |
| `api/conversations/{id}/messages` returns text only | A reopened conversation can't redraw its weather widgets | [A2](#a2-tool-calls-and-results-on-stored-messages): tool calls and results on messages |
| Sessions are listed by creation time | A continued old conversation stays buried | [A3](#a3-most-recent-activity-first): order by last activity |
| A 400/401/429 has no body for `Accept: text/event-stream`; `Retry-After` isn't readable cross-origin; malformed JSON is a 500 in Development | The chat can't explain failures | [A4](#a4-errors-the-browser-can-read) |

## A1. AG-UI input guard

### Behavior

| Input | Result |
| --- | --- |
| `threadId` blank | Accepted; the host issues an N-form GUID |
| `threadId` a `D` or `N` GUID | Accepted |
| `threadId` anything else | 400, `errors.threadId` |
| `messages` empty, or the last message isn't `user` | 400, `errors.messages` |
| Last user message isn't text (string, or text parts only) | 400, `errors["messages.content"]` |
| Last user message blank, or longer than 4,000 UTF-16 code units | 400, `errors["messages.content"]` |
| Earlier messages (any role) | **Ignored** — the server owns history, and CopilotKit-style clients resend it |
| `tools`, `context` or `resume` non-empty | 400, `errors.tools` / `errors.context` / `errors.resume` |
| `state` or `forwardedProps` anything but null, `{}` or `[]` | 400, `errors.state` / `errors.forwardedProps` |

An accepted request reaches the host as a fresh `RunAgentInput` holding `ThreadId`, `RunId`, `ParentRunId` and one new `AGUIUserMessage` with the caller's content — without the client's message id, which is unbounded caller text that would otherwise be persisted. Nothing a caller supplied is logged: `GlobalExceptionHandler` already logs only method, route, status and exception type.

### Files

| File | Change |
| --- | --- |
| `Api/Filters/AGUIInputEndpointFilter.cs` (new) | Static `Require()` returning an endpoint-filter **factory** |
| `Api/Endpoints/AgentEndpoints.cs` | `MapAGUIServer(...)…AddEndpointFilterFactory(AGUIInputEndpointFilter.Require())` |
| `Common/Constants/AgentTurnLimits.cs` (new) | `MaxUserMessageLength = 4000` — a wire contract the chat input mirrors |
| `Service/Sessions/SessionIdValidator.cs` | Extract a public `IsValid(string)`; `EnsureValid` calls it |
| `Directory.Packages.props`, `Api/Andes.Agents.Api.csproj` | Reference `AGUI.Abstractions` 0.0.5 explicitly, since Api now names its types; a hosting bump that needs another version then fails restore instead of drifting |

### Why a filter factory works here

`MapAGUIServer` returns an `IEndpointConventionBuilder`, and the pinned hosting package maps a generated route handler that binds `[FromBody] RunAgentInput?`. Endpoint filters run for such handlers, and `EndpointFilterInvocationContext.Arguments` is writable by design. A factory finds the `RunAgentInput` parameter once, when the endpoint is built — and fails the build if a hosting upgrade stops binding it, instead of silently serving an unguarded route.

```csharp
// Design sketch — the rules live in the nested validator described below.
public static Func<EndpointFilterFactoryContext, EndpointFilterDelegate, EndpointFilterDelegate> Require() =>
    (factoryContext, next) =>
    {
        int index = Array.FindIndex(factoryContext.MethodInfo.GetParameters(), parameter => parameter.ParameterType == typeof(RunAgentInput));

        if (index < 0)
        {
            throw new InvalidOperationException("The AG-UI handler no longer binds RunAgentInput, so its input cannot be guarded.");
        }

        return invocationContext =>
        {
            RunAgentInput? input = invocationContext.GetArgument<RunAgentInput?>(index);
            ValidationResult result = _validator.Validate(TrailingTurn.From(input));

            if (!result.IsValid)
            {
                throw new ValidationException(result.Errors);
            }

            invocationContext.Arguments[index] = Normalize(input!);

            return next(invocationContext);
        };
    };
```

**Validation placement.** The rules validate a private nested record (`TrailingTurn`: thread id, last message, extracted text, and whether each rejected field is non-empty) with a private nested `AbstractValidator`. A third-party wire type has no file of its own, so nesting keeps "the validator lives with the type it validates" literally true. This is a **sanctioned deviation** to record in `.claude/CLAUDE.md`. The validator isn't registered in DI because it validates a private type.

## A2. Tool calls and results on stored messages

Every stored message already keeps the full serialized `ChatMessage`; only the DTO drops it. The change is additive — existing properties keep their order.

| File | Change |
| --- | --- |
| `Dto/Sessions/SessionMessageDto.cs` | Trailing `IReadOnlyList<SessionToolCallDto> ToolCalls`, `IReadOnlyList<SessionToolResultDto> ToolResults` |
| `Dto/Sessions/SessionToolCallDto.cs` (new) | `Id`, `Function`, computed `Type => "function"` |
| `Dto/Sessions/SessionToolCallFunctionDto.cs` (new) | `Name`, `Arguments` (compact JSON string) |
| `Dto/Sessions/SessionToolResultDto.cs` (new) | `ToolCallId`, `Content` |
| `Service/Sessions/SessionMapper.cs` | Deserialize the stored `ChatMessage` with `AIJsonUtilities.DefaultOptions` and project the contents |
| `Api/Endpoints/SessionEndpoints.cs` | Summary: "…in order, with their tool calls and results." |

The shape mirrors AG-UI's own message types, so the chat maps it one to one:

```json
{
  "items": [
    { "id": "…:00000001", "sequence": 1, "role": "user", "text": "What's the weather in Seattle right now?",
      "dateCreated": "…", "toolCalls": [], "toolResults": [] },
    { "id": "…:00000002", "sequence": 2, "role": "assistant", "text": null, "dateCreated": "…",
      "toolCalls": [ { "id": "call_1", "type": "function",
                       "function": { "name": "search_location", "arguments": "{\"query\":\"Seattle\"}" } } ],
      "toolResults": [] },
    { "id": "…:00000003", "sequence": 3, "role": "tool", "text": null, "dateCreated": "…",
      "toolCalls": [],
      "toolResults": [ { "toolCallId": "call_1", "content": "[{\"name\":\"Seattle\",\"country\":\"United States\",…}]" } ] }
  ],
  "skip": 0, "take": 50, "totalCount": 3
}
```

**Mapping rules**

- `FunctionCallContent` → a tool call; arguments serialized compactly (`WriteIndented = false`), `{}` when null.
- `FunctionResultContent` → a tool result, with `content` matching what AGUI.Server streams in `TOOL_CALL_RESULT`: a string as-is, a `JsonElement` as its raw text, a JSON-string element unwrapped (so a failed call's text isn't double-quoted), anything else serialized compactly.
- Reasoning and encrypted content are never projected; `text` stays the message's text content.
- On the client, an assistant item becomes `{ role: "assistant", content: text, toolCalls }`, and each tool result becomes a `tool` message with id `${item.id}:${toolCallId}`.

## A3. Most recent activity first

| File | Change |
| --- | --- |
| `Repository/Cosmos/Sessions/CosmosSessionRepository.cs` | `SELECT * FROM c ORDER BY c._ts DESC OFFSET @skip LIMIT @take` |
| `Repository/Sessions/Interfaces/ISessionRepository.cs`, `Service/Sessions/SessionService.cs`, `Api/Endpoints/SessionEndpoints.cs` | Summaries say "most recently active first" |

**Why `_ts`.** Every Cosmos DB document has `_ts`, it is always indexed, and every save — one per turn — bumps it. `dateModified` is missing from documents saved before its rename, and an `ORDER BY` over a property some items lack can leave them out of the results. No indexing change is needed.

## A4. Errors the browser can read

| File | Change |
| --- | --- |
| `Api/Problems/FallbackProblemDetailsWriter.cs` (new), registered after `AddProblemDetails` in `ProblemDetailsRegistration.cs` | An `IProblemDetailsWriter` that runs only when the default writer declines, writing `application/problem+json` regardless of `Accept` — covering the exception handler, the 429 limiter and 401 status pages. `ProblemResponseWriter`'s `HasStarted` guard stays, so a stream that already started still gets nothing |
| `Api/ExceptionHandlers/GlobalExceptionHandler.cs` | A `BadHttpRequestException` arm mapping to its status code as `validation-error`, so malformed JSON isn't a 500 in Development |
| `Api/Configuration/CorsConfiguration.cs` | `.WithExposedHeaders("Retry-After")` |

**CORS configuration.** The browser now calls `/weather/ui` (POST with an SSE response) and `api/conversations` directly, so `Cors:AllowedOrigins` must list every origin that serves the app. The API must be reached over HTTPS: `UseHttpsRedirection` runs before `UseCors`, and a redirected preflight fails.

## A5. Docs to update when this ships

- `docs/agent/hosting-and-protocols.md` — the server-owned history contract, client-minted thread ids, browsers calling the endpoint directly.
- `docs/conversations/sessions-and-history.md` — ordering and a `toolCalls` example.
- `docs/operations/runbook.md` — a mid-stream failure drops the connection (no `RUN_ERROR` on the pinned build); replace references to the nonexistent `appsettings.Development.json` with user-secrets and environment-variable settings (also in `configuration.md`); fix `weather-agent/Andes.Agents` paths to `agents-api`.
- `docs/architecture/overview.md` — where the new files live.
- `.claude/CLAUDE.md` — invariants for the guard, `_ts` ordering and the fallback writer, plus the nested-validator deviation.
- ADR-0005 — server-owned AG-UI history (see [decisions.md](../decisions.md)).

## A6. Verification

- Gate [GATE-API](../verification.md#gates).
- Scratch file-based apps, per this repository's practice for framework-level behavior: the filter on `MapAGUIServer` over a stub chat client; a `SessionMapper` round trip; a Cosmos emulator comparison of `ORDER BY dateModified` and `_ts` with a document lacking `dateModified`.
- The smoke matrix [API-1 to API-13](../verification.md#api-acceptance-checks).
- Review by the `csharp-code-reviewer` agent, at most two rounds.
- A .NET unit-test project (`agents-api/tests/Andes.Agents.Unit.Test`, xUnit v3 with NSubstitute) follows as a separate story.

## A7. Risks

| Risk | Mitigation |
| --- | --- |
| A hosting upgrade maps a raw `RequestDelegate`, so filters silently stop running | [API-1](../verification.md#api-acceptance-checks) joins the upgrade checklist of ADR-0001 |
| Retry, regenerate and edit flows resend an already-stored user message, which is stored again | The chat hides regenerate and edit |
| The full request body is deserialized before the guard runs | Consider a per-route body size limit |
| `_ts` has one-second resolution, so offset pages can shift while sessions update | Accept for a personal session list |
| AGUI.Server writes nothing during a silent tool phase; Azure ingress closes idle requests at about 240 s | The weather tools are fast; revisit for long-running tools ([SPK-9](../verification.md#spikes)) |
