# Verification

How the implementation of this PRD is proven to work. The PRD's acceptance criteria refer to the IDs on this page. Nothing here runs yet: this package is documentation only.

Verification has three layers:

1. **Spikes** (`SPK-*`) run first. They settle the assumptions the designs rest on, and each has a fallback if it fails.
2. **Gates** (`GATE-*`) are the build, lint and test commands every change must pass.
3. **Acceptance checks** (`API-*`, `UI-*`) exercise the finished feature end to end: the API with HTTP requests, the chat in a real browser.

## Spikes

Run spikes before building on the assumption each one tests. Record the outcome — pass, or the fallback taken — in the implementation PR.

| ID | Spike | Passes when | If it fails |
| --- | --- | --- | --- |
| SPK-1 | CopilotKit provided on the chat route with `selfManagedAgents` (no `runtimeUrl`), lazy CSS, tool renderers from config | The initial chunk grows by at most 1 kB and contains no `@copilotkit`, `@ag-ui`, `katex` or `highlight.js`; no `NullInjectorError`; no `/info` request; the agent store resolves immediately; text streams; a server tool call renders our widget; `chat.css` loads only on `/chat`; CDK tooltips position correctly; the empty-state slot renders; the production console is clean. Record the lazy chunk sizes and one real `TOOL_CALL_RESULT` payload | **Headless fallback:** drive `CopilotKitCore` directly and render our own Bootstrap transcript. Input, markdown, widgets, stores and theme carry over unchanged |
| SPK-2 | History restore through `MafAgent.connect()` | Each `[threadId]` binding connects exactly once; core applies the `RUN_STARTED` · `MESSAGES_SNAPSHOT` · `RUN_FINISHED` sequence (note whether it clears messages first); a reload shows text plus completed widgets; a new GUID opens empty with no alert; a deleted thread opens empty | **Client restore fallback:** `provideCopilotChatConfiguration`, `setActiveThreadId(id, { explicit: false })`, then `agent.setMessages(...)` after CopilotKit's clearing effect |
| SPK-3 | Tokens through the agent's `fetch` wrapper | The wrapper is used for both run and connect; core's header merge never removes the `Authorization` header the wrapper sets; an expired token is renewed silently; `InteractionRequiredAuthError` redirects once and the sign-in loop breaker holds | Set headers with `CopilotKit.updateRuntime({ headers })` before every submit and thread switch |
| SPK-4 | The API contract from the browser | CORS preflight passes for `POST /weather/ui` with `Authorization`, `Content-Type` and `Accept`; SSE streams cross-origin; the trimmed request body is accepted; problem bodies are readable for 400, 401, 409 and 429; `Retry-After` is readable | Adjust the CORS policy's exposed headers and the agent's `Accept` header |
| SPK-5 | Theme isolation and the assistant toolbar | A computed-style diff of CopilotKit's markup shows only the mapped variables changing; text contrast is AA in both themes at 360, 768 and 1280 px; widgets look identical to plain Bootstrap; regenerate and edit actions are hidden | Load Bootstrap into a cascade layer with `meta.load-css`; replace the assistant toolbar |
| SPK-6 | Access-token shape | A real token for the app registration has `ver` 2.0, `aud` equal to the client GUID, and `scp` containing `access_as_user` when requested through `<clientId>/.default` | Set `requestedAccessTokenVersion: 2`; grant the app its own delegated permission |
| SPK-7 | Stop, switching threads mid-run, and the Cosmos save after `RUN_FINISHED` | No 409 on the next send after Stop; a new session appears in the list within 2 s | A short send grace period after Stop; insert the session optimistically |
| SPK-8 | Signal Forms textarea and streaming markdown cost | `[formField]`, IME composition and auto-grow all work; a 10 kB reply streamed at 50 tokens/s with 4× CPU throttling keeps long tasks under 50 ms | A plain signal-bound textarea; coalesce markdown rendering to animation frames |
| SPK-9 | A long silent stream behind Azure ingress | An idle stream longer than 240 s survives, or its failure behavior is documented | Accept the limit while tools stay fast; open a follow-up for a server keep-alive |

## Gates

| ID | Commands | Passes when |
| --- | --- | --- |
| GATE-API | `dotnet build agents-api/Andes.Agents.slnx` and `dotnet format agents-api/Andes.Agents.slnx --verify-no-changes` | Both succeed with warnings treated as errors |
| GATE-UI | From `agents-ui/`: `npm run lint`, `npm run format:check`, `npm test -- --watch=false`, `npm run build`, `npm run check:initial-chunk`, `npm ls rxjs` | All succeed; one `rxjs` (7.8.2) is installed; the initial total stays within the 900 kB warning budget; the size of `chat.css` and the largest lazy chunk are recorded |

## API acceptance checks

Send each request to `POST /weather/ui` with a valid token and `Accept: text/event-stream` unless the row says otherwise. Every rejection must carry an `application/problem+json` body, because the API writes problem bodies whatever the `Accept` header says.

| ID | Request | Expected |
| --- | --- | --- |
| API-1 | A single `system` message | 400, `errors.messages`; nothing is stored |
| API-2 | A `user` message followed by an `assistant` message | 400, `errors.messages` |
| API-3 | A GUID `threadId`; history with a system message, an assistant tool call and a tool result, then a trailing user message "What's the weather in Seattle right now?" | 200 SSE; `RUN_STARTED.threadId` equals the GUID; only the Seattle message is stored |
| API-4 | `tools: [{ name: "reveal_secrets", … }]` | 400, `errors.tools` |
| API-5 | A user message of 4,001 characters, then one of 4,000 | 400 `errors["messages.content"]`, then 200 |
| API-6 | `threadId` of `has/slash` or a braced GUID, then an N-form GUID | 400 `errors.threadId`, then 200 |
| API-7 | Content with text and image parts, then text parts only | 400 ("must be text"), then 200 |
| API-8 | Blank text; `messages: []`; an empty body; malformed JSON | 400 with a body in every case |
| API-9 | The 31st turn within a minute; a request with no token | 429 with a body and a `Retry-After` header readable cross-origin; 401 as problem+json |
| API-10 | A turn on an older session A after creating session B; a stored session document without `dateModified` | A lists before B; the legacy document is still listed |
| API-11 | `GET api/conversations/{id}/messages` after a turn that called a tool | Assistant items carry `toolCalls[].type == "function"`; tool items carry `toolResults[].toolCallId` matching a call |
| API-12 | A CORS preflight from the SPA origin for `POST /weather/ui` with `Authorization`, `Content-Type` and `Accept` | 204 with the allow headers; a disallowed origin gets no CORS headers |
| API-13 | Search the API logs for text a caller sent (`reveal_secrets`, `has/slash`, the prompt) | No matches |

## Chat UI acceptance checks

Run these against a production build of `agents-ui` served by a static host that returns `index.html` for the sign-in redirect bridge at `/auth` (with `Cache-Control: no-store` and no `Cross-Origin-Opener-Policy` header) and serves `config.json` with `Cache-Control: no-cache`.

No browser automation MCP is available in this repository's environment, so drive headless Microsoft Edge over the Chrome DevTools Protocol from a Node script. Use a fresh profile per run that has been signed in once, and set `prefers-color-scheme` and `prefers-reduced-motion` explicitly through emulation. Run every check at 360×800, 768×1024 and 1280×800, in the light and dark themes.

| ID | Check | Passes when |
| --- | --- | --- |
| UI-NET | Network traffic during a chat | Every `POST /weather/ui` carries `Authorization: Bearer` and exactly one message with empty `tools`; no `/info` or runtime request is made; opening a thread issues exactly one `GET …/messages` |
| UI-THEME | Theme and layout | Text contrast is at least 4.5:1 and UI component contrast at least 3:1 for user and assistant text, links, code, the input, buttons, cards, alerts and the active session; the font equals `--bs-body-font-family`; the `.dark` class is present exactly when the dark theme is; interactive targets are at least 24×24 px; the Reboot computed-style diff is clean |
| UI-XSS | A prompt that makes the agent repeat the XSS corpus (`<img onerror>`, `<script>`, `javascript:` and `data:` links, `<svg onload>`, `id`/`name` clobbering, a remote image, a malicious code-fence language) | No dialog opens; no forbidden element is rendered; every link is `http(s)` or `mailto` with `rel` containing `noopener`; no sanitizer warnings or CSP violations in the console |
| UI-STOP | Stop during a long reply; five rapid Enter presses | The input is enabled again within 1 s of Stop; the rapid presses send one request and cause no 409 |
| UI-TOKEN | Expire the cached MSAL access token, then send and switch threads | A silent token request happens before the next run request and before the next history request |
| UI-RESTORE | Reload an existing session that includes tool calls | The full transcript renders with every widget in its completed state |
| UI-SESSIONS | Session list and keyboard use | A new chat moves to the top after its first reply; delete works after confirmation; a keyboard-only pass reaches every control; focus lands in the textarea after a thread switch; the off-canvas list traps and restores focus |
| UI-MOTION | Reduced motion and console | With `prefers-reduced-motion: reduce`, animations and transitions compute to near zero; the production build logs no console errors |
