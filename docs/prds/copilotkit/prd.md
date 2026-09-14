# PRD: CopilotKit Chat for the Weather Agent

## 1. Overview

**Problem**: `agents-api/` already serves the weather agent over AG-UI at `POST /weather/ui` and exposes its history through `api/conversations`, but neither is safe or complete enough for a production browser chat yet. The endpoint stores whatever role a client sends and accepts client-declared tools and state — the injection surface Microsoft's AG-UI security guidance calls out for a server exposed directly to browsers — drops the connection with no `RUN_ERROR` once a stream has started, and `api/conversations` returns text-only history, so a reopened session can't re-render the tool-call widgets it produced. `agents-ui/` has no chat surface at all: an app shell, one lazy home page, and (as of this branch) automatic Entra ID sign-in, but nothing that talks to the agent.

**Solution**: Harden `/weather/ui` and `api/conversations` with a server-side input guard, tool-call-aware message history, and clearer error surfacing (epic 1), then build a route-scoped chat page in `agents-ui/` on CopilotKit's prebuilt Angular chat (`@copilotkit/angular` 0.5.2), connected straight to the hardened API with no Node runtime in between, restyled onto the existing Bootstrap/Andes tokens and extended only through its slots (epic 2). This document is a pre-implementation specification: no application code ships as part of authoring it. Designs A and B (linked below) carry the file-level detail a later implementation effort will follow.

**Success criteria**:
- All 13 API smoke checks (`API-1`–`API-13` in [verification.md](./verification.md)) pass against the hardened `/weather/ui` and `api/conversations`, confirmed by running the smoke matrix once epic 1 lands.
- The Angular initial bundle stays at or below the existing 900 kB warning / 1 MB error budget, growing by at most 1 kB over today's 887.57 kB raw baseline, measured by `npm run build` and `npm run check:initial-chunk` (`GATE-UI`).
- Zero caller-supplied text, prompts, or query values appear in API request logs across the full smoke matrix, verified by grepping logs after `API-13`.
- The chat surface meets WCAG 2.2 AA contrast (text ≥ 4.5:1, UI components ≥ 3:1) in both light and dark themes at 360, 768 and 1280 px, verified by `UI-THEME`.
- The Markdown XSS corpus (script tags, event-handler attributes, `javascript:`/`data:` links, a remote image, and a DOM-clobbering `id`/`name`) renders with zero forbidden nodes and opens no dialog, verified by `UI-XSS`.

A signed-in user opens the app, selects "Start chatting," and lands on a fresh session. They ask about the weather in a city; the reply streams in as formatted Markdown, and a tool call resolves into a Bootstrap weather card instead of raw JSON. They can stop a reply mid-stream, come back later and see the same session — text and widgets both — at the top of a sessions list ordered by recent activity, and delete it when they're done. Throughout, the only thing that changed on the wire is that the browser now talks to `agents-api` directly, under a request guard that limits what a browser client can ever send it.

See [README.md](./README.md) for the glossary and reading order, [learn/01-ag-ui.md](./learn/01-ag-ui.md) and [learn/02-copilotkit.md](./learn/02-copilotkit.md) for the underlying protocols, and [sources.md](./sources.md) for every version and license claim this document relies on.

## 2. Goals & non-goals

**Goals**:
- Give the weather agent a production-ready browser chat inside `agents-ui/`, reusing CopilotKit's prebuilt Angular chat rather than building a bespoke transcript UI.
- Close the AG-UI input-injection surface before `/weather/ui` is exposed directly to browser traffic: only a trailing user text message reaches the agent.
- Let a user keep, resume, and browse their own sessions with tool-call widgets intact after a reload, ordered by recent activity.
- Establish a reusable pattern — a transport subclass over `HttpAgent`, a tool-renderer registration mechanism, and a server-side input guard — this repository and other projects built the same way can reuse for a future agent.
- Keep the whole design inside `agents-ui`'s existing Bootstrap/Andes theming and bundle-budget constraints.

**Non-goals**:
- No application code ships from this PRD. It is a pre-implementation specification; [design/api.md](./design/api.md) and [design/chat-ui.md](./design/chat-ui.md) carry the file-level detail for the later implementation of epics 1 and 2.
- A2UI (declarative, agent-driven generative UI surfaces) is out of scope; tracked separately in [../a2ui/prd.md](../a2ui/prd.md) and [learn/03-a2ui.md](./learn/03-a2ui.md).
- No CopilotKit Runtime or other Node server. The browser calls `agents-api` directly.
- No frontend (client-declared) tools, shared state, or human-in-the-loop interrupts — the input guard rejects all three by design.
- No changes to the A2A protocol surface, the SQL Server policy table, or the agent catalog.
- No feature flag or staged-rollout mechanism is introduced.
- No new client-side analytics or telemetry beyond the existing server-side `gen_ai.*` spans; prompt capture on those spans stays off.
- A .NET automated test project is scoped as a P2 follow-up (`US-303`), not a condition of the initial release.

## 3. Users & access

**Personas**:
- **Authenticated tenant user**: a person already signed in through the existing Entra ID app registration ([../../ui/authentication.md](../../ui/authentication.md)) who wants to ask the weather agent questions, read answers as formatted text and widgets, and return to earlier sessions.
- **Repository owner/maintainer**: new to AG-UI, CopilotKit and A2UI; uses the `learn/` docs to evaluate the integration and owns the accepted risks recorded in [decisions.md](./decisions.md) (the CopilotKit license caveat, direct browser exposure).
- **Future feature team**: a developer adding a next agent, in this repository or another project built the same way, who reuses the guard, transport-subclass, and tool-renderer patterns this design establishes.

**Role-based access**:
- **Authenticated caller** is the only role: any caller granted the shared app registration's `access_as_user` scope can start a session, send messages, stop an in-flight run, and list, resume, or delete only sessions whose Cosmos DB document `userId` equals their own Entra `oid`. Every route requires the existing `AgentAccess` authorization policy; `/weather/ui` also carries the `AgentTurns` rate limit (30 turns/min per `oid`), while `api/conversations` is unmetered. A session id that exists but belongs to another caller, or doesn't exist at all, returns 404 — never 403 — matching `api/conversations`'s existing behavior today.
- No anonymous access exists to any chat surface. `/.well-known/agent-card.json` remains the only anonymous route and is unchanged by this work.

## 4. Functional requirements

| ID | Requirement | Priority | Epic(s) |
| --- | --- | --- | --- |
| FR-1 | The API rejects a turn whose last message isn't user-authored text, or that carries non-empty `tools`, `context`, `resume`, `state` or `forwardedProps`, with 400 `validation-error` | P0 | EP-1 |
| FR-2 | The API enforces a 4,000-character cap on the trailing user message and accepts only `D`/`N`-form GUID thread ids | P0 | EP-1 |
| FR-3 | The API forwards only the trailing user message of a valid turn to the agent — earlier messages are ignored because the server owns history — and issues a thread id when the client omits one | P0 | EP-1 |
| FR-4 | Malformed or empty request bodies return a 400 problem+json response instead of an unhandled 500 | P1 | EP-1 |
| FR-5 | Problem+json is written for guard, auth and rate-limit failures even when the client negotiated `Accept: text/event-stream` | P1 | EP-1 |
| FR-6 | CORS allows the configured SPA origins to call `/weather/ui` and `api/conversations` directly, cross-origin, and exposes the `Retry-After` header | P0 | EP-1 |
| FR-7 | The messages API returns the tool calls and tool results each stored message already holds, not just its text | P0 | EP-1, EP-2 |
| FR-8 | `api/conversations` lists a caller's sessions most-recent-activity-first | P0 | EP-1, EP-2 |
| FR-9 | A user can start a new session with a client-minted lowercase GUID thread id | P0 | EP-2 |
| FR-10 | A user can send a message and watch the assistant's reply stream in as sanitized Markdown | P0 | EP-2 |
| FR-11 | Rendered Markdown is sanitized against XSS and never fetches remote images | P0 | EP-2 |
| FR-12 | The composer enforces the same 4,000-character cap as the API and disables sending while a reply is in progress | P0 | EP-2 |
| FR-13 | A user can stop an in-progress reply | P1 | EP-2 |
| FR-14 | Transport, stream and history failures map to a typed code and a plain-language message, with a retry or sign-in action where one applies | P0 | EP-2 |
| FR-15 | Weather tool calls render as Bootstrap widgets (location choices, current conditions, daily forecast) instead of raw JSON | P1 | EP-2 |
| FR-16 | Reopening a session restores its full transcript, including completed tool widgets, without resending prior messages | P0 | EP-2 |
| FR-17 | A user can browse, resume, and delete their own sessions from a sessions list | P0 | EP-2 |
| FR-18 | Every chat request and history fetch carries a freshly acquired Entra ID bearer token, renewed silently or via redirect on expiry | P0 | EP-2 |
| FR-19 | The chat surface matches the Andes Bootstrap palette in light and dark and meets WCAG 2.2 AA contrast and target-size requirements | P1 | EP-2 |
| FR-20 | CopilotKit and its stylesheet load only on the chat route; the initial bundle grows by at most 1 kB | P0 | EP-2 |
| FR-21 | Operational docs, `.claude/CLAUDE.md` invariants, and ADR-0004/ADR-0005 describe the guard, the direct-browser architecture, and its accepted risks | P1 | EP-3 |
| FR-22 | A .NET unit-test project exercises the input guard and session-message mapping | P2 | EP-3 |

## 5. User experience

**Entry points & first-time flow**: a signed-in user lands on the existing lazy `pages/home/` and selects a "Start chatting" call to action, landing on `/chat`, which redirects to `/chat/{a freshly minted GUID}` (`US-201`, `US-211`). A user following a deep link to `/chat/{threadId}` for an existing session goes straight there; `connect()` restores its history (`US-204`).

**Core experience**:
1. The chat page renders a two-column layout at `lg` and above (a sessions sidebar and the chat column) or an offcanvas sidebar below that (`US-210`).
2. The user types into the composer — an autosizing textarea with a character counter — and presses Enter or Send (`US-206`).
3. `MafAgent` posts the trailing message with a bearer token; the assistant's reply streams in as sanitized Markdown (`US-202`, `US-205`).
4. If the agent calls a weather tool, its result renders as a Bootstrap card inline — a skeleton while pending, then the result or a friendly error (`US-207`).
5. The user can stop a reply in progress; a notice explains it may not be saved (`US-206`).
6. The session moves to the top of the sidebar once its turn finishes saving (`US-210`).
7. The user can start a new session, switch to a previous one and see its transcript restore, or delete a session from the list (`US-210`, `US-211`, `US-212`).

**Edge cases & UI states**:
- A new, never-used session shows suggested prompts instead of an empty transcript (`US-211`).
- Tool cards show a skeleton (`aria-busy`) while pending; the composer disables Send while a reply streams (`US-206`, `US-207`).
- Expired sign-in, forbidden, not-found, session-busy, rate-limited, invalid-input, upstream-failure, offline, and history-unavailable each show a distinct, non-technical message and, where one applies, an action — retry sign-in, start a new session, or reload (`US-203`).
- A session whose history fails to load shows "Earlier messages couldn't be loaded" with a reload action, rather than a blank transcript that looks merely empty (`US-203`, `US-204`).
- A deleted or nonexistent thread id opens as an empty new session instead of erroring (`US-204`, `US-211`).

**UI/UX highlights**:
- WCAG 2.2 AA contrast and a 24×24 px minimum target size in both themes (`US-208`, `US-209`).
- A visually-hidden live region announces "Agent is responding," "Reply complete," "Stopped," and "Opened {title}" (`US-209`).
- Reduced motion is respected throughout (`US-208`).
- Mobile-first at 360 px; the sessions list moves into an `NgbOffcanvas` below 992 px (`US-210`).
- Focus moves to the composer after a session switch, and to the sessions list heading after a delete (`US-209`, `US-212`).

## 6. Technical considerations

**Integration points**:
- **API** (`agents-api/`): `Api/Endpoints/AgentEndpoints.cs` (`MapAGUIServer` at `weather/ui`, hosting `1.20.0-preview.260831.1`, `AGUI.Server`/`AGUI.Abstractions` `0.0.5`); a new `Api/Filters/AGUIInputEndpointFilter.cs`; `Api/ExceptionHandlers/GlobalExceptionHandler.cs`; `Api/Configuration/CorsConfiguration.cs`; a new `Api/Problems/FallbackProblemDetailsWriter.cs`; `Service/Sessions/SessionIdValidator.cs` and `Service/Sessions/SessionMapper.cs`; `Dto/Sessions/SessionMessageDto.cs` plus new `SessionToolCallDto`/`SessionToolCallFunctionDto`/`SessionToolResultDto`; `Repository/Cosmos/Sessions/CosmosSessionRepository.cs`; a new `Common/Constants/AgentTurnLimits.cs`; `Directory.Packages.props`/`Api.csproj` (an explicit `AGUI.Abstractions` 0.0.5 reference).
- **UI** (`agents-ui/`): a new lazy `pages/chat/`; new `components/chat/*` (transport wiring, Markdown, input, tool renderers, weather widgets, session list, error alert); new `services/agents/maf-agent-client.ts` and `authorized-fetch.ts`; a new `core/auth/access-token-service.ts` built on the existing `AuthStore`/`MsalService` documented in [../../ui/authentication.md](../../ui/authentication.md); new `state/sessions/sessions-store.ts` and `state/chat/chat-status-store.ts`; new `src/styles/chat.scss`, `_copilotkit.scss`, `_chat-markdown.scss` (a lazy bundle); `angular.json` (budgets and lazy CSS entries) and `scripts/check-initial-chunk.mjs` (`pageRoots`/`lazyOnly` additions). Package pins: `@copilotkit/angular` 0.5.2, `@copilotkit/core` 1.70.2, `@ag-ui/client` 0.0.59, `@angular/cdk` `^22.1.0`, `zod` 3.25.76, `marked` 18.0.13, `dompurify` 3.4.15.

**Data storage & privacy**: no new data store. Stored data doesn't change: every document in the Cosmos DB `messages` container ([../../conversations/sessions-and-history.md](../../conversations/sessions-and-history.md)) already holds the full serialized chat message, and `api/conversations` gains two additive response fields (`toolCalls`, `toolResults`) projected from it — so no migration or backfill applies, and sessions stored before this change restore with their widgets too. The SQL Server usage-summary tables are untouched. Nothing a caller supplied may reach a log sink (existing repository invariant); prompt capture on OTel spans stays off in every environment. Access tokens live only in the browser's existing MSAL `localStorage` cache — no chat-specific token storage is introduced.

**Security**: the AG-UI input guard (`Api/Filters/AGUIInputEndpointFilter.cs`) is the actual trust boundary. Microsoft's [AG-UI security guidance](https://learn.microsoft.com/agent-framework/integrations/by-component/ui/ag-ui/security-considerations) recommends a trusted frontend server in front of a browser-exposed AG-UI server, which this design consciously does not add; the guard, the existing Entra ID bearer auth, and the existing 30-turns/min-per-`oid` rate limit are the accepted mitigation (recorded in [decisions.md](./decisions.md)). Rendered Markdown is sanitized with DOMPurify (forbidding media, iframes, forms and `style`, the `style`/`id`/`name` attributes, and non-`http(s)`/`mailto` URIs) plus `marked`'s own HTML escaping, closing CopilotKit's unsanitized `innerHTML` renderer. Every chat and history request attaches a freshly acquired MSAL access token per call, since the transport uses a raw `fetch` wrapper rather than `HttpClient` and therefore bypasses the existing `msalInterceptor`.

**Scalability & performance**: no new load-bearing infrastructure — requests still flow through the existing rate limiter and Cosmos DB's hierarchical partitioning. The known idle-stream limitation (no SSE keep-alive past Azure ingress's ~240 s idle timeout) is accepted for the fast, synthetic weather tools and flagged as a follow-up before any longer-running tool ships. CopilotKit (a single 3.66 MB unminified module with static imports of `marked`, `highlight.js` and `katex`) and its stylesheet load only behind the `/chat` route; the initial bundle budget (900 kB warning / 1 MB error) may grow by at most 1 kB.

**AI system requirements**: the weather agent, its three tools (`search_location`, `get_current_weather`, `get_daily_forecast`), and its prompt are unchanged — this feature only changes how a browser reaches the existing agent and how its output is rendered ([../../agent/hosting-and-protocols.md](../../agent/hosting-and-protocols.md)). The guard prevents a caller from supplying its own tools, context, or state to the model, so no new tool-injection surface is introduced. No model-output evaluation harness is added: the agent's responses aren't benchmarked as part of this feature, and the underlying weather data remains a deterministic, synthetic stub unchanged from today.

## 7. Epics & user stories

Acceptance criteria below reference the verification IDs defined in full in [verification.md](./verification.md): `SPK-1`–`SPK-9` (spikes, in the plan's spike-table order), `API-1`–`API-13` (the Design A smoke matrix, in table order), `GATE-API`/`GATE-UI` (build gates), and `UI-NET`/`UI-THEME`/`UI-XSS`/`UI-STOP`/`UI-TOKEN`/`UI-RESTORE`/`UI-SESSIONS`/`UI-MOTION` (browser checks).

Many stories in epic 1 and the core of epic 2 are P0 because this feature's central risk — a browser-exposed AG-UI server with no trusted frontend in front of it — makes the guard, its CORS exposure, and Markdown sanitization genuinely release-blocking rather than a prioritization oversight.

| ID | Epic | Goal | Priority | Estimate | Depends on |
| --- | --- | --- | --- | --- | --- |
| EP-1 | API hardening and history | Make `/weather/ui` and `api/conversations` safe to call directly from a browser, with tool-aware history | P0 | M | — |
| EP-2 | Chat UI | Give users a production-ready CopilotKit chat on Bootstrap, wired straight to the hardened API | P0 | L | EP-1 |
| EP-3 | Docs and operations | Document the guard and direct-browser architecture at implementation, and schedule automated API test coverage | P1 | M | EP-1, EP-2 |

### EP-1: API hardening and history

#### US-101: Reject a turn that isn't a single trailing user message

- **Story**: As the API hosting `/weather/ui`, I want to reject a request whose last message isn't user-authored text, or that carries non-empty `tools`, `context`, `resume`, `state`, or `forwardedProps`, so that a browser client can't inject instructions, tool definitions, or state the server didn't ask for.
- **Priority**: P0 · **Estimate**: M · **Depends on**: —
- **Acceptance criteria**:
  - Given a request whose last message has role `system` or `assistant`, when it reaches the guard, then the API returns 400 `validation-error` with `errors.messages`, and nothing is stored (`API-1`).
  - Given a request ending in a `user` message followed by an `assistant` message, when submitted, then the API returns 400 (`API-2`).
  - Given a request with a non-empty `tools` array (e.g. a tool named `reveal_secrets`), when submitted, then the API returns 400 with `errors.tools`, and the tools are never passed to the agent (`API-4`).
  - Given a last user message containing an image content part alongside text, when submitted, then the API returns 400; given text-only content, then it is accepted (`API-7`).
  - Given a request with non-empty `context`, `resume`, `state`, or `forwardedProps`, when submitted, then the API returns 400 naming the offending field.
  - Given a request with none of the above violations, when it reaches the guard, then only the trailing user message is forwarded to the agent, with `forwardedProps` replaced by an empty object.

#### US-102: Enforce the message length cap and thread id format

- **Story**: As the API, I want to reject a trailing user message over 4,000 characters or a thread id that isn't a `D`/`N`-form GUID, so that stored sessions and the SQL `[Core].[Session].SessionId` column stay within their limits.
- **Priority**: P0 · **Estimate**: S · **Depends on**: —
- **Acceptance criteria**:
  - Given a trailing user message of exactly 4,000 characters, when submitted, then the API accepts it (200); given 4,001 characters, then it returns 400 with `errors["messages.content"]` (`API-5`).
  - Given a blank (whitespace-only) trailing message, an empty `messages` array, or an empty request body, when submitted, then the API returns 400 with a body (`API-8`).
  - Given a `threadId` containing a slash, braces, or padding, when submitted, then the API returns 400 with `errors.threadId`; given a valid `N`-form GUID, then the API accepts it (`API-6`).
  - Given a caller resubmits the same oversized request twice, when both are rejected, then no message is stored for either attempt.

#### US-103: Forward a valid trailing-user turn to the agent unmodified in shape

- **Story**: As a signed-in caller, I want a well-formed request — a GUID thread id, prior context, and one trailing user message — accepted and passed to the agent, so that a real conversation turn can happen.
- **Priority**: P0 · **Estimate**: S · **Depends on**: US-101, US-102
- **Acceptance criteria**:
  - Given a GUID `threadId` and a history ending in system, an assistant tool call, a tool result, and a trailing user message "Seattle now?", when submitted, then the API returns 200, `RUN_STARTED.threadId` equals the supplied GUID, and only the "Seattle now?" message is newly stored (`API-3`).
  - Given a request with no `threadId`, when submitted, then the API mints an `N`-form GUID and returns it as `RUN_STARTED.threadId`.
  - Given a valid request, when the guard rewrites it, then the original `RunId` and `ParentRunId` are preserved unchanged.
  - Given a valid request, when the guard rewrites it, then the client-supplied message id is dropped from the forwarded user message.

#### US-104: Map malformed request bodies to a 400 problem response

- **Story**: As the API, I want malformed JSON and other unparseable request bodies to produce a 400 `validation-error` problem response instead of an unhandled 500, so that a broken client gets an actionable, non-leaking error.
- **Priority**: P1 · **Estimate**: S · **Depends on**: —
- **Acceptance criteria**:
  - Given a syntactically invalid JSON body posted to `/weather/ui`, when processed, then `GlobalExceptionHandler`'s `BadHttpRequestException` arm returns 400 `validation-error` with a body (`API-8`).
  - Given the same malformed body in Development, when processed, then the response still carries the 400 `validation-error` shape rather than a raw exception page.
  - Given a well-formed but semantically invalid body (wrong JSON type for a field), when processed, then the response is also 400, not 500.

#### US-105: Write problem+json for guard, auth and rate-limit failures negotiated over SSE

- **Story**: As a browser client that requested `Accept: text/event-stream`, I want a 400/401/429 failure to arrive as a readable `application/problem+json` body instead of an empty or malformed response, so the chat transport can parse the failure and show an accurate message.
- **Priority**: P1 · **Estimate**: M · **Depends on**: —
- **Acceptance criteria**:
  - Given a request with `Accept: text/event-stream` that fails the input guard, when the API responds, then the body is valid `application/problem+json` (`API-1`–`API-8`).
  - Given a caller with no bearer token calling `/weather/ui` with `Accept: text/event-stream`, when the request is processed, then the API returns 401 as problem+json (`API-9`).
  - Given a caller who has made 31 turns in the current minute, when they call `/weather/ui` with `Accept: text/event-stream`, then the API returns 429 as problem+json with a readable `Retry-After` value (`API-9`).
  - Given `FallbackProblemDetailsWriter` is registered after `AddProblemDetails`, when no other writer claims a failed response, then a problem body is still emitted rather than an empty 4xx/5xx.

#### US-106: Expose CORS headers for direct browser calls

- **Story**: As the Angular chat client calling `/weather/ui` and `api/conversations` directly from the browser, I want CORS to allow my origin and expose `Retry-After`, so that preflighted requests succeed and rate-limit responses are readable cross-origin.
- **Priority**: P0 · **Estimate**: S · **Depends on**: —
- **Acceptance criteria**:
  - Given a CORS preflight from a configured SPA origin for `POST /weather/ui` with `Authorization`, `Content-Type`, and `Accept` in `Access-Control-Request-Headers`, when sent, then the API returns 204 with the matching allow headers (`API-12`).
  - Given the same preflight from an origin not on `Cors:AllowedOrigins`, when sent, then the response carries no CORS allow headers (`API-12`).
  - Given a 429 response to a cross-origin request, when the browser reads response headers, then `Retry-After` is present in `Access-Control-Expose-Headers` and readable by client script (`API-9`).

#### US-107: Return tool calls and tool results on stored messages

- **Story**: As a user reopening a past session, I want the API to return the tool calls and results a turn produced, not just its text, so that the chat UI can re-render completed widgets instead of empty bubbles.
- **Priority**: P0 · **Estimate**: M · **Depends on**: —
- **Acceptance criteria**:
  - Given a turn where the agent called `get_current_weather` and got a result, when `GET api/conversations/{sessionId}/messages` is called afterward, then the assistant message's `toolCalls` array contains an entry with `type` `"function"` and a matching function name/arguments, and the tool message's `toolResults[0].toolCallId` matches that call's id (`API-11`).
  - Given a stored message with no tool activity, when listed, then `toolCalls` and `toolResults` are empty arrays, and clients reading only `text` are unaffected by the additive change.
  - Given a tool result whose content is a JSON-string element, when projected, then it is unwrapped rather than double-encoded; given a non-string, non-`JsonElement` value, then it is compact-serialized.
  - Given a message carrying reasoning or encrypted content, when projected, then neither is included in the DTO.

#### US-108: List sessions by most recent activity

- **Story**: As a user with several sessions, I want my sessions list ordered by most recent activity rather than creation time, so that the session I was just using is easiest to find.
- **Priority**: P0 · **Estimate**: S · **Depends on**: —
- **Acceptance criteria**:
  - Given an older session (A) receives a new turn after a newer session (B) was created, when `GET api/conversations` is called, then A is listed before B (`API-10`).
  - Given a legacy session document written before the `dateModified` rename (and so lacking that field), when listed, then it still appears in the results rather than being dropped by the ordering change (`API-10`).
  - Given the query orders by Cosmos DB's built-in `_ts`, when a session is saved, then its position in the list updates without an application-level timestamp write.

### EP-2: Chat UI

#### US-201: Route-scoped CopilotKit chat page loads without growing the initial bundle

- **Story**: As a signed-in user, I want the chat experience to live behind its own lazy route, so that CopilotKit's dependency tree never loads for anyone who isn't chatting.
- **Priority**: P0 · **Estimate**: M · **Depends on**: —
- **Acceptance criteria**:
  - Given a production build, when `npm run check:initial-chunk` runs, then no `@copilotkit`, `@ag-ui`, `@angular/cdk`, `marked`, `dompurify`, `highlight.js`, or `katex` module is reachable from `main` or the polyfills, and the initial bundle total grows by at most 1 kB over the current baseline (`SPK-1`, `GATE-UI`).
  - Given a user navigates to `/chat`, when the route resolves, then `chat.css` (CopilotKit's styles, the CDK overlay stylesheet, and the local chat partial) loads via the chat-styles resolver and is never referenced from `index.html` (`SPK-1`).
  - Given a user navigates to `/chat` with no trailing thread id, when the route matches, then it redirects to `/chat/{a freshly minted lowercase D-form GUID}`.
  - Given a thread id in the URL that fails client-side validation, when matched, then the route does not activate the chat component.

#### US-202: Send a message to the weather agent with a fresh bearer token

- **Story**: As a signed-in user, I want to type a message and have it sent to the weather agent with my current credentials, so that I get a real, authorized response.
- **Priority**: P0 · **Estimate**: L · **Depends on**: US-101, US-102, US-103, US-106, US-201
- **Acceptance criteria**:
  - Given a user submits a message, when `MafAgent.run()` executes, then it `POST`s to `{Api.BaseUrl}/weather/ui` with `Accept: text/event-stream, application/problem+json`, an `Authorization: Bearer` header carrying a freshly acquired token, and a body containing only the trailing user message with empty `tools`/`context`/`state`/`forwardedProps` (`UI-NET`, `SPK-4`).
  - Given the token cached by MSAL is inside its renewal window, when a request is about to be sent, then a fresh token is acquired silently before the request goes out (`UI-TOKEN`, `SPK-3`, `SPK-6`).
  - Given the API accepts the turn, when the response streams, then assistant text appears incrementally in the transcript.
  - Given the network request fails outright, and the failure wasn't caused by the user stopping the run, when this happens, then the transport does not throw out of the observable — it emits one `RUN_ERROR` instead (`US-203`).

#### US-203: Map transport and stream failures to actionable messages

- **Story**: As a user whose message couldn't be sent or whose reply failed mid-stream, I want a plain-language explanation and, where possible, something I can do about it, so that a failure doesn't look like the app is broken.
- **Priority**: P0 · **Estimate**: M · **Depends on**: US-202, US-105, US-106
- **Acceptance criteria**:
  - Given the API returns 401, 403, 404, 409, 429, or a 5xx for a run, when `MafAgent.run()` observes the response, then it emits exactly one `RUN_ERROR` with the matching code (`UNAUTHORIZED`, `FORBIDDEN`, `NOT_FOUND`, `CONVERSATION_BUSY`, `RATE_LIMITED` with `retryAfterSeconds`, or `UPSTREAM_ERROR`).
  - Given a `RUN_ERROR` with code `UNAUTHORIZED`, when the error alert renders, then it shows "Your sign-in has expired" with a retry-sign-in action; given `RATE_LIMITED`, it shows the wait time in seconds with no action.
  - Given a stream ends without a terminal `RUN_FINISHED` or `RUN_ERROR` event, when detected, then the UI shows a "reply cut off" message rather than leaving the transcript looking like it's still responding.
  - Given the browser is offline when a send is attempted, when detected, then the UI shows "You appear to be offline" rather than a generic upstream error.
  - Given a displayed error, when rendered, then it never includes the raw `error.message`, and appends "Reference: {traceId}" whenever one was returned.

#### US-204: Restore a session's history when reopening its thread

- **Story**: As a user opening a link to, or switching to, an existing session, I want to see everything said before, including completed tool results, so that I don't lose context on reload.
- **Priority**: P0 · **Estimate**: M · **Depends on**: US-107, US-201
- **Acceptance criteria**:
  - Given an existing thread id with stored messages, when the chat component binds `[threadId]`, then `MafAgent.connect()` fetches `api/conversations/{threadId}/messages`, and the transcript shows the full history — including rendered, completed tool widgets — after a page reload (`UI-RESTORE`, `SPK-2`).
  - Given a thread id that doesn't exist (a fresh GUID, or someone else's thread), when `connect()` runs, then the API's 404 is treated as an empty session, with no error shown (`SPK-2`).
  - Given `connect()` receives a 401 or 403, when this happens, then it surfaces `UNAUTHORIZED` or `FORBIDDEN` respectively, rather than silently showing an empty thread.
  - Given the same `[threadId]` binds twice in quick succession, when `connect()` is invoked, then in-flight loads are de-duplicated so only one `GET` is issued.
  - Given a session with more than 500 stored messages, when `connect()` loads history (paged at 200 per request), then only the newest 500 are shown.

#### US-205: Render assistant replies as sanitized Markdown

- **Story**: As a user reading the agent's reply, I want formatted Markdown that can't execute a script or leak my session through a remote image, so that the chat surface is safe even if the model reflects untrusted content.
- **Priority**: P0 · **Estimate**: M · **Depends on**: US-202
- **Acceptance criteria**:
  - Given the XSS corpus (script tags, `onerror` handlers, `javascript:`/`data:` links, an SVG `onload`, `id`/`name`-clobbering attempts, a remote `<img>`, and a malicious code-fence language), when rendered, then no forbidden node, inline handler, or remote image request results, and no dialog opens (`UI-XSS`).
  - Given a link in a reply, when rendered, then it carries `target="_blank"` and `rel="noopener noreferrer nofollow"`, and only `http(s)` or `mailto` URIs are allowed through.
  - Given a partial (streaming) Markdown fragment, when rendered mid-stream, then it is repaired before sanitization runs, so a reply doesn't flash unclosed formatting.
  - Given a code-fence language that doesn't match `[\w+-]{1,32}`, when encountered, then it is not treated as a valid language token.

#### US-206: Compose and send messages with a length cap, stop, and busy handling

- **Story**: As a user writing a message, I want a composer that stops me sending an overlong or empty message, shows me it's busy while a reply streams, and lets me stop that reply, so that I stay in control of the session.
- **Priority**: P0 · **Estimate**: M · **Depends on**: US-201, US-202
- **Acceptance criteria**:
  - Given the textarea holds more than 4,000 characters, when the user looks at the composer, then the counter shows the overage and Send stays disabled until the text is within the limit (mirrors `API-5`).
  - Given a reply is streaming, when the user presses Enter or Send again, then no second request is sent — five rapid Enters produce exactly one `POST` and cause no 409 (`UI-STOP`, `SPK-7`).
  - Given a run this client started is in progress, when the user selects Stop, then the run stops, the input re-enables within 1 second, and a "Stopped — this reply may not be saved" notice appears (`UI-STOP`).
  - Given the user is mid-IME composition, when they press Enter, then the message is not sent; given a coarse pointer device, then Enter inserts a newline instead of sending.
  - Given Shift+Enter is pressed, when this happens, then a newline is inserted rather than the message being sent.

#### US-207: Render weather tool calls as Bootstrap widgets

- **Story**: As a user asking about weather, I want the agent's tool calls to appear as readable cards — a place list, current conditions, or a forecast — instead of raw JSON, so that the answer is easy to scan.
- **Priority**: P1 · **Estimate**: L · **Depends on**: US-107, US-202
- **Acceptance criteria**:
  - Given the agent calls `search_location` and gets multiple matches, when the result arrives, then a list of location choices renders.
  - Given `get_current_weather` returns a result, when rendered, then a card shows the condition icon, °C/°F, and a "feels like" value.
  - Given `get_daily_forecast` returns a result, when rendered, then a list renders below the `md` breakpoint and a compact table renders at `md` and above.
  - Given a tool call is still executing, when rendered, then the card shows a skeleton with `aria-busy="true"`; given it completed with a tool error, then a friendly message shows, never raw JSON.
  - Given a restored (historical) tool call has no stored result, when rendered with no run active, then the card shows a "no result" state rather than staying stuck on "pending."
  - Given a tool this renderer set doesn't recognize, when it appears, then the fallback renderer shows only the tool's name and status.

#### US-208: Theme the chat UI onto Bootstrap/Andes tokens in light and dark

- **Story**: As a user of either theme, I want the chat surface to look like the rest of the app rather than CopilotKit's default styling, so the product feels consistent.
- **Priority**: P1 · **Estimate**: M · **Depends on**: US-201, US-205
- **Acceptance criteria**:
  - Given the chat route is active, when computed styles are diffed against plain Bootstrap, then only the variables the theme partial maps differ — no unmapped CopilotKit color leaks through (`SPK-5`).
  - Given `data-bs-theme` is dark, when the chat renders, then its variables resolve to the dark Andes palette, and text/UI contrast meets WCAG 2.2 AA at 360, 768, and 1280 px (`UI-THEME`).
  - Given a user has `prefers-reduced-motion` enabled, when the chat renders or streams, then no animation beyond an immediate state change occurs (`UI-MOTION`).
  - Given the regenerate/edit toolbar actions on assistant messages, when the chat renders, then they are hidden, since the app doesn't support editing history (`SPK-5`).

#### US-209: Meet accessibility requirements for announcements, focus, and keyboard use

- **Story**: As a keyboard or screen-reader user, I want state changes announced and focus managed predictably, so that I can use the chat without a mouse or without seeing the screen.
- **Priority**: P1 · **Estimate**: S · **Depends on**: US-203, US-206
- **Acceptance criteria**:
  - Given a reply starts, streams, and completes, when these transitions happen, then a visually-hidden live region announces "Agent is responding" and "Reply complete"; given the user stops a reply, it announces "Stopped."
  - Given a user switches sessions, when the new session loads, then focus moves to the composer.
  - Given a user deletes a session, when the deletion completes, then focus moves to the sessions list heading.
  - Given a keyboard-only user tabs through the composer, Send, Stop, and the sessions list, when doing so, then every control is reachable and operable without a mouse, and every interactive target is at least 24×24 px.

#### US-210: Browse and resume sessions from a most-recent-first list

- **Story**: As a user with multiple sessions, I want a sidebar (or offcanvas, on small screens) listing my sessions with the newest activity first, so that I can find and reopen the one I want.
- **Priority**: P0 · **Estimate**: M · **Depends on**: US-108, US-201
- **Acceptance criteria**:
  - Given the caller has several sessions, when the chat page loads, then the list shows them in the same order `api/conversations` returned, with "Load more" once more than the first page exists.
  - Given the user selects a session from the list, when clicked, then the app navigates to `/chat/{that threadId}` and its transcript restores (`US-204`).
  - Given the viewport is below 992 px, when the user opens the sessions button, then the list appears in an offcanvas positioned at the start and is dismissed automatically at 992 px and above.
  - Given the caller has no sessions yet, when the page loads, then the list shows an empty state instead of nothing.
  - Given a full keyboard-only pass over the list and offcanvas, when performed, then the offcanvas traps and restores focus correctly (`UI-SESSIONS`).

#### US-211: Start a new chat session

- **Story**: As a user, I want an obvious way to start a fresh session, so that I'm not stuck continuing an old one.
- **Priority**: P0 · **Estimate**: S · **Depends on**: US-201, US-204
- **Acceptance criteria**:
  - Given the user selects "New chat," when this happens, then the app navigates to `/chat/{a new lowercase D-form GUID}`.
  - Given the new session has no history, when it opens, then `connect()` gets a 404, the transcript opens empty, and three suggested prompts render instead of CopilotKit's default welcome screen.
  - Given the home page, when a signed-in user views it, then a "Start chatting" call to action is present and leads to `/chat`.

#### US-212: Delete a session

- **Story**: As a user, I want to remove a session I no longer need, so that my sessions list only shows sessions I still care about.
- **Priority**: P1 · **Estimate**: S · **Depends on**: US-210
- **Acceptance criteria**:
  - Given a session in the list, when the user selects delete and confirms in the modal, then `DELETE api/conversations/{sessionId}` is called and the entry is removed from the list on success.
  - Given the user opens the delete confirmation and cancels, when this happens, then nothing is deleted and the modal closes.
  - Given the deleted session was the active thread, when the deletion completes, then the app starts a new session rather than showing a now-nonexistent thread.
  - Given a repeat delete call on an already-deleted session returns 404, when this happens, then the UI still treats the entry as removed rather than showing an error.

### EP-3: Docs and operations

#### US-301: [enabler] Update operational and architecture docs for the hardened, browser-direct AG-UI endpoint

- **Story**: [enabler] Update `docs/operations/runbook.md`, `docs/operations/configuration.md`, [../../agent/hosting-and-protocols.md](../../agent/hosting-and-protocols.md), [../../conversations/sessions-and-history.md](../../conversations/sessions-and-history.md), `docs/architecture/overview.md`, and the relevant `.claude/CLAUDE.md` invariants once epics 1 and 2 land, so the guard, tool-aware history, session ordering, and direct browser exposure are documented where an operator would look. This unblocks nothing downstream but is the only account of the new trust boundary an operator has.
- **Priority**: P1 · **Estimate**: M · **Depends on**: —
- **Acceptance criteria**:
  - Given the guard and fallback-writer changes ship, when the runbook and `CLAUDE.md` are updated, then they describe the input guard as the security boundary and correct the existing "no `RUN_ERROR` on mid-stream failure" caveat if it changed.
  - Given the ordering change ships, when `sessions-and-history.md` is updated, then it documents `_ts DESC` ordering and includes a `toolCalls`/`toolResults` example.
  - Given the runbook's existing references to a nonexistent `appsettings.Development.json` are already known to be stale, when the runbook and configuration docs are touched, then those references are corrected to user-secrets/environment-variable guidance in the same pass.

#### US-302: [enabler] Record ADR-0004 and ADR-0005

- **Story**: [enabler] Write ADR-0004 (CopilotKit's prebuilt chat with no Node runtime, connecting directly to the API) and ADR-0005 (the AG-UI input guard as the trust boundary, and the accepted risk of not fronting AG-UI with a trusted server) from [decisions.md](./decisions.md)'s decision log, once the design lands. This gives future maintainers the "why," not just the "what," for two decisions this PRD treats as settled but doesn't itself formalize as ADRs.
- **Priority**: P1 · **Estimate**: S · **Depends on**: —
- **Acceptance criteria**:
  - Given `decisions.md`'s decision log and accepted risks, when ADR-0004 and ADR-0005 are written, then each follows the existing ADR template (`docs/adr/0001`–`0003`) and is linked from `docs/README.md`'s Decisions section.
  - Given the CopilotKit license caveat on `selfManagedAgents`, when ADR-0004 is written, then it names the caveat and its revisit trigger (confirmed CopilotKit license terms).

#### US-303: [enabler] Add a .NET unit-test project for the guard and session mapping

- **Story**: [enabler] Stand up `agents-api/tests/` (xUnit v3, NSubstitute) covering the input guard's validation rules and `SessionMapper`'s tool-call/result projection with automated tests, once the scratch-app and smoke-matrix verification in epic 1 has already proven the behavior by hand. This unblocks nothing further but closes the "no .NET tests exist yet" gap this feature would otherwise leave permanently open.
- **Priority**: P2 · **Estimate**: L · **Depends on**: —
- **Acceptance criteria**:
  - Given the guard's rejection rules (`US-101`, `US-102`), when unit-tested, then each rejection case in the smoke matrix (`API-1`, `API-2`, `API-4`–`API-8`) has a corresponding automated test.
  - Given `SessionMapper`'s projection (`US-107`), when unit-tested, then function-call, function-result, reasoning-exclusion, and encrypted-content-exclusion cases are each covered.
  - Given the project is added, when `dotnet build agents-api/Andes.Agents.slnx` runs, then it builds cleanly under the existing `TreatWarningsAsErrors` settings.

## 8. Milestones & rollout

**Phases** (topological order of the dependency graph in section 7):
- **Phase 1 — API hardening core**: `US-101`, `US-102`, `US-103`, `US-106`, `US-107`, `US-108` (EP-1's P0 stories). Nothing else can start until the guard, CORS exposure, and tool-aware history exist. Estimate: TBD (no team size or velocity given — see section 9).
- **Phase 2 — API hardening completeness**: `US-104`, `US-105` (EP-1's P1 stories). Can run alongside phase 1; `US-203` in phase 3 depends on `US-105`. Estimate: TBD.
- **Phase 3 — Chat MVP**: `US-201`–`US-206`, `US-210`, `US-211` (EP-2's P0 stories). Depends on phases 1 and 2. This is the minimum a user needs to have a real, safe conversation and find it again. Estimate: TBD.
- **Phase 4 — Chat polish**: `US-207`, `US-208`, `US-209`, `US-212` (EP-2's P1 stories). Depends on phase 3. Estimate: TBD.
- **Phase 5 — Docs, ADRs, and test follow-up**: `US-301`, `US-302` (after phases 3–4 ship); `US-303` can start any time after phase 2, independent of epic 2. Estimate: TBD.

**Risks & mitigations**:
- **License risk**: CopilotKit's docs label production `selfManagedAgents` as part of its Enterprise Intelligence tier, and the npm package itself has no license check. Mitigation: confirm terms with CopilotKit before enabling `/chat` in production; tracked as a revisit trigger in `decisions.md`/ADR-0004 (`US-302`).
- **Direct exposure**: Microsoft recommends a trusted frontend server in front of AG-UI; this design exposes it directly. Mitigation: the input guard, Entra ID auth, and the per-`oid` rate limit (`US-101`, `US-102`, `US-106`); `API-1` is added to the ADR-0001 hosting-package upgrade checklist so a future hosting bump can't silently bypass the guard.
- **Churn**: AG-UI is a 1.0 draft, `@ag-ui/client` is 0.0.59, CopilotKit Angular is 0.x, and MAF's hosting packages are a preview build. Mitigation: every version is pinned exactly, with revisit triggers recorded in `decisions.md`; watch the upstream issues named there (double-connect, stop, pluggable Markdown, thread-changes-observable).
- **Guard trade-offs**: frontend tools, shared state, and human-in-the-loop are rejected by design; A2UI later needs the .NET agent to emit surfaces on its own, since no CopilotKit runtime or middleware runs here. Mitigation: tracked in [../a2ui/prd.md](../a2ui/prd.md).
- **Idle streams**: no SSE keep-alive past Azure ingress's roughly 240 s idle timeout. Mitigation: accepted for the current fast, synthetic weather tools; flagged as a follow-up before any longer-running tool ships.
- **Oversized request bodies**: the full body deserializes before the guard runs. Mitigation: a per-route body size limit is an open follow-up (section 9), not part of this release.
- **Duplicate history**: retry, regenerate, or edit actions could resend a stored user message and store it twice. Mitigation: those actions are hidden from the assistant message toolbar (`US-208`).

**Rollout & rollback**: no feature flag exists in `agents-ui` or `agents-api`. Rollout is ordinary PR-by-PR merges to `main`, gated by the existing `csharp-code-reviewer`/`code-review` and `security-review` passes, `GATE-API`/`GATE-UI`, and the full smoke matrix and spike verification in `verification.md`, before the home page's "Start chatting" call to action goes live (`US-211`). Rollback is reverting the merging PR(s): the guard, transport, and chat route are additive, with no destructive migration or schema change, and the guard is fail-closed by construction (an unrecognized shape is a 400, not a pass-through), so a partial rollback that removes the UI but leaves the guard in place doesn't break A2A or existing `api/conversations` callers, none of which send the fields the guard rejects.

## 9. Assumptions & open questions

**Assumptions**:
- The Entra ID sign-in work is committed on this branch (`9f1d7ec`: `agents-ui/src/app/core/auth/*`, with the redirect bridge at `/auth`); epic 2 builds on `AuthStore`, `TokenRefreshService`, and `msal.interceptor.ts` as they exist today rather than redesigning them. A reviewer should flag this if that work has since diverged or been reverted.
- CopilotKit's `selfManagedAgents` license terms for production use are assumed unresolved as of this draft; epic 2 ships the design under that open risk, and enabling `/chat` in a production deployment is assumed to wait on legal/commercial confirmation (`US-302`).
- No feature flag or staged-rollout mechanism is assumed necessary; ordinary code review plus the verification gates in `verification.md` are assumed sufficient, since the change is additive and revertible.
- No new client-side analytics or usage telemetry is assumed in scope; the existing OTel `gen_ai.*` spans (prompt capture off) are assumed to remain the only instrumentation this feature needs.
- Story estimates (S/M/L) are relative sizing only. No team size or velocity was supplied, so the milestone durations in section 8 are `TBD` until a team is assigned.
- `US-108`'s session-ordering change is treated as P0 alongside the rest of the sessions-list MVP, since `US-210` (P0) depends on it; a reviewer who considers ordering non-blocking should downgrade both together, not `US-108` alone.
- No stored data changes for `toolCalls`/`toolResults`: every message document already holds the full chat message, so the projection applies to sessions stored before this change as well, with no migration or backfill.

**Open questions**:
- Has CopilotKit confirmed `selfManagedAgents` licensing terms for this production use case? — CopilotKit/owner, before `/chat` is enabled for real users (tracked as a revisit trigger in `decisions.md`).
- Should a per-route request body size limit be added to `/weather/ui` before or alongside this release, given the guard deserializes the full body before rejecting it? — Owner, decides whether this becomes a story in a future PRD revision or stays a documented follow-up.
- Is the roughly 240 s Azure ingress idle-stream limitation acceptable indefinitely, or does it need a keep-alive follow-up before a longer-running tool is added to the weather agent? — Owner/API maintainer.
- Should `US-303` (the .NET test project) gate future PR merges once it exists, or remain best-effort, given "no CI, and no .NET tests" is a standing characteristic of this repository today? — Owner.
