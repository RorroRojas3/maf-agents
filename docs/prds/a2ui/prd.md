# PRD: A2UI declarative generative UI for the weather agent

**Status:** Proposed — blocked on the dependencies in section 6 and the open decisions in section 9.

**Builds on:** the [CopilotKit chat PRD](../copilotkit/prd.md) (written in parallel) — the CopilotKit prebuilt Angular chat, its controlled tool-call widgets, its tool-call/result persistence, and its AG-UI input guard are the foundation this PRD's epics extend, not redesign.

**Learning doc:** [`learn/03-a2ui.md`](../copilotkit/learn/03-a2ui.md) covers declarative vs. controlled generative UI, the A2UI v0.9.1 message model, and why neither MAF .NET's nor CopilotKit's own A2UI path fits this repository's no-Node-server constraint out of the box. This PRD assumes that reading and does not repeat it.

## 1. Overview

**Problem.** The weather agent's only way to show structured output today is the CopilotKit chat's controlled tool-call widgets (per-tool, hand-built Bootstrap components, one PRD parallel to this one). That pattern doesn't scale to richer, agent-composed layouts — a multi-day forecast that needs a different shape for a mobile screen than a desktop table, or a location picker the agent assembles from a variable number of candidates — without a new bespoke widget for every shape. A2UI's declarative message model (v0.9.1, now spec-stable) lets an agent describe such a surface once from a fixed component catalog, but nothing in this stack currently has a way to produce or render one: Microsoft Agent Framework's .NET hosting emits no A2UI, and CopilotKit's own A2UI middleware is a Node-only package this repository's owner has ruled out.

**Solution.** Adopt A2UI v0.9.1 for one concrete surface — the multi-day forecast — end to end: the weather agent describes it declaratively, and the Angular client renders it from a trusted, allow-listed Bootstrap 5.3 component catalog, themed to the Andes palette in both themes and responsive at 360/768/1280px to WCAG 2.2 AA. Because the renderer, the transport from `agents-api` to the browser, and the inbound action contract all have open, unresolved choices (detailed in section 6), the first epic is a time-boxed spike that picks between the documented candidates before any implementation epic starts.

**Success criteria** *(TBD targets carry an assumption in section 9; there is no measured baseline yet)*:
- The renderer-and-transport spike (EP-1) concludes with a written, checked-in decision within an assumed 3-person-day time-box, measured by the decision log's completion date in `docs/prds/copilotkit/decisions.md`.
- The chosen renderer renders a fixture 7-day forecast surface with zero unrecognized-component fallbacks and zero escapes across the XSS/catalog fuzz corpus, measured by that corpus's pass rate (target 100%).
- The rendered surface has zero critical or serious axe-core violations at 360px, 768px and 1280px in both `data-bs-theme` values, measured by an automated accessibility scan plus a manual contrast check, mirroring the CopilotKit PRD's verification approach.
- 100% of forecast surfaces the server builds pass catalog-schema validation before leaving `agents-api` (US-202), measured by a rejection-rate log/metric once implemented.
- TBD end-to-end latency from tool-call completion to surface paint (assumption: under 2s at p95) — measured by browser timing instrumentation once the feature exists to instrument.

A user's journey once this ships: they ask the weather agent for a week's forecast; the agent resolves the place, calls `get_daily_forecast`, and — instead of only a paragraph of text or a single controlled tool-call card — the client renders a declarative 7-day surface from the trusted catalog, themed like the rest of the chat, that (once epic 4 ships) they can also act on, for example switching units or picking a different day range, without retyping the request.

## 2. Goals & non-goals

**Goals**
- Let the weather agent describe at least one declarative UI surface — the multi-day forecast — using the A2UI v0.9.1 message model (`createSurface`, `updateComponents`, `updateDataModel`).
- Render that surface in `agents-ui` through a trusted, catalog-allow-listed Bootstrap 5.3 renderer, themed with the Andes palette in light and dark, responsive at 360/768/1280px, and WCAG 2.2 AA.
- Establish a transport from the .NET agent to the browser that needs no Node process of any kind.
- Support a user-action round trip from a rendered surface back to the agent, including the API contract change that carrying an action requires.
- Record the renderer, transport and action-contract decisions, their alternatives, and the triggers that would reopen them, given the spec and every candidate library are pre-1.0.

**Non-goals**
- **No Node server of any kind.** CopilotKit's `a2ui` runtime middleware and `@ag-ui/a2ui-middleware` (Node-only; imports `node:crypto`) are out of scope by owner decision, regardless of how much of the transport problem they would otherwise solve.
- **No open-ended or MCP Apps generative UI.** A2UI's declarative, catalog-constrained model is the only generative-UI tier this PRD adopts; an agent never returns arbitrary markup, script, or an iframe.
- **No client-injected tools.** A2UI operations do not ride as a client-supplied `render_a2ui` tool — the AG-UI input guard (CopilotKit PRD, Design A) rejects non-empty `tools` from the browser, and this PRD does not propose changing that.
- **No production implementation this iteration.** This document and its companion learning doc are the entire deliverable; no code changes land in `agents-api/` or `agents-ui/` from this PRD alone.
- **No A2UI v1.0 RC features.** Scope is v0.9.1 stable only; v1.0's breaking changes are a revisit trigger (section 8), not a target.
- **No surface beyond the forecast example is fully scoped.** A location-picker surface appears only as an illustrative second example for the interactive epic (US-403); see the open question in section 9 about promoting it.
- **No change to the controlled tool-call-widget pattern.** A2UI is additive to the CopilotKit PRD's existing per-tool widgets, not a replacement for them.

## 3. Users & access

**Personas**
- **Weather chat user**: the same authenticated end user as the CopilotKit chat PRD, asking the weather agent questions through `agents-ui`'s chat page.

**Role-based access**
- **Authenticated caller** (Entra ID bearer token, same audience and scope as every other `agents-api` route): can cause the agent to emit a surface only within their own session, and — once epic 4 ships — can submit an action only against a surface belonging to a thread they own. No new anonymous surface is introduced; the existing `AgentAccess` authorization policy and per-`oid` `AgentTurns` rate limit apply unchanged.

## 4. Functional requirements

| ID | Requirement | Priority | Epic(s) |
| --- | --- | --- | --- |
| FR-1 | The weather agent must be able to emit an A2UI v0.9.1-conformant surface describing a multi-day forecast from a `get_daily_forecast` result. | P0 | EP-2 |
| FR-2 | The Angular client must render an A2UI `createSurface`/`updateComponents`/`updateDataModel` message using only components from a trusted, allow-listed Bootstrap-based catalog — never arbitrary markup. | P0 | EP-3 |
| FR-3 | Every rendered surface must theme to the Andes palette in both `data-bs-theme` values and remain usable with no horizontal overflow at 360px, 768px and 1280px, meeting WCAG 2.2 AA. | P0 | EP-3 |
| FR-4 | The system must validate every surface against the A2UI basic-catalog schema, both server-side before it streams and client-side before it renders, and must degrade to plain text/tool-widget rendering rather than render an unknown component or unresolved remote media. | P0 | EP-2, EP-3 |
| FR-5 | A time-boxed spike must select the renderer and the transport from the documented candidates before any other implementation epic starts. | P0 | EP-1 |
| FR-6 | The API must define and implement one narrowly allow-listed contract for an inbound A2UI user action, given the existing AG-UI input guard rejects non-empty `forwardedProps`, `state`, `context` and `tools` from the browser. | P1 | EP-4 |
| FR-7 | The renderer, transport and action-contract decisions — including rejected alternatives and revisit triggers — must be recorded for future contributors. | P2 | EP-5 |

## 5. User experience

**Entry points & first-time flow.** A surface appears inline in the existing CopilotKit chat transcript (`/chat/:threadId` from the CopilotKit PRD) the first time the model calls `get_daily_forecast` after this feature ships — there is no separate entry point or opt-in the user takes.

**Core experience.** The user asks something like "What's the forecast for Seattle this week?" The agent resolves the place with `search_location`, calls `get_daily_forecast`, and the client renders the resulting A2UI surface — for example a card row on narrow screens and a table from `md` up — in place of, or alongside, the plain-text/tool-call-widget rendering the CopilotKit PRD already provides for that tool.

**Edge cases & UI states**
- **Unrecognized component or unresolved remote media**: the client renders nothing for that node rather than raw or unsafe markup; if the whole surface fails validation, the transcript falls back to the existing plain-text/tool-widget rendering for that turn.
- **Oversized surface** (more components than the server-side cap in US-202): the server does not emit it; the client never receives an oversized surface to begin with.
- **Action submitted while a run is busy** (EP-4): the actionable component disables, mirroring the existing chat input's busy state from the CopilotKit PRD.
- **Transport failure** (neither the tool-result convention nor `ACTIVITY_SNAPSHOT` is available for a given deployment): the turn silently falls back to the plain-text/tool-widget path — the user always gets an answer, only sometimes a plain one.

**UI/UX highlights.** Components come from the existing Bootstrap 5.3 + ng-bootstrap toolkit already themed for `agents-ui` (Summit Navy/Slate/Glacier Blue/Andes Green, dark-theme primary repoint) rather than a second design system; icons are Bootstrap Icons; no remote media renders without an explicit allow-list entry.

## 6. Technical considerations

**Integration points**
- `agents-api/Andes.Agents.Service/Weather/Tools/WeatherToolProvider.cs` and `WeatherRecords.cs` (`DailyForecastResult`, `DailyForecastDay`) — the tool whose result becomes the forecast surface's data model.
- `Api/Endpoints/AgentEndpoints.cs` (`AddAGUIServer`/`MapAGUIServer` at `weather/ui`, documented in `docs/agent/hosting-and-protocols.md`) — the AG-UI stream any transport candidate must ride, on the pinned preview hosting build `Microsoft.Agents.AI.Hosting.AGUI.AspNetCore` 1.20.0-preview.260831.1 (ADR-0001).
- The CopilotKit PRD's Design A (`AGUIInputEndpointFilter`, `Api/Filters/`) — the input guard that FR-6/EP-4 must extend rather than bypass, and Design A's `SessionMapper` tool-call/result projection, which US-203 extends rather than replaces.
- The CopilotKit PRD's Design B (`agents-ui` chat page, `components/chat/tools/` renderer pattern, `_copilotkit.scss` theming partial, `render-markdown`/DOMPurify sanitization pipeline) — the client surfaces this PRD's renderer sits beside and reuses, not duplicates.
- `@ag-ui/client`'s `HttpAgent`/`MafAgent` transport (CopilotKit PRD, B2) — whichever AG-UI event type carries the A2UI payload arrives through this same client-side agent.

**Data storage & privacy.** A rendered surface's data model is derived entirely from existing tool-result fields (place names, coordinates, forecast values) already covered by the CopilotKit PRD's privacy handling — no new PII is introduced. Whether the A2UI operations needed to restore a surface on reload are persisted alongside the existing stored tool-call/result messages (US-203) is scoped as a story, not assumed; if out of scope, restoring a session shows text only for that turn, same as today.

**Security.** A2UI is declarative with a catalog allow-list, but the surface and its data remain untrusted model output: every surface is validated against the basic-catalog schema server-side (US-202) and again client-side before rendering (US-302), free text is sanitized through the same pipeline pattern as chat markdown, no remote media renders without an explicit allow-list, and layout size is capped. Authn/authz is inherited unchanged from the rest of `agents-api` — Entra ID bearer auth, the `AgentAccess` policy, the CORS allow-list, and the per-`oid` `AgentTurns` rate limit. Widening the input guard for FR-6 is treated as a narrow, explicit allow-list addition (US-401/US-402), not a general relaxation, because the guard exists specifically to close the injection vectors Microsoft's AG-UI security guidance names.

**Scalability & performance.** No load target is specified; this feature rides the same per-`oid` rate limit and single-agent-instance assumptions as the rest of the chat. Adding a renderer library to `agents-ui`'s lazy chat chunk (already budgeted separately in the CopilotKit PRD, B11) needs its own bundle-size accounting once a candidate is chosen (US-101) — no number is fabricated here.

**AI system requirements**
- **Tools/APIs needed:** the existing `search_location` and `get_daily_forecast` tools; a mechanism for the .NET agent to emit A2UI messages, which does not exist today — MAF .NET emits no A2UI and no official .NET A2UI SDK exists on NuGet (`a2ui-agent-sdk` is Python-only).
- **Evaluation strategy:** schema conformance against a fixed, hand-authored A2UI fixture corpus (size TBD, set by the spike), not a generation-quality eval — the agent's job is to populate a fixed catalog template from tool output, not to compose novel layouts.
- **Pass threshold:** 100% of the fixture corpus validates against the A2UI v0.9.1 basic-catalog schema before this feature is considered spike-complete (US-101/US-201 acceptance criteria).

## 7. Epics & user stories

| ID | Epic | Goal | Priority | Estimate | Depends on |
| --- | --- | --- | --- | --- | --- |
| EP-1 | Renderer-and-transport spike | Choose a renderer and a transport for A2UI surfaces before implementation work starts | P0 | S | — |
| EP-2 | .NET surface emission | The weather agent emits one A2UI-conformant forecast surface | P0 | M | EP-1 |
| EP-3 | Angular rendering and theming | The client renders a trusted, themed, accessible forecast surface | P0 | M | EP-1 |
| EP-4 | User-action round trip | A user can act on a rendered surface and have the action reach the agent | P1 | M | EP-2, EP-3 |
| EP-5 | Decision log and revisit triggers | The A2UI decision, its alternatives and its revisit triggers are recorded | P2 | S | EP-1 |

### EP-1: Renderer-and-transport spike

#### US-101: [enabler] Spike — prove a renderer can render a static forecast surface

- **Story**: As the engineering team, I want a time-boxed spike proving at least one candidate renderer can render a hand-authored A2UI v0.9.1 forecast surface, so that epic 3 starts from a validated choice instead of a guess.
- **Priority**: P0 · **Estimate**: S · **Depends on**: —
- **Acceptance criteria**:
  - Given a hand-authored `createSurface` + `updateDataModel` message for a 7-day forecast fixture, when each candidate renderer (`@a2ui/angular` 0.10.7, a custom Bootstrap renderer on `@a2ui/web_core` 0.11.0, or CopilotKit Angular's A2UI activity renderer) processes it, then the spike records whether it renders without throwing.
  - Given `@a2ui/angular`'s declared peer range `^21.2.5` against this workspace's Angular 22.1, when the spike attempts installation, then it records whether it resolves cleanly, needs an override, or fails outright.
  - Given the custom-Bootstrap-on-`@a2ui/web_core` candidate, when the spike builds only the basic-catalog components a forecast surface needs, then it records a person-day estimate for the remaining components of the 18-component catalog.
  - Given CopilotKit Angular's activity renderer, when the spike runs it with no CopilotKit runtime configured, then it records whether the renderer works standalone or hard-depends on runtime state (the open question this PRD inherits from the CopilotKit PRD's research).
  - Given all three candidates were exercised, when the spike concludes, then a written comparison (bundle size, peer-dependency fit, remaining build effort) is ready for US-103.

#### US-102: [enabler] Spike — prove a transport can carry a surface to the browser

- **Story**: As the engineering team, I want a time-boxed spike proving one of the two candidate transports can carry an A2UI message from `agents-api` to the browser over the existing `/weather/ui` AG-UI stream, so that epic 2 starts from a validated integration point.
- **Priority**: P0 · **Estimate**: S · **Depends on**: —
- **Acceptance criteria**:
  - Given a stub tool result shaped as `{ ...DailyForecastResult, a2ui_operations: [...] }`, when the existing `MapAGUIServer` pipeline streams the resulting `TOOL_CALL_RESULT` event with no hosting-package changes, then the spike confirms the A2UI payload round-trips unmodified through JSON serialization to the client.
  - Given `AGUIStreamOptions.MapResult`/`MapContent`, when the spike attempts to emit an `ACTIVITY_SNAPSHOT` event with `activityType: "a2ui-surface"` from a minimal AG-UI host, then it records whether the pinned `Microsoft.Agents.AI.Hosting.AGUI.AspNetCore` 1.20.0-preview.260831.1 build supports it, since this path is undocumented for .NET.
  - Given both transports were exercised, when the spike concludes, then a written recommendation names which one epic 2 implements, with the rejected option's specific failure mode (compile error, silent drop, thrown exception) recorded.

#### US-103: Decide and record the renderer and transport

- **Story**: As the engineering team, I want the spike results turned into one recorded decision — renderer, transport, and fallback — so that epics 2 through 4 can be scoped without an open unknown blocking them.
- **Priority**: P0 · **Estimate**: S · **Depends on**: US-101, US-102
- **Acceptance criteria**:
  - Given both spikes are complete, when the decision is recorded, then it names the chosen renderer, the chosen transport, and the specific fallback if either later breaks.
  - Given `@a2ui/angular` still lacks Angular 22 support at decision time and the spike found no working override, when the renderer decision is made, then the custom Bootstrap renderer on `@a2ui/web_core` is chosen.
  - Given the decision is recorded, when a reviewer reads it, then it links back to FR-5 and names the revisit trigger (section 8) that would reopen it.

### EP-2: .NET surface emission

#### US-201: Emit an A2UI surface for the daily forecast

- **Story**: As the weather agent, I want a successful `get_daily_forecast` call to also produce an A2UI v0.9.1 `createSurface`/`updateDataModel` message pair, so that the client can render a declarative surface instead of only text.
- **Priority**: P0 · **Estimate**: M · **Depends on**: US-103
- **Acceptance criteria**:
  - Given a successful `get_daily_forecast` call returning a `DailyForecastResult` of 1–7 days, when the chosen transport (US-103) is used, then the emitted message conforms to the A2UI v0.9.1 schema and references only basic-catalog components.
  - Given the tool-result transport is chosen, when the tool returns, then the result JSON carries both the existing plain `DailyForecastResult` (for the CopilotKit PRD's controlled tool-call widget) and the `a2ui_operations` array, additively — `SessionMapper`'s existing projection is unaffected.
  - Given a `ToolError` result (invalid coordinates, an out-of-range day count), when the tool fails, then no A2UI surface is emitted and only the existing error-text path runs.
  - Given the emitted surface, when captured as a checked-in fixture, then epic 3's rendering work can proceed against it without a live agent call.

#### US-202: Cap and validate the surface before it streams

- **Story**: As the API, I want the emitted surface capped to a bounded component count and validated against the basic-catalog schema before it leaves the server, so that a malformed or oversized surface never reaches the browser.
- **Priority**: P1 · **Estimate**: S · **Depends on**: US-201
- **Acceptance criteria**:
  - Given a 7-day forecast (the maximum `get_daily_forecast` allows), when the surface is built, then its component count stays under a documented cap (numeric value TBD, assumption recorded in section 9).
  - Given a surface that would exceed the cap, when it is built, then the server does not emit an A2UI message for that turn and falls back to the plain-text/tool-widget path, with a non-PII diagnostic logged.
  - Given the surface is built, when serialized, then it validates against the A2UI v0.9.1 basic-catalog schema before the stream writes it — a failed validation behaves like the oversize case.

#### US-203: [enabler] Persist enough of the surface to restore it on reload

- **Story**: As a returning user, I want a reopened session to still show the last forecast surface, not just its text, so that history restoration doesn't regress compared to the CopilotKit PRD's tool-call/result persistence.
- **Priority**: P2 · **Estimate**: M · **Depends on**: US-201 (and the CopilotKit PRD's session-history persistence story, external to this PRD)
- **Acceptance criteria**:
  - Given a session whose last turn included a forecast surface, when `GET api/conversations/{id}/messages` is called, then the stored message carries enough of the A2UI operations to re-render the surface, or the decision to omit it is recorded explicitly as an assumption instead of silently dropped.
  - Given a session accumulates many surface-bearing turns, when storage grows, then the added size per turn stays within a documented budget (TBD, assumption recorded).

### EP-3: Angular rendering and theming

#### US-301: Render a forecast surface with the chosen catalog renderer

- **Story**: As a user chatting with the weather agent, I want a multi-day forecast described declaratively by the agent to render as a Bootstrap-styled surface in the transcript, so that I see a richer layout than plain text.
- **Priority**: P0 · **Estimate**: M · **Depends on**: US-103
- **Acceptance criteria**:
  - Given the fixture surface from US-201, when the Angular client renders it, then every referenced component resolves to an allow-listed catalog component, and an unrecognized component type renders nothing rather than raw or unsafe markup.
  - Given the rendered surface, when inspected under `data-bs-theme="light"` and `"dark"`, then all text meets WCAG 2.2 AA contrast (≥4.5:1 body text, ≥3:1 UI components) using the existing Andes palette tokens.
  - Given the rendered surface, when the viewport is 360px, 768px, or 1280px wide, then the layout has no horizontal overflow and every interactive target is at least 24×24px.
  - Given a surface that somehow still exceeds the server-side cap (US-202), when the client encounters it, then it falls back to plain-text/tool-widget rendering instead of a partial or broken layout.

#### US-302: Sanitize and constrain surface content client-side

- **Story**: As a security-conscious operator, I want every surface's text and media re-validated on the client even though the server already caps it, so that a compromised or buggy agent response can't render unsafe content.
- **Priority**: P0 · **Estimate**: M · **Depends on**: US-301
- **Acceptance criteria**:
  - Given a surface component carrying free text, when rendered, then the text is escaped or sanitized through the same pipeline pattern the CopilotKit PRD uses for chat Markdown, not a second bespoke sanitizer.
  - Given a surface component referencing an image or other remote media URL, when rendered, then the URL is rejected unless it matches an explicit allow-list, and rejection shows a placeholder rather than a broken or silently omitted element.
  - Given the XSS/catalog fuzz corpus (extended from the CopilotKit PRD's `markdown-xss.ts` fixture with A2UI-specific payloads), when run against the renderer, then zero payloads execute script or clobber the DOM.

#### US-303: [enabler] Theme the chosen renderer onto Andes tokens

- **Story**: As a user, I want the forecast surface's colors, spacing and typography to match the rest of the chat UI rather than the renderer's own default styling, so that it doesn't look like a foreign widget.
- **Priority**: P1 · **Estimate**: S · **Depends on**: US-301
- **Acceptance criteria**:
  - Given the chosen renderer ships its own CSS variables or utility classes, when themed, then every variable maps onto `--bs-*` tokens in one partial, following the pattern the CopilotKit PRD's `_copilotkit.scss` establishes.
  - Given a component the renderer bakes `$primary` into, when the dark theme is active, then it repoints to Glacier Blue per the existing dark-theme override block in `styles.scss`.
  - Given the themed surface, when compared to the plain Bootstrap daily-forecast tool widget from the CopilotKit PRD, then spacing and type scale are visually consistent on reviewer sign-off.

### EP-4: User-action round trip

#### US-401: [enabler] Decide the API contract for an inbound A2UI action

- **Story**: As the engineering team, I want a recorded decision on how an A2UI user action reaches `agents-api`, given the input guard rejects non-empty `forwardedProps`, `state`, `context` and `tools`, so that epic 4's implementation isn't blocked on an open question.
- **Priority**: P1 · **Estimate**: S · **Depends on**: —
- **Acceptance criteria**:
  - Given the two candidates from research — a `forwardedProps.a2uiAction` convention, or a native A2UI `action` message on the AG-UI stream — when the decision is recorded, then it states which one (or neither, shipping read-only surfaces only this cycle) the guard will allow.
  - Given today's guard rejects any non-empty `forwardedProps`, `state`, `context` or `tools` (CopilotKit PRD, Design A), when the decision proposes an allowance, then it specifies the exact new field name, size cap and validation rule rather than opening the guard generally.
  - Given the decision is recorded, when a reviewer reads it, then it cross-links FR-6 and the CopilotKit PRD's guard story so a future guard change doesn't silently reopen a vector that guard was built to close.

#### US-402: Extend the input guard to accept one allow-listed action shape

- **Story**: As the API, I want the guard to accept the specific action shape from US-401 while still rejecting every other client-injected vector, so that a user's surface selection can reach the agent without reopening arbitrary tool/state/context injection.
- **Priority**: P1 · **Estimate**: M · **Depends on**: US-401
- **Acceptance criteria**:
  - Given a request carrying the allow-listed action shape and nothing else non-conformant, when the guard runs, then the request passes and the action reaches the agent.
  - Given a request carrying the allow-listed field name but an oversized or malformed payload, when the guard runs, then it is rejected 400, the same way an oversize user message is today.
  - Given a request carrying any other non-empty `tools`, `state` or `context` alongside the action, when the guard runs, then it is still rejected 400, unchanged from today.

#### US-403: Submit a surface action from the Angular client

- **Story**: As a user, I want selecting an option in a rendered surface — for example a location from a picker — to send that choice back to the agent, so that the surface is interactive rather than only a display.
- **Priority**: P1 · **Estimate**: M · **Depends on**: US-402, US-301
- **Acceptance criteria**:
  - Given a rendered surface with an actionable component, when the user selects it, then the client sends the allow-listed action shape from US-402 as the next turn.
  - Given an action is in flight, when the user selects another action or types a message, then the input disables the same way the existing chat input disables while busy.
  - Given the agent responds to the action, when the response arrives, then the transcript shows the follow-up turn like any other agent reply.

### EP-5: Decision log and revisit triggers

#### US-501: [enabler] Record the A2UI decision log and revisit triggers

- **Story**: As the engineering team, I want the spike outcomes, accepted risks and revisit triggers recorded alongside the CopilotKit PRD's decision log, so that a future contributor knows why A2UI shipped in this shape — or didn't ship — without re-deriving it.
- **Priority**: P2 · **Estimate**: S · **Depends on**: US-103
- **Acceptance criteria**:
  - Given the spikes in EP-1 conclude, when the decision log is updated, then it names the chosen renderer, the chosen transport, and every rejected alternative with its reason.
  - Given a revisit trigger from section 8 becomes true, when the log is next reviewed, then the entry states what changes as a result.
  - Given this PRD's status is Proposed and blocked, when the decision log is written, then it links back to this PRD and to `../copilotkit/learn/03-a2ui.md`.

## 8. Milestones & rollout

**Phases** (topological order of the epic dependency graph in section 7; this PRD's own status is Proposed, so phases are relative sequencing, not a schedule):
- **Phase 1 — Decide** (EP-1: US-101, US-102, US-103). Nothing else can be estimated with confidence until this phase produces a recorded renderer and transport choice.
- **Phase 2 — Build the surface, both sides** (EP-2, EP-3). The .NET emission and the Angular rendering can proceed in parallel once Phase 1 lands, each against the fixture US-201 produces; they converge for a live end-to-end forecast surface.
- **Phase 3 — Make it interactive** (EP-4). Depends on both halves of Phase 2 existing, plus its own contract decision (US-401).
- **Phase 4 — Close the loop** (EP-5). Can start as early as Phase 1's decision and is finalized once Phases 2–4 have real outcomes to record.

**Risks & mitigations**
- **No .NET A2UI SDK exists**, and the `ACTIVITY_SNAPSHOT` `a2ui-surface` path is undocumented for .NET. Mitigation: US-102 spikes both candidate transports before epic 2 commits to one; the tool-result convention is the documented fallback if `AGUIStreamOptions` doesn't support it.
- **`@a2ui/angular` has no Angular 22 support** (peers `^21.2.5`, no PR as of 2026-09-13). Mitigation: US-101 evaluates the custom-Bootstrap-on-`@a2ui/web_core` alternative in parallel so the decision isn't stalled on an upstream PR.
- **CopilotKit's A2UI middleware needs their runtime**, which this repository doesn't run. Mitigation: not pursued as the transport; CopilotKit Angular's standalone activity renderer is evaluated only as a rendering candidate (US-101), and only if it works without the runtime.
- **Spec and library churn.** A2UI is "early stage public preview" (v0.9.1 stable, v1.0 RC with breaking changes); every renderer candidate is 0.x. Mitigation: pin exact versions once chosen (matching this repository's existing preview-package discipline, ADR-0001), and record every revisit trigger below.
- **Untrusted declarative content.** A surface is still model output. Mitigation: schema validation on both sides (US-202, US-302), reused sanitization, remote-media allow-listing, and a layout-size cap — all specified as acceptance criteria, not left implicit.
- **Guard-widening risk.** Any change to the AG-UI input guard risks reopening an injection vector Microsoft's AG-UI security guidance specifically calls out. Mitigation: US-401/US-402 specify one narrow, named, size-capped field rather than a general relaxation, and US-402's acceptance criteria explicitly re-test that every other vector still rejects.

**Revisit triggers** — reopen the EP-1 decision when any of these becomes true:
- `@a2ui/angular` publishes Angular 22 support (a release or a merged PR).
- Microsoft Agent Framework's .NET hosting documents or ships A2UI emission.
- A2UI reaches v1.0 final (the RC's breaking changes are settled).
- CopilotKit Angular's A2UI activity renderer is confirmed to work without the CopilotKit runtime.

**Rollout & rollback.** Because every implementation epic is still Proposed, rollout is a future concern this PRD only frames: gate the entire A2UI rendering path behind a feature flag/config value so the CopilotKit PRD's controlled tool-call widgets remain the default, unaffected path. Rollback is disabling that flag — no data migration is needed to roll back, since a surface is additive to the existing plain-text/tool-widget rendering (and, if US-203 ships, its stored representation is likewise additive to the existing message shape).

## 9. Assumptions & open questions

**Assumptions**
- This PRD's epics 2 and 4 assume the CopilotKit PRD's Design A (the AG-UI input guard, `SessionMapper` tool-call/result projection) ships first — a reviewer who expects A2UI to ship independently of that PRD should flag this sequencing.
- "One forecast surface" (FR-1) is read as the multi-day forecast from `get_daily_forecast`; the location picker mentioned in this PRD's brief is treated as an illustrative second example for the interactive story (US-403), not a separately scoped requirement — see the open question below if that's wrong.
- No performance, cost, or timeline numbers were supplied, so sections 1, 6 and 7 mark render-latency, component-cap, and spike time-box values as TBD with an explicit best-guess rather than inventing a target that would look authoritative.
- The action-contract decision (US-401) assumes a narrow, single-purpose allow-listed field is preferable to a general relaxation of the guard — favoring security over reuse across future surface types, which may need revisiting once a second interactive surface is designed.
- Epic 5's "docs" scope is this PRD's own decision log and revisit triggers, not the routine `docs/` + `CHANGELOG.md` update that follows normal implementation work (owned by this repository's `se-technical-writer` flow) — assumed not to duplicate that flow once/if this feature is actually implemented.
- Because this PRD's status is Proposed and blocked, every estimate in section 7 is relative sizing only, not a resourcing or scheduling commitment.
- Authn/authz needs no new story because this feature introduces no new protected resource — every surface and action rides the existing Entra bearer auth, `AgentAccess` policy and per-`oid` rate limit already covering `/weather/ui`; a reviewer who considers the action contract itself a new protected surface should treat US-401/US-402 as that story.

**Open questions**
- Does CopilotKit Angular's A2UI activity renderer work without the CopilotKit runtime? (Owner: whoever runs the EP-1 spike — flagged as explicitly open in the source research digest.)
- Will `@a2ui/angular` or MAF .NET's AG-UI hosting gain the missing support (Angular 22 peers; A2UI emission) before a custom renderer/transport would otherwise need to be built and maintained? (Owner: whoever monitors the revisit triggers in section 8.)
- What numeric values should replace the TBD render-latency, component-cap, and fixture-corpus-size targets in sections 1, 6 and 7? (Owner: product/engineering lead, once the EP-1 spike gives a real baseline.)
- Should the location-picker surface be promoted to a first-class, separately scoped deliverable alongside the forecast, rather than staying illustrative? (Owner: product.)
- Does the Enterprise-tier licensing caveat on CopilotKit's `selfManagedAgents` (recorded in the CopilotKit PRD's risk log) also bear on using CopilotKit Angular's A2UI activity renderer as a rendering candidate, even without the runtime? (Owner: whoever confirms terms with CopilotKit per the CopilotKit PRD's open risk.)
