# PRD: Shared Interactive Agent Console

## 1. Overview

**Problem**: The repository's only sample, `samples/01-get-started/HelloAgent/Program.cs`, is 91 lines, of which roughly 55 are sample-agnostic plumbing — a `ConfigurationBuilder`, a `FirstNonBlank` key/model resolver, `OpenAIClient` → `GetResponsesClient()` → `AsAIAgent(...)` behind an `OPENAI001` pragma, `Console.CancelKeyPress` wiring, and an `OperationCanceledException` → exit-code-130 path. Only about 12 lines teach an Agent Framework concept. The `02-agents`, `03-workflows`, `04-hosting`, and `05-end-to-end` categories are empty, so every sample that follows would retype that plumbing plus a multi-turn console loop that does not exist anywhere in the repo. Separately, samples show nothing of what the agent is *doing* — function calls, MCP round-trips, and nested agent runs are invisible until the final text arrives — and token cost is never shown at all.

**Solution**: A shared class library, `MafAgents.Interactive`, under `src/`, referenced by `ProjectReference` from each sample. It owns configuration, tracked-agent construction, a live activity tree with streamed output, token accounting, a multi-turn REPL with cancellation, and MCP wiring, so a sample's `Program.cs` contains only what it teaches. One new showcase sample, `samples/02-agents/TrackedAgent`, consumes it; `HelloAgent` stays raw and unchanged as the deliberate no-dependency baseline.

**Success criteria**:

- Plumbing in `samples/02-agents/TrackedAgent/Program.cs` — configuration, client construction, agent construction, cancellation wiring, and the turn loop — is **≤ 12 non-blank lines**, against 55 in `HelloAgent` (a ≥ 78% reduction), counted by hand at PR review against the two files.
- Configuration resolution, Responses-client construction, and Ctrl+C handling exist in **exactly one place** outside `HelloAgent`: `rg "OPENAI_API_KEY|CancelKeyPress|GetResponsesClient" --glob '!**/bin/**' --glob '!**/obj/**'` returns hits only in `src/MafAgents.Interactive/` and `samples/01-get-started/HelloAgent/`.
- For a turn that invokes at least one function tool, one MCP tool, and one nested agent, **100% of those invocations** appear in the console as a card carrying a type badge (`fn` / `mcp` / `agent`), a terminal status, and an elapsed time, with each card's first appearance no later than one redraw interval (≤ 100 ms) after its start event — verified by the scripted `TrackedAgent` run on three consecutive executions.
- Every completed turn prints an input/output/total token footer, and the session footer equals the arithmetic sum of the per-turn totals — asserted by an xUnit test over `SessionUsage` and confirmed visually in the scripted run.
- `dotnet build`, `dotnet format --verify-no-changes`, and `dotnet test` all pass with **zero warnings and zero suppressions** for the new projects, under the repository's existing `TreatWarningsAsErrors` + `GenerateDocumentationFile` settings.

End to end, a reader clones the repo, sets `OpenAI:ApiKey`, and runs `dotnet run --project samples/02-agents/TrackedAgent`. They get a prompt; they ask a question; the console draws a live tree as the agent works — a `fn` card for the weather function, an `mcp` card whose progress bar advances as the MCP server reports progress, an `agent` card for the nested reviewer agent — each with its own elapsed time, while the answer streams below. When the turn ends the tree collapses to a static summary with a token footer, the running session total ticks up, and the prompt returns for the next turn. Ctrl+C cancels the turn in flight; Ctrl+C at an idle prompt exits with code 130.

## 2. Goals & non-goals

**Goals**

- A new sample author writes only the code that teaches the concept — tools, instructions, prompts — and no configuration, client, cancellation, or loop plumbing.
- A reader can see every tool call, MCP round-trip, and nested agent run as it happens, rather than inferring it from the final answer.
- Token consumption is visible per turn and cumulatively per session, so a reader knows what a run costs before they copy it.
- Plumbing bugs are fixed in one file rather than in every sample.
- The library meets the repository's library-grade bar: XML docs on every public member, xUnit coverage of the deterministic parts, and a clean `dotnet format --verify-no-changes`.

**Non-goals**

- A menu-driven playground or launcher. Each sample stays an independently runnable console app started with `dotnet run --project samples/...`.
- Reflection-based or attribute-based sample discovery.
- Any change to `samples/01-get-started/HelloAgent`. It deliberately demonstrates the bare framework with no shared dependency, and its four-place hardcoded key-setup message stays as it is.
- Workflow rendering (`03-workflows`), hosting (`04-hosting`), or end-to-end (`05-end-to-end`) samples. The library is built so those can adopt it later; this project does not write them.
- Persisting sessions to disk, resuming across process restarts, or any session store.
- Dollar-cost estimation from token counts (needs a price table that goes stale).
- Telemetry export, OpenTelemetry wiring, or any log sink. Output is the terminal.
- Multi-user, hosted, or web UI of any kind.
- Any prerelease package, and any provider other than OpenAI — both are settled by `docs/adr/0001-openai-as-model-provider.md`.
- Unit-testing the live-model path. Per `.claude/CLAUDE.md`, samples that call a live model are not unit-tested; only the library's deterministic units are.

## 3. Users & access

**Personas**

- **Sample reader**: a .NET developer evaluating Microsoft Agent Framework who clones the repo, runs one sample, and reads its `Program.cs` in a sitting. They need the teaching code to dominate the file and the runtime behavior to be legible.
- **Sample author**: the repository owner or a contributor adding a sample under `samples/<NN-category>/`. They need one call that yields a working, observable console so the diff of a new sample is the concept and nothing else.
- **Reviewer**: whoever reviews the sample PR. They need the build, format, and test gates to fail loudly rather than relying on inspection.

There is no authentication or authorization model: every sample is a single-user, single-process console app. The only credentialed resources are the OpenAI API (an API key) and any MCP server the sample connects to; both are covered under Security in section 6 and by US-103 and US-501.

## 4. Functional requirements

| ID | Requirement | Priority | Epic(s) |
| --- | --- | --- | --- |
| FR-1 | A sample builds a fully tracked `AIAgent` — Responses client, tool-tracking middleware, function invocation, tools, instructions — in a single call. | P0 | EP-1 |
| FR-2 | The OpenAI key and model resolve from user-secrets then environment, accepting `OpenAI:ApiKey` / `OPENAI_API_KEY` / `OpenAI__ApiKey`, treating a blank value as absent, and binding user-secrets to the *calling sample's* assembly. | P0 | EP-1 |
| FR-3 | The chat-client pipeline preserves the ordering invariant `UseToolTracking` → `UseFunctionInvocation`, and `ChatClientAgent` must not insert a second `FunctionInvokingChatClient`. | P0 | EP-1 |
| FR-4 | Synthetic progress and usage content must not corrupt `AgentSession` history: a session survives serialization and a subsequent turn after a tool-calling turn. | P0 | EP-1 |
| FR-5 | While a turn runs, the console renders a live activity tree: one card per activity with a `fn` / `mcp` / `agent` badge, sub-status, nesting under its parent, elapsed time, and a terminal status. | P0 | EP-2 |
| FR-6 | The answer text streams into the same live region as it arrives, with redraws throttled to a configurable interval (default 80 ms). | P0 | EP-2 |
| FR-7 | When the console is non-interactive or output is redirected, the program produces plain sequential output and exits normally instead of throwing. | P0 | EP-2, EP-4 |
| FR-8 | All model-, tool-, and server-supplied text is escaped before it reaches the markup renderer. | P0 | EP-2 |
| FR-9 | Failed and cancelled activities are visually distinct from succeeded ones, and a static final snapshot of the tree remains on screen after the live region closes. | P1 | EP-2 |
| FR-10 | Each completed turn prints an input / output / total token footer. | P0 | EP-3 |
| FR-11 | A running session total accumulates across turns and is shown with each footer. | P1 | EP-3 |
| FR-12 | Activity cards show per-tool token counts when the underlying report supplies them, and omit the field when it does not. | P2 | EP-3 |
| FR-13 | The console runs a multi-turn REPL over a single `AgentSession`, so turn *n* sees the context of turns 1..*n*-1. | P0 | EP-4 |
| FR-14 | Ctrl+C during a turn cancels that turn and returns to the prompt; Ctrl+C at an idle prompt exits with code 130. | P0 | EP-4 |
| FR-15 | A sample customizes agent name, instructions, tools, banner, greeting, redraw interval, and usage display through `AgentConsoleOptions` without touching library code. | P1 | EP-4 |
| FR-16 | The library connects to an MCP server, lists its tools, and attaches them to the agent with tracking; a server that fails to start produces an actionable error and does not abort the console. | P1 | EP-5 |
| FR-17 | MCP `notifications/progress` messages surface in the activity tree as an advancing progress indicator on the owning card. | P1 | EP-5 |
| FR-18 | `samples/02-agents/TrackedAgent` runs end to end with a function tool, a nested agent tool, and an MCP tool, requiring no install beyond the .NET SDK and an API key. | P0 | EP-6 |
| FR-19 | The new `src/` and `tests/` projects build clean under `TreatWarningsAsErrors` with XML docs on every public member, use central package management with no `Version` attributes, and add no prerelease package. | P0 | EP-1 |
| FR-20 | No API key, key fragment, or key-bearing configuration value is ever written to the console, the activity tree, or an exception message. | P0 | EP-1 |

## 5. User experience

**Entry points & first-time flow**

The only entry point is `dotnet run --project samples/<NN-category>/<SampleName>`. On start the console resolves configuration; a missing key stops the program with a message naming the *calling sample's* project path (`dotnet user-secrets set "OpenAI:ApiKey" "sk-..." --project samples/02-agents/TrackedAgent`) and exits with a non-zero code, without a stack trace. With a key present, the sample prints its banner — name, model, and a one-line description of what it demonstrates — then the prompt.

**Core experience**

1. The user types a prompt and presses Enter.
2. A live region opens. Activity cards appear as the agent works: a badge (`fn`, `mcp`, `agent`), the activity name, a sub-status line, a progress bar when the source reports progress, elapsed time, and nesting for activities started inside another.
3. Answer text streams beneath the tree as it arrives.
4. When the turn completes, the live region closes, leaving a static snapshot of the tree, the full answer, and a token footer: input / output / total for the turn, plus the running session total.
5. The prompt returns. The next turn shares the same `AgentSession`, so follow-ups can use pronouns.
6. `exit`, `quit`, or EOF ends the session with exit code 0.

**Edge cases & UI states**

- *Empty input*: blank or whitespace-only input re-prompts without calling the model.
- *Non-interactive console* (`AnsiConsole.Profile.Capabilities.Interactive` is false, output redirected, or CI): no live region and no Spectre `TextPrompt`; activities print as one line each on completion, the answer prints once, and prompts are read with `Console.ReadLine`.
- *Tool failure*: the card shows a failed status and the error message; the turn continues if the agent can recover and otherwise ends with the model's error text, never with an unhandled exception.
- *Cancellation*: the in-flight card is marked cancelled, the live region closes cleanly, and the terminal is left with the cursor restored and no partial control sequences.
- *MCP server unavailable*: a warning names the failing command, the console starts with the remaining tools, and no MCP cards appear.
- *No usage reported*: the footer shows `n/a` for the missing figure rather than `0`, and the session total is unchanged.

**UI/UX highlights**

- Redraw is throttled (default 80 ms) because snapshots arrive per text delta; without it the region flickers.
- Colour is a supplement, never the sole carrier of meaning: every status also has a text label, so a monochrome terminal, `NO_COLOR`, or a screen reader consuming redirected output loses nothing.
- Markup escaping is mandatory on untrusted text, so a tool result containing `[red]` renders literally instead of corrupting or crashing the render.
- The non-interactive path is the accessibility path: piping output yields a complete, linear transcript of the same information.

## 6. Technical considerations

**Integration points** (all verified in this repository or in official docs)

- `samples/01-get-started/HelloAgent/Program.cs` — the source of the plumbing being extracted, and the one file this project must not change. Its `FirstNonBlank` resolver, `#pragma warning disable OPENAI001` scope, `CancelKeyPress` handler, and 130 exit path are the behaviours `MafAgents.Interactive` must reproduce.
- `Directory.Packages.props` — central package management with `CentralPackageTransitivePinningEnabled=true`. Every new version lands here; no `.csproj` carries a `Version` attribute. `Microsoft.Extensions.AI.Abstractions` must be pinned explicitly because transitive pinning is on.
- `Directory.Build.props` — `net10.0`, `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`, `AnalysisLevel=latest-recommended`, and `GenerateDocumentationFile`, which makes CS1591 a build error for the library.
- `.editorconfig` — promotes IDE0055 (formatting), IDE1006 (naming), IDE0005 (unused usings), and CA1507 to `warning`, which warnings-as-errors turns into build failures.
- `maf-agents.slnx` — gains `/src/` and `/tests/` solution folders alongside the existing `/samples/01-get-started/` folder.
- `docs/adr/0001-openai-as-model-provider.md` — binds the project to the OpenAI Responses client and the stable-only channel.
- Microsoft Agent Framework 1.17.0: `AIAgent`, `ChatClientAgent`, `AgentSession`, `AIAgent.CreateSessionAsync`, `AIAgent.RunStreamingAsync`, `AIAgent.SerializeSessionAsync` / `DeserializeSessionAsync`, and `AgentResponseExtensions.AsChatResponseUpdatesAsync()` from `Microsoft.Agents.AI.Abstractions`. The `AsChatResponseUpdate` docs state the conversion is a shallow copy with `Contents` shared by reference, so tracking content survives the bridge into Andes' `ToStatusSnapshotsAsync()`.
- `ChatClientAgent`'s constructor documentation states the `IServiceProvider` parameter "is only relevant when the `IChatClient` doesn't already contain a `FunctionInvokingChatClient` and the `ChatClientAgent` needs to insert one" — so a pre-built pipeline preserves the tool-tracking ordering invariant. `ChatClientAgentOptions.UseProvidedChatClientAsIs = true` is the explicit belt-and-braces switch if detection ever proves unreliable.
- `Microsoft.Extensions.AI.OpenAI` supplies `.AsIChatClient(model)` on the Responses client, which is what makes the Andes middleware chain reachable at all; `OpenAIClient.GetResponsesClient()` still needs the scoped `OPENAI001` pragma used in `HelloAgent`.
- Andes (`Andes.Extensions.AI`, `.Agent`, `.Mcp`, `.UI` — all 0.5.0, MIT, `net10.0`): `UseToolTracking`, `UseAgentToolClassification`, `UseMcpToolClassification`, `ToStatusSnapshotsAsync`, `AssistantStatusSnapshot` / `AssistantActivity`, `ToolTrackingOptions`, `IChatProgressObserver`, `AssistantStatusReducer`, and `.WithTracking(client)` for MCP.
- ModelContextProtocol 2.1.0 (GA): `McpClient.CreateAsync(transport)` — the 2.x replacement for 1.x's `McpClientFactory.CreateAsync` — over any `IClientTransport` (stdio, HTTP), then `ListToolsAsync()`, with `McpClientTool` values cast to `AITool` for the agent's tool list.
- Spectre.Console 0.57.2 core: `AnsiConsole.Live`, `AnsiConsole.Profile.Capabilities.Interactive`, `Markup.Escape`. `Spectre.Console.Cli` is not needed.

Package versions to add to `Directory.Packages.props`: `Andes.Extensions.AI` 0.5.0, `Andes.Extensions.AI.Agent` 0.5.0, `Andes.Extensions.AI.Mcp` 0.5.0, `Andes.Extensions.AI.UI` 0.5.0, `Microsoft.Extensions.AI` 10.8.3, `Microsoft.Extensions.AI.Abstractions` 10.8.3, `Microsoft.Extensions.AI.OpenAI` 10.8.3, `ModelContextProtocol` 2.1.0, `Spectre.Console` 0.57.2. All are stable; the Andes 0.5.0 nuspecs declare `Microsoft.Extensions.AI` ≥ 10.8.3, `Microsoft.Agents.AI` ≥ 1.17.0, and `ModelContextProtocol.Core` ≥ 2.1.0, all satisfied by the versions above.

**Data storage & privacy**

Nothing is persisted. `AgentSession` lives in process memory and is discarded at exit; v1 writes no session, transcript, or usage file. Prompts, tool arguments, tool results, and answers are rendered to the terminal only. Agent Framework's own guidance notes that a serialized session may contain conversation content and PII — the spike in US-102 serializes only to prove the session is not corrupted, and the result is not written to disk.

**Security**

- The API key is read from `dotnet user-secrets` (stored outside the repo tree) or the environment, per the repository's documented carve-out from the `DefaultAzureCredential` standard. It is never rendered, never included in an exception message, and never part of a status snapshot.
- Tool arguments are model-chosen and must be treated as untrusted input — the `ChatClientAgent` constructor docs say so explicitly. The library escapes them before rendering (FR-8) and executes only tools the sample registered.
- MCP servers are third-party processes. `McpToolSource` requires an explicit command from the sample rather than discovering servers, and the showcase sample ships an in-process demo server so `dotnet run` never launches an unvetted external binary.
- `NuGetAuditMode` findings stay warnings by repository decision (`WarningsNotAsErrors` in `Directory.Build.props`); adding nine packages widens the transitive surface that audit covers, which is a monitoring obligation, not a build gate.

**Scalability & performance**

Single user, single process, one in-flight turn. The performance constraints that matter are render-side: snapshots arrive per text delta, so redraws are throttled to one per 80 ms (≤ 12.5 redraws/s) and the tree is re-rendered from the latest snapshot rather than diffed. Token accounting is O(1) per turn. The activity tree is bounded by the number of tool calls in a turn; no history trimming is in scope.

**AI system requirements**

- Model: whatever `OpenAI:Model` resolves to, default `gpt-4o-mini`, matching `HelloAgent`.
- Tools the showcase must exercise: at least one local `AIFunction`, one nested agent exposed via `.AsAIFunction()`, and one MCP tool that reports progress.
- Evaluation: there is no model-quality benchmark and none is proposed — the library does not change what the model says. Verification is (a) xUnit over the deterministic units (configuration precedence, usage aggregation, markup escaping, snapshot merge) and (b) a scripted manual run of `TrackedAgent` whose pass threshold is: on 3 consecutive runs, all three badge types appear, every card reaches a terminal status, the per-turn footer shows a non-zero total, and the session total equals the sum of the per-turn totals.

## 7. Epics & user stories

| ID | Epic | Goal | Priority | Estimate | Depends on |
| --- | --- | --- | --- | --- | --- |
| EP-1 | Tracked agent foundation | A sample can build a configured, fully tracked agent in one call | P0 | L | — |
| EP-2 | Live activity tree | A reader sees every tool call, MCP round-trip, and nested run as it happens | P0 | L | EP-1 |
| EP-3 | Token usage & session cost | A reader sees what each turn and the whole session cost in tokens | P1 | M | EP-1, EP-2 |
| EP-4 | Interactive console loop | A reader holds a multi-turn conversation and can cancel cleanly | P0 | M | EP-1, EP-2 |
| EP-5 | MCP tool source | A sample attaches MCP server tools and sees their progress in the tree | P1 | M | EP-1, EP-2 |
| EP-6 | TrackedAgent showcase sample | The library is proven by a runnable sample under `02-agents` | P0 | M | EP-2, EP-4 |

### EP-1: Tracked agent foundation

#### US-101: `[enabler]` Package and project scaffolding

- **Story**: `[enabler]` Create `src/MafAgents.Interactive` and `tests/MafAgents.Interactive.Tests`, register their package versions centrally, and add them to the solution. Unblocks every story in this PRD.
- **Priority**: P0 · **Estimate**: S · **Depends on**: —
- **Acceptance criteria**:
  - Given the nine new package versions added to `Directory.Packages.props`, when `dotnet build` runs, then it succeeds and no `.csproj` in the repository contains a `Version` attribute on a `PackageReference`.
  - Given the restore graph, when it is inspected, then it contains no prerelease package, and `Microsoft.Extensions.AI.Abstractions` 10.8.3 is pinned explicitly so transitive pinning cannot resolve a lower version.
  - Given a public type with no XML doc comment added temporarily to the library, when `dotnet build` runs, then it fails with CS1591 — proving the documentation gate is active on the new project.
  - Given `maf-agents.slnx`, when it is opened, then `/src/` and `/tests/` folders exist containing the two new projects, and `dotnet format --verify-no-changes` passes.

#### US-102: `[enabler]` Spike — does progress content leak into `AgentSession` history?

- **Story**: `[enabler]` Determine within one working day whether `ChatClientAgent` aggregates Andes' in-band `ChatProgressContent` / `UsageReportContent` into `AgentSession` history, and fix the design of `TrackedAgentFactory` accordingly. Blocks US-104 and, through it, EP-2 through EP-6.
- **Priority**: P0 · **Estimate**: M · **Depends on**: US-101
- **Acceptance criteria**:
  - Given a pipeline built as `.UseToolTracking(...)` then `.UseFunctionInvocation()`, when a turn that invokes a function tool runs against an `AgentSession`, then it is recorded whether progress or usage content is present in the session's aggregated history.
  - Given that session, when `agent.SerializeSessionAsync(session)` is called, then it either returns a `JsonElement` without throwing — confirming the in-band path is safe — or the failure is captured and the fallback is adopted.
  - Given the same session, when a second turn is sent, then the provider accepts the request without a 4xx error, or the fallback is adopted.
  - Given the fallback is adopted, when the spike closes, then `ToolTrackingOptions.EmitProgressContent` and `EmitUsageReportContent` are set to `false` and the out-of-band route (`IChatProgressObserver` → `Channel<ChatProgressUpdate>` → `AssistantUiEvent` → `AssistantStatusReducer`) is the design US-104 implements.
  - Given the time box expires without a conclusive result, when the day ends, then the out-of-band fallback is chosen by default so downstream stories are never blocked on further investigation.

#### US-103: Resolve the OpenAI key and model from user-secrets and the environment

- **Story**: As a sample author, I want the library to resolve the API key and model for me so that I never copy the resolver or the "how to set your key" message into a new sample.
- **Priority**: P0 · **Estimate**: S · **Depends on**: US-101
- **Acceptance criteria**:
  - Given a key stored under `OpenAI:ApiKey` in the calling sample's user-secrets, when the sample starts, then `OpenAIConfiguration` resolves it — proving user-secrets binds to the caller's `UserSecretsId` via a supplied `Assembly` or `TProgram`, not the library's.
  - Given no user-secret but `OPENAI_API_KEY` set in the environment, when the sample starts, then that value is used; given both, then user-secrets wins.
  - Given `OPENAI_API_KEY` set to an empty or whitespace string, when the sample starts, then it is treated as absent — matching the unpopulated-CI-secret case `HelloAgent` already handles.
  - Given no key from any source, when the sample starts, then the program exits non-zero with a message naming the caller's project path taken from options (for example `--project samples/02-agents/TrackedAgent`), with no stack trace and no hardcoded `HelloAgent` path.
  - Given a resolved key, when any console output, activity card, or exception message is produced, then the key value appears in none of them — asserted by a test that resolves a sentinel key and scans captured output for it.
  - Given no model configured, when the sample starts, then the model defaults to `gpt-4o-mini`, overridable by `OpenAI:Model` or `OPENAI_MODEL`.

#### US-104: Build a tracked agent in one call

- **Story**: As a sample author, I want `TrackedAgentFactory` to hand me a ready `AIAgent` with tracking already wired so that my sample's setup is one statement.
- **Priority**: P0 · **Estimate**: M · **Depends on**: US-102, US-103
- **Acceptance criteria**:
  - Given a name, instructions, and a tool list, when the factory is called, then it returns an `AIAgent` built on `OpenAIClient().GetResponsesClient().AsIChatClient(model)` with `UseToolTracking` applied before `UseFunctionInvocation`, and both `UseAgentToolClassification` and `UseMcpToolClassification` enabled.
  - Given the returned agent, when a turn invokes a registered function tool, then the tool executes exactly once — proving `ChatClientAgent` did not insert a second `FunctionInvokingChatClient` around the pre-built pipeline.
  - Given a `CancellationToken` passed to the factory's run path, when the token is cancelled mid-run, then `OperationCanceledException` propagates rather than being swallowed.
  - Given the `OPENAI001` evaluation-only attribute on `GetResponsesClient()`, when the library builds, then the suppression is a `#pragma` scoped to those lines only, not a project-wide `NoWarn`.
  - Given a `null` options argument, when the factory is called, then it throws `ArgumentNullException` naming the parameter.

### EP-2: Live activity tree

#### US-201: Render a live activity tree while the agent runs

- **Story**: As a sample reader, I want to see each tool call appear and complete while the agent works so that I can tell what the agent is actually doing.
- **Priority**: P0 · **Estimate**: L · **Depends on**: US-104
- **Acceptance criteria**:
  - Given a turn that invokes a function tool, when the tool starts, then a card appears in the live region within one redraw interval carrying a `fn` badge, the tool name, and a running elapsed time.
  - Given an activity started inside another activity, when it is rendered, then it appears nested under its parent rather than as a sibling.
  - Given an activity that reports a sub-status or a progress fraction, when the report arrives, then the card's sub-status line or progress bar updates without the card being duplicated.
  - Given a completed activity, when the turn continues, then the card shows a terminal status and a final elapsed time and stops updating.
  - Given a snapshot stream that emits per text delta, when the turn runs, then redraws occur no more often than once per configured interval (default 80 ms), measured by counting render calls in a unit test over the throttle.

#### US-202: Stream the answer into the live region

- **Story**: As a sample reader, I want the answer to appear as it is generated, below the activity tree, so that I see progress rather than a frozen screen.
- **Priority**: P0 · **Estimate**: M · **Depends on**: US-201
- **Acceptance criteria**:
  - Given a turn producing text, when deltas arrive, then the accumulated answer renders below the tree in the same live region and grows in place.
  - Given the turn completes, when the live region closes, then the full answer remains on screen exactly once — not duplicated by a final re-print.
  - Given a turn that produces tool calls but no text, when it completes, then the answer area is omitted rather than rendered empty.

#### US-203: Degrade to plain output when the console is not interactive

- **Story**: As a sample reader piping output to a file or running in CI, I want the sample to run and print a readable transcript so that a redirected run does not crash.
- **Priority**: P0 · **Estimate**: M · **Depends on**: US-201
- **Acceptance criteria**:
  - Given `AnsiConsole.Profile.Capabilities.Interactive` is false, when a turn runs, then no `AnsiConsole.Live` region is created and no exception is thrown.
  - Given the non-interactive path, when a turn completes, then stdout contains one line per activity with its badge, terminal status, and elapsed time, followed by the full answer and the token footer.
  - Given output redirected to a file (`dotnet run ... > out.txt`), when the process exits, then the exit code matches the interactive path for the same input and the file contains no ANSI cursor-control sequences.

#### US-204: Escape untrusted text before rendering

- **Story**: As a sample reader, I want tool and model output containing markup characters to display literally so that a stray `[` cannot corrupt or crash the console.
- **Priority**: P0 · **Estimate**: S · **Depends on**: US-201
- **Acceptance criteria**:
  - Given a tool that returns the literal string `[red]danger[/]`, when its card renders, then the text appears literally and no colour is applied.
  - Given an activity name or MCP server message containing unbalanced brackets, when it renders, then no markup-parsing exception is thrown and the turn completes.
  - Given the escaping helper, when it is called with text containing `[`, `]`, and no markup, then the unit test asserts the exact escaped output and that already-escaped input is not double-escaped.

#### US-205: Distinguish failed and cancelled activities, and leave a final snapshot

- **Story**: As a sample reader, I want a failed or cancelled step to look different from a successful one and to stay on screen so that I can see where a run went wrong.
- **Priority**: P1 · **Estimate**: S · **Depends on**: US-201, US-202
- **Acceptance criteria**:
  - Given a tool that throws, when its card reaches a terminal state, then the card shows a failed status plus the exception message, distinguished by a text label and not by colour alone.
  - Given Ctrl+C during a tool call, when the turn unwinds, then the in-flight card shows a cancelled status and no card is left in a running state.
  - Given the live region closes for any reason, when the next prompt is drawn, then a static `FinalSnapshot` of the tree remains above it with the cursor restored and no partial control sequences on screen.

### EP-3: Token usage & session cost

#### US-301: Show a per-turn token footer

- **Story**: As a sample reader, I want each turn to report its input, output, and total tokens so that I can see what a single question costs.
- **Priority**: P0 · **Estimate**: S · **Depends on**: US-201, US-102
- **Acceptance criteria**:
  - Given a completed turn whose response reports usage, when the live region closes, then a footer prints input, output, and total token counts, and total equals input plus output.
  - Given a turn whose response reports no usage, when it completes, then the footer prints `n/a` for the missing values rather than `0`.
  - Given the out-of-band fallback chosen in US-102, when a turn completes, then the footer values are identical to those from the in-band path for the same response — asserted by a unit test over the report-to-snapshot merge.

#### US-302: Accumulate a running session total

- **Story**: As a sample reader, I want a running total across the whole session so that I know what the conversation has cost so far.
- **Priority**: P1 · **Estimate**: S · **Depends on**: US-301
- **Acceptance criteria**:
  - Given three completed turns, when the third footer prints, then the session total equals the arithmetic sum of the three per-turn totals — asserted by an xUnit test over `SessionUsage`.
  - Given a turn that reports no usage, when it completes, then the session total is unchanged and no exception is thrown.
  - Given the session ends by `exit` or EOF, when the process exits, then the final session total is printed once.

#### US-303: Show per-tool token counts on activity cards

- **Story**: As a sample reader, I want to see which tool consumed which share of the tokens so that I can spot an expensive step.
- **Priority**: P2 · **Estimate**: M · **Depends on**: US-301
- **Acceptance criteria**:
  - Given an activity whose tracking report includes token counts, when its card reaches a terminal state, then the card shows those counts.
  - Given an activity whose report includes no counts, when its card renders, then the token field is omitted entirely rather than shown as `0`.
  - Given all cards in a turn report counts, when the turn ends, then the sum of card counts is less than or equal to the turn total, and the assertion is covered by a unit test on the merge logic.

### EP-4: Interactive console loop

#### US-401: Hold a multi-turn conversation over one session

- **Story**: As a sample reader, I want to ask follow-up questions in the same conversation so that I can see `AgentSession` doing its job.
- **Priority**: P0 · **Estimate**: M · **Depends on**: US-104
- **Acceptance criteria**:
  - Given a first turn that establishes a fact, when a second turn refers to it by pronoun, then the answer resolves the reference — proving both turns used the same `AgentSession` from `CreateSessionAsync()`.
  - Given input of `exit` or `quit`, when Enter is pressed, then the loop ends and the process exits with code 0.
  - Given end-of-input (EOF) on stdin, when the read returns null, then the loop ends with exit code 0 rather than looping forever or throwing.
  - Given blank or whitespace-only input, when Enter is pressed, then the prompt redraws and no model call is made.

#### US-402: Cancel a turn with Ctrl+C and exit with code 130

- **Story**: As a sample reader, I want Ctrl+C to stop a long-running turn without killing my session so that I can retry a different prompt.
- **Priority**: P0 · **Estimate**: S · **Depends on**: US-401
- **Acceptance criteria**:
  - Given a turn in flight, when Ctrl+C is pressed, then the run is cancelled, a cancellation notice prints, and the prompt returns for a new turn within 1 second.
  - Given an idle prompt, when Ctrl+C is pressed, then the process exits with code 130 — matching `HelloAgent`'s cancellation contract.
  - Given the process exits by any path, when it terminates, then the `CancelKeyPress` handler is detached before the `CancellationTokenSource` is disposed, so no `ObjectDisposedException` is observable.
  - Given a cancelled turn, when the next turn runs, then it succeeds on the same session rather than failing on inconsistent history.

#### US-403: Read prompts from redirected stdin

- **Story**: As a sample reader scripting a run, I want to pipe prompts in so that I can capture a transcript without typing.
- **Priority**: P1 · **Estimate**: M · **Depends on**: US-401, US-203
- **Acceptance criteria**:
  - Given `echo "hello" | dotnet run --project samples/02-agents/TrackedAgent`, when the process runs, then exactly one turn executes and the process exits with code 0.
  - Given a non-interactive console, when a prompt is needed, then `Console.ReadLine` is used instead of Spectre's `TextPrompt`, and no terminal-capability exception is thrown.
  - Given a multi-line piped input, when it is consumed, then each non-blank line becomes one turn in order and blank lines are skipped.

#### US-404: Configure the console per sample

- **Story**: As a sample author, I want to set the agent name, instructions, tools, banner, greeting, and redraw interval from my sample so that I never edit library code to change presentation.
- **Priority**: P1 · **Estimate**: S · **Depends on**: US-401
- **Acceptance criteria**:
  - Given `AgentConsoleOptions` with a custom name, banner, and greeting, when the console starts, then those values appear and no library default is shown.
  - Given a custom redraw interval, when a turn runs, then the throttle uses it instead of the 80 ms default.
  - Given a redraw interval that is negative, when the console is constructed, then it throws `ArgumentOutOfRangeException` naming the property.
  - Given `AgentConsoleOptions` with usage display disabled, when a turn completes, then no token footer prints and the session total is still tracked internally.

### EP-5: MCP tool source

#### US-501: Attach an MCP server's tools to the agent

- **Story**: As a sample author, I want to name an MCP server and have its tools attached to my agent so that a sample demonstrating MCP is a few lines long.
- **Priority**: P1 · **Estimate**: M · **Depends on**: US-104
- **Acceptance criteria**:
  - Given an MCP server command, when `McpToolSource` connects, then it creates the client with `McpClient.CreateAsync` over a `StdioClientTransport`, calls `ListToolsAsync()`, and the returned tools appear in the agent's tool list as `AITool` values.
  - Given the connected source, when the console shuts down, then the client is disposed with `await using` and the server process exits.
  - Given a server command that does not exist or exits immediately, when connection is attempted, then a warning naming the command prints, the console starts with the remaining tools, and no unhandled exception escapes.
  - Given an MCP tool invoked by the model, when it completes, then its card carries the `mcp` badge rather than `fn`, confirming `UseMcpToolClassification` is active.

#### US-502: Surface MCP progress notifications in the tree

- **Story**: As a sample reader, I want a long MCP call to show progress so that a slow round-trip does not look like a hang.
- **Priority**: P1 · **Estimate**: M · **Depends on**: US-501, US-201
- **Acceptance criteria**:
  - Given the client attached with `.WithTracking(client)`, when a tool sends `notifications/progress`, then the owning card's progress indicator advances toward the reported total.
  - Given a tool that sends progress with no known total, when notifications arrive, then the card shows an indeterminate indicator rather than a bar stuck at zero.
  - Given a tool that sends no progress notifications at all, when it runs, then its card shows elapsed time only and no empty progress bar is drawn.

### EP-6: TrackedAgent showcase sample

#### US-601: `samples/02-agents/TrackedAgent` runs end to end

- **Story**: As a sample reader, I want one sample that shows the tracked console working so that I can copy a known-good starting point.
- **Priority**: P0 · **Estimate**: M · **Depends on**: US-201, US-202, US-401, US-402
- **Acceptance criteria**:
  - Given a valid API key, when `dotnet run --project samples/02-agents/TrackedAgent` starts, then a banner and prompt appear and a prompt exercising the sample's function tool produces at least one `fn` card and a streamed answer.
  - Given the sample's `Program.cs`, when reviewed, then its configuration, client construction, agent construction, cancellation wiring, and turn loop together occupy ≤ 12 non-blank lines, with the remainder being tools, instructions, and options.
  - Given the sample project file, when reviewed, then it references `src/MafAgents.Interactive` by `ProjectReference`, carries its own `UserSecretsId`, and declares no `Version` attribute on any `PackageReference`.
  - Given no API key configured, when the sample starts, then the error message names `samples/02-agents/TrackedAgent` and the process exits non-zero without a stack trace.

#### US-602: Demonstrate a nested agent tool

- **Story**: As a sample reader, I want to see a sub-agent invoked as a tool and rendered as a nested card so that I understand agent composition and how it appears at runtime.
- **Priority**: P1 · **Estimate**: S · **Depends on**: US-601
- **Acceptance criteria**:
  - Given `DemoAgents.cs` exposing a secondary agent via `.AsAIFunction()`, when the primary agent calls it, then a card with the `agent` badge appears nested under the calling activity.
  - Given the nested agent itself calls a function tool, when it runs, then that call renders as a further nested `fn` card.
  - Given the nested run completes, when the turn ends, then the nested card shows its own elapsed time distinct from the parent's.

#### US-603: Ship an in-process demo MCP server with the sample

- **Story**: As a sample reader, I want the MCP part of the sample to work with no external install so that `dotnet run` is the only prerequisite beyond a key.
- **Priority**: P1 · **Estimate**: M · **Depends on**: US-501, US-502
- **Acceptance criteria**:
  - Given a clean machine with only the .NET SDK and an API key, when the sample runs, then the MCP demo executes with no `npx`, no network fetch, and no additional install step.
  - Given the demo server's long-running tool, when it is invoked, then it emits `notifications/progress` at least three times and the card's progress advances visibly between them.
  - Given the sample exits, when the process terminates, then the demo server is shut down and no orphaned process remains.

## 8. Milestones & rollout

**Phase 1 — Foundation** (EP-1: US-101, US-102, US-103, US-104). Roll-up: L. Nothing else can start until the spike resolves the progress-content question and the factory exists. US-101 is the only story with no dependency, so it is first.

**Phase 2 — MVP: a visible, cancellable turn** (US-201, US-202, US-203, US-204, US-301, US-401, US-402, US-601). Roll-up: L. This is the smallest set that delivers the PRD's headline value: a sample whose plumbing is ≤ 12 lines, whose tool calls are visible, whose answer streams, and whose token cost is shown. At the end of this phase the boilerplate and visibility success criteria are both measurable.

**Phase 3 — Polish and cost** (US-205, US-302, US-403, US-404). Roll-up: M. Failure/cancel states, session totals, scripted runs, and per-sample configuration.

**Phase 4 — MCP** (US-501, US-502, US-602, US-603). Roll-up: M. MCP is last because nothing else depends on it and the console is useful without it.

**Phase 5 — Optional** (US-303). Roll-up: M. Per-tool token counts; the first thing to cut.

Calendar dates and team composition are deliberately absent — see the assumption in section 9.

**Risks & mitigations**

| Risk | Mitigation |
| --- | --- |
| In-band `ChatProgressContent` leaks into `AgentSession`, breaking `SerializeSessionAsync` or the next turn. | US-102 is a time-boxed P0 spike that blocks the dependent stories, with a pre-agreed out-of-band fallback (`EmitProgressContent = false` + `IChatProgressObserver` → `Channel` → `AssistantStatusReducer`) adopted by default if the box expires. |
| Andes is a 0.x line; a minor release may break the API between now and the next repo change. | Pin the four Andes packages to exact versions in `Directory.Packages.props` using bracket notation (`[0.5.0]`) so a floor bump cannot silently resolve a breaking minor. |
| `CentralPackageTransitivePinningEnabled` plus divergent floors (Andes wants `Microsoft.Extensions.AI` ≥ 10.8.3, Agent Framework 1.17.0 wants ≥ 10.7.0) causes NU1605/NU1608 restore failures. | Pin `Microsoft.Extensions.AI` and `Microsoft.Extensions.AI.Abstractions` explicitly at 10.8.3 (US-101), and treat a restore warning here as a blocking review item since warnings are errors. |
| `AnsiConsole.Live` or `TextPrompt` throws in CI, a redirected shell, or a terminal without capabilities. | US-203 and US-403 make the non-interactive path a first-class, separately verified route gated on `AnsiConsole.Profile.Capabilities.Interactive`. |
| Warnings-as-errors plus `GenerateDocumentationFile` makes CS1591 a hard failure across a new public API surface. | US-101 proves the gate is live on day one; every subsequent story lands XML docs with its public members rather than deferring them. |
| Model-controlled tool output corrupts or crashes the Spectre renderer. | US-204 makes escaping mandatory and unit-tested, and is P0 rather than a polish item. |
| Nine new packages widen the transitive audit surface; a future CVE advisory shows up as a warning on every build. | `WarningsNotAsErrors` already exempts NU1901–NU1904, so an advisory stays visible without breaking clones; the obligation is to watch, not to gate. |
| Verification depends on a live model, so results vary run to run. | Pass thresholds are structural (badge types present, terminal statuses reached, totals reconcile), not answer-content-based, and are checked over 3 consecutive runs. |
| Third-party MCP servers execute arbitrary local processes. | The sample ships an in-process demo server (US-603); `McpToolSource` never discovers servers, it only launches a command the sample states explicitly. |

**Rollout & rollback**

The change is purely additive: a new `src/` project, a new `tests/` project, one new sample, and edits to `Directory.Packages.props`, `maf-agents.slnx`, and the root `README.md` samples table. No feature flag is warranted, because `HelloAgent` — the existing sample and the documented raw baseline — is untouched and keeps working regardless. Back-out is a revert of the PR; nothing outside `src/`, `tests/`, and `samples/02-agents/` references the new code. If EP-5 slips, the sample ships without the MCP demo and the MCP stories move to a follow-up PR, since no other epic depends on them.

## 9. Assumptions & open questions

**Assumptions**

- The personas in section 3 (sample reader, sample author, reviewer) are inferred from the repository's stated purpose; the request did not name users.
- Calendar dates, team size, and velocity are unknown for a single-maintainer repository, so section 8 gives dependency-ordered phases and T-shirt roll-ups only. Any date is `TBD`.
- Ctrl+C semantics are a design choice made here, not a given requirement: cancel the in-flight turn and return to the prompt; exit with 130 only from an idle prompt. `HelloAgent` has no loop, so it simply exits 130. Veto this if the repo wants the first Ctrl+C to always exit.
- A configuration failure (no key) exits non-zero with a friendly message rather than an unhandled exception. Exit codes assumed: `0` normal, `130` cancelled at prompt, `1` configuration error.
- The default model stays `gpt-4o-mini`, matching `HelloAgent` and the root `README.md`.
- The default redraw throttle is 80 ms, taken from the Andes reference implementation, and is configurable per sample.
- Exact-version pinning for the Andes packages uses NuGet bracket notation (`[0.5.0]`) in `Directory.Packages.props`; if the repo prefers plain floors for consistency with its other entries, the mitigation weakens to "review Andes release notes before any version bump".
- Session persistence is out of scope, but `SerializeSessionAsync` must not throw — that is the spike's pass criterion, not a v1 feature.
- The repository has no CI workflow and no telemetry or logging conventions today (nothing under `samples/` references `ILogger` or OpenTelemetry), so success criteria are measured by local commands, unit tests, and PR review rather than by emitted events. No telemetry story is proposed for that reason.
- Documentation outputs — the sample `README.md`, any ADR for the Andes dependency, the root `README.md` samples row, and the `CHANGELOG.md` entry — are handled by the `se-technical-writer` flow after implementation, per `.claude/CLAUDE.md`, and are therefore deliberately absent from section 7.
- The showcase sample's function tools, nested agent, and demo MCP tool are assumed to be simple deterministic stubs (for example weather lookup, a reviewer sub-agent, a paced counter) rather than anything calling an external service.

**Open questions**

- Should `MafAgents.Interactive` also expose a non-streaming `RunAsync` convenience for a future sample that wants a plain answer without the live tree? — repository owner.
- Should the REPL support slash-commands (`/reset`, `/usage`, `/save`) in a later version? They are excluded from v1 as a non-goal, but the options surface in US-404 could be shaped now to accommodate them. — repository owner.
- `HelloAgent` keeps its hardcoded `--project samples/01-get-started/HelloAgent` message in four places by decision. If a second raw, dependency-free sample is ever added, does that duplication get revisited? — repository owner.
- Does taking a third-party 0.x dependency (Andes) in a Microsoft-samples repository warrant its own ADR alongside ADR-0001? — repository owner, drafted by `se-technical-writer`.
- If per-tool token counts (US-303) ship, should `SessionUsage` ever estimate dollar cost? It would need a price table that goes stale, which is why it is a non-goal today. — repository owner.
