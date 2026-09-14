# Decision log

The decisions behind the [CopilotKit chat PRD](prd.md), made with the owner on **2026-09-13**, with the alternatives weighed and the risks accepted. When implementation starts, D2 and D8 become **ADR-0004** (CopilotKit chat connecting directly to the AG-UI endpoint) and D3–D5 become **ADR-0005** (server-owned AG-UI history) under `docs/adr/`.

Status for every decision: **Accepted** (for the proposed implementation).

## Decisions

| ID | Decision | Why | Consequences |
| --- | --- | --- | --- |
| D1 | **CopilotKit's prebuilt Angular chat** (`@copilotkit/angular` 0.5.2), restyled; the app shell and pages stay Bootstrap 5.3 with ng-bootstrap | The owner wants CopilotKit's chat with style overrides rather than building a transcript from scratch | Customization goes through CopilotKit's slots and CSS variables; CopilotKit's gaps (Markdown sanitization, Stop, error display, live announcements) are filled by our slot components — see [02-copilotkit.md](learn/02-copilotkit.md#what-surprised-us-and-how-the-design-handles-it) |
| D2 | **Connect the browser directly to `POST /weather/ui`** with `selfManagedAgents` and an `HttpAgent` subclass; **no Node server** | The API already authenticates with Entra ID, scopes sessions per user and rate-limits turns; a CopilotRuntime would be another deployable duplicating those duties | The API is the only server-side filter for browser traffic (hence D3); CORS must allow the app's origins; history restore and token handling live in the browser agent. License caveat — see [Accepted risks](#accepted-risks) |
| D3 | **An input guard on the AG-UI endpoint**: only the trailing user text message is used; earlier messages are ignored; non-empty `tools`, `context`, `resume`, `state` or `forwardedProps`, a non-text or non-user last message, blank or over-length text, or a non-GUID `threadId` → 400 | Today the host persists every client message and passes client tools to the model — the injection vectors in Microsoft's AG-UI security guidance | Frontend tools, shared state and human-in-the-loop are unavailable until the contract is deliberately widened; A2UI user actions will need a contract change |
| D4 | **The client mints a lowercase GUID thread id** for a new chat; the server still issues one when a caller omits it | CopilotKit binds a thread id when a chat opens and advises applications to own it; the API already accepts D and N GUIDs and scopes every thread by the caller's object id | New conversations appear in the session list only after their first reply is saved |
| D5 | **List sessions by most recent activity** (`ORDER BY c._ts DESC`) | A continued conversation should move to the top; `_ts` exists on every document and is always indexed, while `dateModified` is missing on older documents | One-second resolution; offset pages may shift while sessions update |
| D6 | **Cap a user message at 4,000 characters** (`AgentTurnLimits.MaxUserMessageLength`), enforced by the API and mirrored by the input | About a page of text: enough for questions to an agent while limiting prompt stuffing and token cost | Longer pasted content is rejected with a clear message |
| D7 | **Tool calls return on stored messages** in AG-UI's shape (`toolCalls`, `toolResults`) | A reopened conversation must redraw its widgets, and the client can load the shape without translation | Additive DTO change; one extra deserialization per message on reads |
| D8 | **Render weather results as controlled generative UI** (our Bootstrap components registered as tool-call renderers); **A2UI is documentation and a PRD only** | Controlled widgets are stable today; A2UI has no Angular 22 renderer, no .NET producer, and needs a Node middleware or contract changes | See [03-a2ui.md](learn/03-a2ui.md) and the [A2UI PRD](../a2ui/prd.md) |
| D9 | **Replace CopilotKit's Markdown renderer** with marked + DOMPurify through the assistant-message slot; raw HTML and images escaped | CopilotKit writes Markdown to `innerHTML` unsanitized | A second marked copy in the lazy chunk (about 40 kB) |
| D10 | **Provide CopilotKit on the lazy chat route** and load its CSS as a lazy bundle | The package is a single 3.66 MB module and the initial bundle is at 887.57 kB of a 900 kB warning | Two root services are re-provided on the route; a development-only inspector error remains |
| D11 | **Build on the existing MSAL Angular sign-in** and attach a token per request through the agent's `fetch` | The sign-in work is committed and owner-approved; `MsalInterceptor` only covers `HttpClient`, while AG-UI uses `fetch` | No changes to the auth design |
| D12 | **Standing styling rule**: Bootstrap 5.3 first, mobile-first, the Andes palette through Bootstrap tokens, third-party design systems themed not adopted — in `.claude/rules/ui-architecture.md` | Owner requirement for every future UI change, not only this feature | Applies automatically when files under `src/app` or `src/styles` are edited |
| D13 | **Delivery**: this effort is documentation only (this PRD package); the API epic comes first, the chat UI after it; a .NET unit-test project follows as its own story | The owner asked for a PRD before any code | Spikes run first at implementation time |

## Alternatives considered

| Alternative | Why it wasn't chosen |
| --- | --- |
| **A CopilotRuntime Node server** between the browser and the API (hosted in `agents-ui/server/`, or through Angular SSR) | Rejected by the owner: an extra deployable and operational surface the app doesn't need. It would have added header forwarding, thread replay and CopilotKit's A2UI middleware — and CopilotKit's docs present it as the license-free path |
| **Our own thin AG-UI layer** without CopilotKit (`@ag-ui/client` + NgRx SignalStore + our Bootstrap transcript) | Owner preference for CopilotKit's prebuilt chat. It remains the fallback if route-scoped CopilotKit fails ([SPK-1](verification.md#spikes)) |
| **Headless CopilotKit** everywhere | More template work for the same result; the owner prefers overriding styles |
| **A .NET backend-for-frontend** constructing AG-UI input server-side | Another hop and deployable; the input guard gives the same protection in place |
| **`@azure/msal-browser` wrapped directly** | Superseded by the committed MSAL Angular sign-in |
| **`ngx-markdown`** | Lists `zone.js` as a non-optional peer in a zoneless app; the chat needs a renderer it controls inside CopilotKit's slot anyway |
| **The official `@a2ui/angular` renderer now** | Peers Angular `^21.2.5`; the app is on Angular 22 |
| **Rejecting any request that carries earlier messages** | CopilotKit-style clients resend history on every run; ignoring it keeps any AG-UI client working without persisting it |
| **Ordering sessions by `dateModified`** | Missing on documents written before its rename; `ORDER BY` over a missing property can drop items |

## Accepted risks

| Risk | Why it's accepted | Mitigation |
| --- | --- | --- |
| **License terms for self-managed agents.** CopilotKit's documentation says "`selfManagedAgents` is part of CopilotKit's Enterprise Intelligence tier. Talk to an engineer about licensing for production use." | The npm package is MIT, the published code performs no license check, and the same page lists `selfManagedAgents` as the production option for agents you manage | **Confirm terms with CopilotKit before production** |
| **Direct browser access to the AG-UI endpoint.** Microsoft's guidance recommends a trusted frontend server | The API authenticates every call, scopes sessions per user and rate-limits turns; the guard performs the input validation the guidance requires for directly exposed servers | [API-1 to API-13](verification.md#api-acceptance-checks); API-1 joins the hosting-package upgrade checklist |
| **Preview and 0.x dependencies**: AG-UI 1.0 draft, `@ag-ui/client` 0.0.59, CopilotKit Angular 0.x with exact pins, Agent Framework hosting preview | Nothing stable offers the same capability | Exact pins; revisit triggers below |
| **No keep-alive during long silent tool phases** behind Azure ingress (about 240 s idle timeout) | The weather tools are fast | [SPK-9](verification.md#spikes); a server keep-alive if long-running tools arrive |

## Revisit triggers

- CopilotKit clarifies or changes self-managed agent licensing.
- CopilotKit ships a sanitized or pluggable Markdown renderer ([PR #5147](https://github.com/CopilotKit/CopilotKit/pull/5147)) or a Stop button ([#5428](https://github.com/CopilotKit/CopilotKit/issues/5428)) — delete the matching custom code.
- `@a2ui/angular` supports Angular 22, the Agent Framework emits A2UI on .NET, or A2UI 1.0 is final — start the [A2UI PRD](../a2ui/prd.md).
- AG-UI 1.0 is ratified, or the hosting package emits `RUN_ERROR` on stream failures — revisit the error handling in `MafAgent`.
- Long-running tools are added to any agent — revisit keep-alive and the ingress timeout.
- The app needs frontend tools, shared state or human-in-the-loop — widen the input guard deliberately.
