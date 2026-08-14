# PRD: Shared Interactive Agent Console

## 1. Overview

**Problem**: The repository's only sample, `samples/01-get-started/HelloAgent/Program.cs`, is 91 lines, of which roughly 55 are sample-agnostic plumbing — a `ConfigurationBuilder`, a `FirstNonBlank` key/model resolver, `OpenAIClient` → `GetResponsesClient()` → `AsAIAgent(...)` behind an `OPENAI001` pragma, `Console.CancelKeyPress` wiring, and an `OperationCanceledException` → exit-code-130 path. Only about 12 lines teach an Agent Framework concept. The `02-agents`, `03-workflows`, `04-hosting`, and `05-end-to-end` categories are empty, so every sample that follows would retype that plumbing plus a multi-turn loop that exists nowhere in the repo. Samples also show nothing of what the agent is *doing* — function calls, MCP round-trips, and nested agent runs are invisible until the final text arrives — token cost is never shown, and the model endpoint is hardcoded to OpenAI in source, so trying a sample against Ollama, LM Studio, or an Azure OpenAI resource means editing C#.

**Solution**: A shared class library, `MafAgents.Tui`, under `src/`, referenced by `ProjectReference` from each sample. It owns configuration, the provider/model picker, tracked-agent construction, a Terminal.Gui v2 chat shell with a live activity tree and streamed answer pane, token accounting, cancellation, and MCP wiring, so a sample's `Program.cs` declares only its name, instructions, tools, and one `AgentShell.RunAsync(options)` call. One new showcase sample, `samples/02-agents/TrackedAgent`, consumes it; `HelloAgent` stays raw and unchanged as the deliberate no-dependency baseline.

**Success criteria**:

- Plumbing in `samples/02-agents/TrackedAgent/Program.cs` — configuration, provider selection, client construction, agent construction, cancellation wiring, and the turn loop — is **≤ 12 non-blank lines**, against 55 in `HelloAgent` (a ≥ 78% reduction), counted by hand at PR review against the two files.
- Pointing a sample at a different provider or model costs **one edited file and zero lines of C#**: `rg "Endpoint|ApiKeyRef|GetResponsesClient|GetChatClient" --glob '!**/bin/**' --glob '!**/obj/**'` returns hits only in `src/MafAgents.Tui/` and `samples/01-get-started/HelloAgent/`, and the reviewer adds a second provider to `config/appsettings.json` and runs the showcase against it without touching a `.cs` file.
- For a turn that invokes at least one function tool, one MCP tool, and one nested agent, **100% of those invocations** appear in the activity tree as a node carrying a type badge (`fn` / `mcp` / `agent`), a terminal status, and an elapsed time, with each node's first paint no later than one redraw interval (≤ 100 ms) after its start event — verified by the scripted `TrackedAgent` run on three consecutive executions.
- Every completed turn shows an input/output/total token footer, and the running session total equals the arithmetic sum of the per-turn totals — asserted by an xUnit test over `SessionUsage` and confirmed visually in the scripted run.
- `dotnet build`, `dotnet format --verify-no-changes`, and `dotnet test` all pass with **zero warnings and zero suppressions** for the new projects under the repository's existing `TreatWarningsAsErrors` + `GenerateDocumentationFile` settings, **and** three consecutive runs of a turn emitting ≥ 500 text deltas complete with zero cross-thread or `Invoke`-related exceptions.

End to end, a reader clones the repo, copies `config/appsettings.sample.json` to `config/appsettings.json`, sets their key with `dotnet user-secrets`, and runs `dotnet run --project samples/02-agents/TrackedAgent`. A Terminal.Gui window opens. If more than one provider or model is available, a picker lists them with the configured defaults preselected; otherwise the picker is skipped. They type a question. An activity tree fills in as the agent works — an `fn` node for the weather function, an `mcp` node whose progress advances as the server reports it, an `agent` node for the nested reviewer — each with its own elapsed time, while the answer streams into the pane beside it. When the turn ends the tree freezes as a static summary, the footer shows the turn's tokens, and the status bar's session total ticks up. `Esc` cancels a turn in flight; `Ctrl+Q` quits. Piping the same sample's output to a file produces a plain linear transcript with no terminal control sequences at all.

## 2. Goals & non-goals

**Goals**

- A new sample author writes only the code that teaches the concept — tools, instructions, prompts — and no configuration, provider, client, cancellation, or loop plumbing.
- A reader can see every tool call, MCP round-trip, and nested agent run as it happens, rather than inferring it from the final answer.
- A reader can run any sample against any OpenAI-compatible endpoint — OpenAI, Azure OpenAI's `/openai/v1` compatibility path, Ollama, LM Studio, OpenRouter, GitHub Models — by editing one untracked file, with no code change and no new package.
- Token consumption is visible per turn and cumulatively per session, so a reader knows what a run costs before they copy it.
- Credentials never enter the repository tree, and never appear in a rendered string or an exception message.
- Plumbing bugs are fixed in one file rather than in every sample.
- The library meets the repository's library-grade bar: XML docs on every public member, xUnit coverage of the deterministic parts, and a clean `dotnet format --verify-no-changes`.

**Non-goals**

- A launcher or sample-discovery UI. Each sample stays an independently runnable console app started with `dotnet run --project samples/...`. The picker chooses a provider and a model — never a sample.
- Reflection-based or attribute-based sample discovery.
- Any change to `samples/01-get-started/HelloAgent`. It deliberately demonstrates the bare framework with no shared dependency, and its four-place hardcoded key-setup message stays as it is.
- Spectre.Console, in any form. Two libraries must not both own the screen; Terminal.Gui owns the interactive path and plain `Console` owns the headless one.
- Workflow rendering (`03-workflows`), hosting (`04-hosting`), or end-to-end (`05-end-to-end`) samples. The library is built so those can adopt it later; this project does not write them.
- Persisting sessions to disk, resuming across process restarts, or any session store — including remembering the last picker selection.
- Dollar-cost estimation from token counts (it needs a price table that goes stale).
- Telemetry export, OpenTelemetry wiring, or any log sink. Output is the terminal.
- Multi-user, hosted, or web UI of any kind.
- Any prerelease package.
- Any provider outside the OpenAI-compatible abstraction. A provider is a base URL plus a credential reference plus a model list; anything needing a different SDK is out of scope and `docs/adr/0001-openai-as-model-provider.md` is untouched.
- Unit-testing the live-model path. Per `.claude/CLAUDE.md`, samples that call a live model are not unit-tested; only the library's deterministic units are.

## 3. Users & access

**Personas**

- **Sample reader**: a .NET developer evaluating Microsoft Agent Framework who clones the repo, runs one sample, and reads its `Program.cs` in a sitting. They need the teaching code to dominate the file and the runtime behaviour to be legible.
- **Sample author**: the repository owner or a contributor adding a sample under `samples/<NN-category>/`. They need one call that yields a working, observable shell so the diff of a new sample is the concept and nothing else.
- **Local-model developer**: a reader who cannot or will not send prompts to OpenAI and wants to run the same sample against Ollama or LM Studio on `localhost`. They need to change an endpoint, not a code path.
- **Reviewer**: whoever reviews the sample PR. They need the build, format, and test gates to fail loudly rather than relying on inspection.

There is no authentication or authorization model: every sample is a single-user, single-process console app with no protected resource of its own. The only credentialed resources are the configured model endpoints and any MCP server a sample connects to. Credential handling — resolution by reference, absence of any key in the repository tree, and redaction from rendered text — is specified under Security in section 6 and delivered by US-105, US-202, and US-701.

## 4. Functional requirements

| ID | Requirement | Priority | Epic(s) |
| --- | --- | --- | --- |
| FR-1 | A sample builds a fully tracked `AIAgent` — client, tool-tracking middleware, function invocation, tools, instructions — from a resolved provider selection in a single call. | P0 | EP-1 |
| FR-2 | Client construction is the only code that branches on the provider's API surface: `Responses` (default, preferred) or `ChatCompletions`. Everything downstream — Andes pipeline, agent, shell, renderers — is identical. | P0 | EP-1 |
| FR-3 | The chat-client pipeline preserves the ordering invariant `UseToolTracking` → `UseFunctionInvocation`, and `ChatClientAgent` must not insert a second `FunctionInvokingChatClient`. | P0 | EP-1 |
| FR-4 | Tracking events reach the renderer out of band (`EmitProgressContent = false`), so no synthetic content enters `AgentSession`: a session survives serialization and a subsequent turn after a tool-calling turn. | P0 | EP-1 |
| FR-5 | No credential, credential fragment, or key-shaped substring appears in any rendered string, status bar, tree node, log line, or exception message — including text supplied by the provider. | P0 | EP-1, EP-2 |
| FR-6 | Provider configuration lives in exactly one gitignored `config/appsettings.json` at the repo root, reaches every consuming sample through one MSBuild mechanism, and has defined behaviour when the file is absent. A tracked `config/appsettings.sample.json` documents the schema and contains no credential. | P0 | EP-2 |
| FR-7 | The configuration schema is `Providers[]` (`Name`, `Endpoint?`, `Api`, `ApiKeyRef?`, `Models[]`), `DefaultProvider`, `DefaultModel`, and a `Ui` section; a malformed or unusable file fails at startup with a message naming the offending entry. | P0 | EP-2 |
| FR-8 | A provider's credential resolves at selection time from the user-secret or environment variable its `ApiKeyRef` names; a missing one produces a message naming the exact `dotnet user-secrets set … --project <sample path>` command, with no stack trace and no key fragment. A provider with no `ApiKeyRef` needs no credential. | P0 | EP-2 |
| FR-9 | A startup view lists the configured providers and their models with the configured defaults preselected, and is skipped when the defaults resolve to exactly one usable provider and model. | P0 | EP-2 |
| FR-10 | A provider declaring `Api: "Responses"` against an endpoint that does not implement it fails with a message naming the provider, the endpoint, and the `"Api": "ChatCompletions"` fix — never an opaque 404. | P1 | EP-2 |
| FR-11 | The shell follows the Terminal.Gui v2 lifecycle `Application.Create()` → `app.Init(driver)` → `Application.Run(…)` → `app.Dispose()`, and restores the terminal on every exit path, including an unhandled exception. | P0 | EP-1, EP-4 |
| FR-12 | Every mutation of a Terminal.Gui view originating from the agent turn — which runs on the thread pool — crosses back through `Application.Invoke(…)`. | P0 | EP-1, EP-3 |
| FR-13 | While a turn runs, the shell renders a live activity tree: one node per activity with a `fn` / `mcp` / `agent` badge, sub-status, nesting under its parent, progress, elapsed time, and a terminal status. | P0 | EP-3 |
| FR-14 | The answer text streams into a dedicated pane as it arrives. | P0 | EP-3 |
| FR-15 | Redraws are throttled on the producer side, before `Invoke`, to a configurable interval (default 80 ms), so the UI thread's work queue never receives more than one snapshot per interval. | P0 | EP-3 |
| FR-16 | A status bar shows the active provider and model for the whole session. | P1 | EP-3 |
| FR-17 | The shell runs a multi-turn conversation over a single `AgentSession`, so turn *n* sees the context of turns 1..*n*-1. | P0 | EP-3 |
| FR-18 | Cancellation, quit, focus movement, and scrolling are key bindings or menu actions owned by the Terminal.Gui driver, not by `Console.CancelKeyPress`; the full key map is published in-app. | P0 | EP-4 |
| FR-19 | The process honours a documented exit-code contract: `0` normal quit, `1` startup configuration failure, `130` SIGINT in the headless path only. | P0 | EP-4, EP-6 |
| FR-20 | Each completed turn shows an input / output / total token footer, and a running session total accumulates across turns. | P0 | EP-5 |
| FR-21 | When stdout is redirected or no TTY is available, the program runs a plain linear transcript on `Console` — never initializing Terminal.Gui — emitting the same information with no terminal control sequences, and exits normally instead of throwing. | P0 | EP-6 |
| FR-22 | Untrusted text (model output, tool arguments and results, MCP server messages, provider error bodies) is sanitized of C0/C1 control characters and ANSI escape sequences before it reaches any view or the plain transcript. | P0 | EP-3, EP-6 |
| FR-23 | The library connects to an MCP server, lists its tools, and attaches them with tracking; `notifications/progress` drive the owning node's progress indicator; a server that fails to start produces an actionable message and does not abort the shell. | P1 | EP-7 |
| FR-24 | `samples/02-agents/TrackedAgent` runs end to end with a function tool, a nested agent tool, and an in-process MCP tool that reports progress, requiring no install beyond the .NET SDK and a credential. | P0 | EP-8 |
| FR-25 | The new `src/` and `tests/` projects build clean under `TreatWarningsAsErrors` with XML docs on every public member, use central package management with no `Version` attributes, add no prerelease package, and leave no reference to Spectre.Console anywhere in the repository. | P0 | EP-1 |

## 5. User experience

**Entry points & first-time flow**

The only entry point is `dotnet run --project samples/<NN-category>/<SampleName>`. Before any UI appears the shell resolves configuration:

- No `config/appsettings.json` present — the shell falls back to a single built-in provider (`OpenAI`, `Responses`, credential `OpenAI:ApiKey` / `OPENAI_API_KEY`, model `gpt-4o-mini`), matching `HelloAgent`'s behaviour exactly. A clone with only a key set still runs.
- File present but malformed or empty of providers — the program prints the offending entry and the schema location on the plain terminal and exits `1`, before Terminal.Gui is initialized.
- File present and usable — the providers it lists are the ones offered.

With a usable configuration and a TTY, Terminal.Gui initializes. If the defaults resolve to exactly one usable provider and model, the picker is skipped and the chat shell opens directly; otherwise the picker opens first with the defaults preselected. Choosing a provider resolves its credential; if the named user-secret and environment variable are both blank, an inline message names the exact command — `dotnet user-secrets set "OpenAI:ApiKey" "sk-..." --project samples/02-agents/TrackedAgent` — and the picker stays open so another provider can be chosen.

**Core experience**

The chat shell is one Terminal.Gui window:

```
┌─ TrackedAgent ─────────────────────────────────────────────────────┐
│ File   Session   Help                                    (menu bar) │
├──────────────────────────┬──────────────────────────────────────────┤
│ Activity                 │ Answer                                   │
│  ▸ fn  get_weather   0.4s│ Quito is currently 18 °C and overcast.   │
│  ▾ mcp search_corpus 2.1s│ The corpus mentions three related…       │
│      ▸ 60%  page 3 of 5  │ ▌                                        │
│  ▸ agent Reviewer    1.2s│                                          │
├──────────────────────────┴──────────────────────────────────────────┤
│ > what is the weather in Quito?                          [ Cancel ] │
├─────────────────────────────────────────────────────────────────────┤
│ OpenAI · gpt-4o-mini │ turn 412/188/600 │ session 1,240 │ Ctrl+Q quit│
└─────────────────────────────────────────────────────────────────────┘
```

1. The user types a prompt in the input field and presses `Enter`.
2. The input field goes read-only, the Cancel button enables, and the agent turn starts on the thread pool.
3. Activity nodes appear in the tree as the agent works: a badge (`fn`, `mcp`, `agent`), the activity name, a sub-status line, a progress indicator when the source reports progress, elapsed time, and nesting for activities started inside another.
4. Answer text streams into the answer pane as it arrives.
5. When the turn completes, the tree freezes as a static summary, the turn's token footer appears in the status bar, the running session total updates, and the input field becomes writable again.
6. The next turn shares the same `AgentSession`, so follow-ups can use pronouns.
7. `Ctrl+Q` or File → Quit ends the session with exit code `0`.

**Key map** — published in-app under Help → Keys (`F1`) and in the sample's README:

| Key | Context | Action |
| --- | --- | --- |
| `Enter` | Input focused, idle | Submit the prompt |
| `Enter` | Input focused, turn running | Ignored — the field is read-only for the duration |
| `Esc` | Anywhere, turn running | Cancel the turn in flight |
| `Esc` | Anywhere, idle | No-op. `Esc` never quits |
| `Ctrl+Q` | Anywhere | Quit: cancel any in-flight turn, dispose the application, exit `0` |
| `F10` | Anywhere | Open the menu bar |
| `F1` | Anywhere | Show the key-map dialog |
| `Tab` / `Shift+Tab` | Anywhere | Cycle focus: input → activity tree → answer pane → input |
| `PgUp` / `PgDn` / arrows | Focused pane | Scroll |
| `Ctrl+C` | Anywhere | Not bound to quit or cancel. The driver puts the terminal in raw mode and consumes the key, so it is left at Terminal.Gui's default binding |

**Exit codes**

| Code | Meaning |
| --- | --- |
| `0` | Normal quit — `Ctrl+Q`, File → Quit, or end of piped input in the headless path |
| `1` | Startup configuration failure — malformed `config/appsettings.json`, no usable provider, or a selected provider whose credential cannot be resolved and no alternative chosen |
| `130` | SIGINT in the **headless** path only, where `Console.CancelKeyPress` is still the mechanism. The TUI has no `130` path because the driver consumes `Ctrl+C` |

A failure inside a turn never terminates the process: it renders as a failed node with its message, and the prompt returns.

**Edge cases & UI states**

- *Empty input*: blank or whitespace-only input re-prompts without calling the model.
- *No providers*: a configuration file with an empty `Providers[]` array is a startup failure (`1`), not an empty picker.
- *Credential missing*: named inline in the picker with the exact command; the picker stays open. Choosing to quit from that state exits `1`.
- *`Responses` against a Chat-Completions-only endpoint*: the first turn fails with a message naming the provider, the endpoint, and the `"Api": "ChatCompletions"` fix; the shell stays open.
- *Tool failure*: the node shows a failed status and the sanitized error message; the turn continues if the agent can recover and otherwise ends with the model's error text, never with an unhandled exception.
- *Cancellation*: the in-flight node is marked cancelled, no node is left running, and the input field becomes writable again within 1 second.
- *MCP server unavailable*: a message names the failing source, the shell starts with the remaining tools, and no MCP nodes appear.
- *No usage reported*: the footer shows `n/a` for the missing figure rather than `0`, and the session total is unchanged.
- *Terminal too small*: the shell renders a single message asking for a larger window rather than drawing a corrupted layout.
- *Redirected output or no TTY*: Terminal.Gui is never initialized; the plain transcript path runs instead.

**UI/UX highlights**

- Redraw is throttled (default 80 ms) *before* the `Invoke` marshalling call, because snapshots arrive per text delta and the UI thread's work queue — not the repaint — is the thing that must not be flooded.
- Colour is a supplement, never the sole carrier of meaning: every status also has a word, so a monochrome terminal, `NO_COLOR`, or a screen reader consuming redirected output loses nothing.
- The whole shell is keyboard-operable; nothing requires a mouse.
- Untrusted text is sanitized of control characters and ANSI sequences, so a tool result containing an escape sequence renders literally instead of repainting or corrupting the terminal.
- The headless path is the accessibility path: piping output yields a complete, linear transcript of the same information with no cursor control at all.

## 6. Technical considerations

**Integration points** (all verified in this repository or against official docs and the NuGet feed)

- `samples/01-get-started/HelloAgent/Program.cs` — the source of the plumbing being extracted, and the one sample this project must not change. Its `FirstNonBlank` resolver, `#pragma warning disable OPENAI001` scope, `CancelKeyPress` handler, and 130 exit path are the behaviours the library reproduces (the last two only on the headless path).
- `Directory.Build.props` — `net10.0`, `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`, `AnalysisLevel=latest-recommended`, and `GenerateDocumentationFile`, which makes CS1591 a build error for the library. `WarningsNotAsErrors` already exempts NU1901–NU1904. This file also gains the shared-configuration `Content` link.
- `Directory.Packages.props` — central package management with `CentralPackageTransitivePinningEnabled=true`. Every new version lands here; no `.csproj` carries a `Version` attribute.
- `.gitignore` — has a `.env` entry at line 7 and **no** `appsettings` entry today. It gains `config/appsettings.json`.
- `.editorconfig` — promotes IDE0055, IDE1006, IDE0005, and CA1507 to `warning`, which warnings-as-errors turns into build failures.
- `maf-agents.slnx` — gains `/src/`, `/tests/`, and `/samples/02-agents/` folders, plus `config/appsettings.sample.json` under the existing `/build/` folder.
- `docs/adr/0001-openai-as-model-provider.md` — unchanged and unsuperseded. A named OpenAI-compatible endpoint is still `OpenAI` + `Microsoft.Agents.AI.OpenAI` with a different base URL, so the ADR's decision and its stable-only reasoning both still hold.
- Microsoft Agent Framework 1.17.0: `AIAgent`, `ChatClientAgent`, `AgentSession`, `CreateSessionAsync`, `RunStreamingAsync`, `SerializeSessionAsync` / `DeserializeSessionAsync`, and `AgentResponseExtensions.AsChatResponseUpdatesAsync()`. `ChatClientAgent`'s constructor documentation states its `IServiceProvider` parameter "is only relevant when the `IChatClient` doesn't already contain a `FunctionInvokingChatClient`", so a pre-built pipeline preserves the tracking ordering invariant; `ChatClientAgentOptions.UseProvidedChatClientAsIs = true` is the belt-and-braces switch.
- `Microsoft.Extensions.AI.OpenAI` — supplies both `AsIChatClient` overloads the dual-surface design needs (see below).
- Andes 0.5.0 (`Andes.Extensions.AI`, `.Agent`, `.Mcp`, `.UI` — MIT, `net10.0`): `UseToolTracking`, `UseAgentToolClassification`, `UseMcpToolClassification`, `WithTracking`, `IChatProgressObserver`, `ChatUsageReport`, `AssistantStatusSnapshot` / `AssistantActivity`, `AssistantStatusReducer`, `StripProgressContent`, `ChatProgressUpdate.CreateCustom`.
- ModelContextProtocol 2.1.0 (GA): `McpClient.CreateAsync(transport, …)` over any `IClientTransport`, then `ListToolsAsync()`, with `McpClientTool` values cast to `AITool`.
- Terminal.Gui 2.4.17 (MIT, `net10.0`, `github.com/tui-cs/Terminal.Gui`): `Application.Create()`, `app.Init(driver)`, `Application.Run(…)`, `app.Dispose()`, `Application.Invoke(…)` / `app.Invoke(…)`, `Window`, `MenuBar`, `StatusBar`, `TreeView`, `TextView`, `TextField`, `Button`, `Dialog`.

**Two API surfaces, one pipeline**

Not every OpenAI-compatible endpoint implements `/v1/responses` — Ollama and OpenRouter are Chat Completions only. Both surfaces reach `IChatClient`, verified against `Microsoft.Extensions.AI.OpenAI`: `AsIChatClient(ResponsesClient, string?)` is marked `[Experimental("OPENAI001")]`, and `AsIChatClient(ChatClient)` is unattributed. A provider therefore *declares* its surface, and exactly one expression branches:

```csharp
OpenAIClient client = new(
    new ApiKeyCredential(credential),
    new OpenAIClientOptions { Endpoint = provider.Endpoint });

IChatClient chatClient = provider.Api switch
{
    ProviderApi.Responses => client.GetResponsesClient().AsIChatClient(model),   // OPENAI001 pragma scoped here
    ProviderApi.ChatCompletions => client.GetChatClient(model).AsIChatClient(),
    _ => throw new ArgumentOutOfRangeException(nameof(provider)),
};
```

Everything after that line — the Andes tracking pipeline, `ChatClientAgent`, the shell, both renderers — is identical. This is one `switch` expression, not two divergent pipelines, and `Responses` remains the default and the preferred surface wherever the endpoint supports it. Azure OpenAI joins as a provider through its documented v1 compatibility path: `OpenAIClientOptions.Endpoint = https://<resource>.openai.azure.com/openai/v1/` with the key in the credential.

**Configuration**

One gitignored `config/appsettings.json` at the repo root, with a tracked `config/appsettings.sample.json` beside it:

```json
{
  "Providers": [
    { "Name": "OpenAI",       "Api": "Responses",       "ApiKeyRef": "OpenAI:ApiKey",     "Models": ["gpt-4o-mini", "gpt-4.1-mini"] },
    { "Name": "Azure OpenAI", "Api": "Responses",       "ApiKeyRef": "AzureOpenAI:ApiKey", "Endpoint": "https://<resource>.openai.azure.com/openai/v1/", "Models": ["gpt-4.1-nano"] },
    { "Name": "Ollama",       "Api": "ChatCompletions",                                    "Endpoint": "http://localhost:11434/v1/", "Models": ["llama3.2"] }
  ],
  "DefaultProvider": "OpenAI",
  "DefaultModel": "gpt-4o-mini",
  "Ui": { "RedrawIntervalMilliseconds": 80, "ShowUsage": true, "AlwaysShowPicker": false }
}
```

`Endpoint` is optional and omitted for OpenAI itself. `ApiKeyRef` is optional and omitted for providers that need no credential (Ollama, LM Studio). It names a configuration key, never a value: the key is read from the calling sample's user-secrets store first and the environment second, with a blank value treated as absent — the unpopulated-CI-secret case `HelloAgent` already handles.

The file reaches every consuming sample through one MSBuild `Content` link in `Directory.Build.props`, guarded so an absent file is not an error:

```xml
<ItemGroup Condition="Exists('$(MSBuildThisFileDirectory)config/appsettings.json')">
  <Content Include="$(MSBuildThisFileDirectory)config/appsettings.json"
           Link="appsettings.json"
           CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

When the file is absent the item does not exist, nothing is copied, and the library's built-in default provider takes over. See the assumption in section 9 about the opt-in refinement if a stray file in `HelloAgent`'s output directory is unacceptable.

**Threading**

This is the load-bearing architectural constraint. The agent turn runs on the thread pool and produces `AssistantStatusSnapshot` values per tracking event and per text delta; Terminal.Gui views may only be touched on the UI thread. Every snapshot therefore crosses back through `Application.Invoke(…)`. Two rules follow:

1. **Throttle before `Invoke`, not inside it.** A gate on the producer side means the work queue receives at most one item per interval; a gate on the consumer side would let a 500-delta turn enqueue 500 closures and make the queue itself the bottleneck.
2. **Latest snapshot wins.** Snapshots are cumulative — each reflects every event so far — so a superseded snapshot is dropped rather than queued behind its successor.

The single marshalling helper that enforces both is the only place the library calls `Invoke`, which makes "no view is touched off the UI thread" a reviewable property rather than a hope.

**Package versions to add to `Directory.Packages.props`** (every version below was confirmed to be a listed, stable release on nuget.org):

| Package | Version | Note |
| --- | --- | --- |
| `Terminal.Gui` | `[2.4.17]` | Latest stable; only `2.4.18-develop.*` prereleases exist above it. Exact-pinned |
| `Andes.Extensions.AI` | `[0.5.0]` | Exact-pinned; 0.x line |
| `Andes.Extensions.AI.Agent` | `[0.5.0]` | Exact-pinned |
| `Andes.Extensions.AI.Mcp` | `[0.5.0]` | Exact-pinned |
| `Andes.Extensions.AI.UI` | `[0.5.0]` | Exact-pinned |
| `Microsoft.Extensions.AI` | `10.8.3` | Andes 0.5.0's declared floor |
| `Microsoft.Extensions.AI.Abstractions` | `10.8.3` | Pinned explicitly because transitive pinning is on |
| `Microsoft.Extensions.AI.OpenAI` | `10.8.3` | Supplies both `AsIChatClient` overloads |
| `ModelContextProtocol` | `2.1.0` | Andes' `ModelContextProtocol.Core` floor |
| `Microsoft.Extensions.Configuration.Json` | `10.0.9` | Matches the repo's existing Configuration family |
| `Microsoft.Extensions.Configuration.Binder` | `10.0.9` | Matches the repo's existing Configuration family |

Terminal.Gui 2.4.17's own nuspec declares `Microsoft.Extensions.Configuration`, `.Binder`, and `.Json` at 10.0.7 and `Microsoft.Extensions.Options` at 10.0.9 — the repo's 10.0.9 pins are upgrades, not downgrades, so no NU1605 arises from that direction.

**Data storage & privacy**

Nothing is persisted. `AgentSession` lives in process memory and is discarded at exit; this version writes no session, transcript, usage file, or picker-state file. Prompts, tool arguments, tool results, and answers are rendered to the terminal only. `config/appsettings.json` holds endpoints, model names, and credential *references* — never a credential, and nothing user-identifying. Agent Framework's guidance notes that a serialized session may contain conversation content and PII; the library serializes a session only in the test that proves the out-of-band design keeps history clean, and never writes the result to disk.

**Security**

- Credentials are read from `dotnet user-secrets` (stored outside the repository tree) or the environment, per the repository's documented carve-out from the `DefaultAzureCredential` standard. No credential is ever written to `config/appsettings.json`, which is gitignored anyway; `config/appsettings.sample.json` is tracked and contains only references.
- A resolved credential is never rendered, never included in an exception message, and never part of a status snapshot. On top of that, provider-supplied text is redacted by shape (`sk-[A-Za-z0-9\-_*]{4,}`) before it reaches a view, because an HTTP 401 body echoes a masked fragment of the key that was sent — which FR-5 forbids on screen as squarely as the whole key.
- Tool arguments and results are model-chosen and must be treated as untrusted input. Terminal.Gui has no markup language, so the previous design's markup-escaping hazard is gone; it is replaced by control-character and ANSI-escape sanitization (FR-22), because a `TextView` or `TreeView` label carrying a raw `ESC [` sequence can still drive the terminal directly.
- MCP servers are third-party processes. `McpToolSource` requires an explicit command from the sample rather than discovering servers, and the showcase ships an in-process demo server so `dotnet run` never launches an unvetted external binary.
- A locally configured provider endpoint is an outbound network destination chosen by the reader. The shell shows the active provider and model in the status bar for the whole session so the destination is never ambiguous.
- `NuGetAuditMode` findings stay warnings by repository decision. Terminal.Gui widens the transitive surface audit covers, which is a monitoring obligation, not a build gate.

**Scalability & performance**

Single user, single process, one in-flight turn. The constraints that matter are render-side: snapshots arrive per text delta, so the producer-side throttle caps UI work at one snapshot per 80 ms (≤ 12.5 repaints/s) and the tree is rebuilt from the latest snapshot rather than diffed. Token accounting is O(1) per turn. The activity tree is bounded by the number of tool calls in a turn; no history trimming is in scope. The acceptance bar for the marshalling design is a turn emitting ≥ 500 text deltas completing with zero cross-thread exceptions and zero dropped *terminal-state* transitions across three runs.

**AI system requirements**

- Model: whatever the picker resolves. `DefaultModel` from configuration, falling back to `gpt-4o-mini` when no configuration file is present, matching `HelloAgent` and the root `README.md`.
- Tools the showcase must exercise: at least one local `AIFunction`, one nested agent exposed through Andes' `WithTracking`, and one MCP tool that reports progress.
- Evaluation: there is no model-quality benchmark and none is proposed — the library does not change what the model says. Verification is (a) xUnit over the deterministic units — configuration binding and validation, provider and credential resolution, key-shape redaction, the control-character sanitizer, the redraw throttle, usage aggregation, snapshot-to-view-model mapping, and prompt dispatch — and (b) a scripted manual run of `TrackedAgent` whose pass threshold is: on 3 consecutive runs, all three badge types appear, every node reaches a terminal status, the per-turn footer shows a non-zero total, the session total equals the sum of the per-turn totals, and no cross-thread exception occurs.

## 7. Epics & user stories

| ID | Epic | Goal | Priority | Estimate | Depends on |
| --- | --- | --- | --- | --- | --- |
| EP-1 | Foundation: projects, host, and tracked agent | A sample can build a configured, fully tracked agent and a Terminal.Gui host in one call each | P0 | L | — |
| EP-2 | Provider and model selection | A reader chooses where the model comes from by editing one file, with no credential in the repo | P0 | L | EP-1 |
| EP-3 | Terminal.Gui chat shell | A reader sees every tool call, MCP round-trip, and nested run as it happens, and can hold a conversation | P0 | L | EP-1, EP-2 |
| EP-4 | Keyboard control, cancellation, and exit contract | A reader cancels a turn and quits cleanly from the keyboard, with a predictable exit code | P0 | M | EP-3 |
| EP-5 | Token accounting | A reader sees what each turn and the whole session cost in tokens | P1 | M | EP-1, EP-3 |
| EP-6 | Headless transcript path | A reader can pipe a sample's output or run it in CI and get a readable transcript | P0 | M | EP-1, EP-2 |
| EP-7 | MCP tool source | A sample attaches MCP server tools and sees their progress in the tree | P1 | M | EP-1, EP-3 |
| EP-8 | TrackedAgent showcase sample | The library is proven by a runnable sample under `02-agents` | P0 | M | EP-3, EP-4 |

### EP-1: Foundation — projects, host, and tracked agent

#### US-101: `[enabler]` Project, package, and solution scaffolding

- **Story**: `[enabler]` Create `src/MafAgents.Tui` and `tests/MafAgents.Tui.Tests`, register their package versions centrally, add them to the solution, and remove the reverted `MafAgents.Interactive` tree. Unblocks every story in this PRD.
- **Priority**: P0 · **Estimate**: M · **Depends on**: —
- **Acceptance criteria**:
  - Given the eleven new package versions added to `Directory.Packages.props`, when `dotnet build` runs, then it succeeds and no `.csproj` in the repository contains a `Version` attribute on a `PackageReference`.
  - Given the restore graph, when it is inspected, then it contains no prerelease package, `Terminal.Gui` resolves to exactly `2.4.17`, the four Andes packages resolve to exactly `0.5.0`, and `Microsoft.Extensions.AI` and `Microsoft.Extensions.AI.Abstractions` are pinned explicitly at `10.8.3` so transitive pinning cannot resolve a lower version.
  - Given the whole working tree, when `rg -i "spectre" --glob '!**/bin/**' --glob '!**/obj/**'` runs, then it returns no match — proving Spectre.Console was dropped rather than left behind.
  - Given a public type with no XML doc comment added temporarily to the library, when `dotnet build` runs, then it fails with CS1591 — proving the documentation gate is active on the new project.
  - Given `maf-agents.slnx`, when it is opened, then `/src/`, `/tests/`, and `/samples/02-agents/` folders exist with the new projects, and `dotnet format --verify-no-changes` passes.

#### US-102: `[enabler]` Terminal.Gui application host and thread marshalling

- **Story**: `[enabler]` Wrap the Terminal.Gui v2 lifecycle and the `Invoke` marshalling rule in one internal host so no view is ever touched from the thread pool and the terminal is always restored. Blocks EP-2's picker and every story in EP-3 and EP-4.
- **Priority**: P0 · **Estimate**: M · **Depends on**: US-101
- **Acceptance criteria**:
  - Given the host, when it starts, then it follows `Application.Create()` → `app.Init(driver)` → `Application.Run(…)` → `app.Dispose()` in that order, and `Dispose` is in a `finally` so it runs on every exit path.
  - Given a top view that throws during `Run`, when the exception unwinds, then the terminal is restored to cooked mode, the message is printed on the restored terminal, and the shell prompt afterwards is usable — verified by running the failing case and typing a command.
  - Given a background caller posting an update, when it uses the marshalling helper, then the delegate executes on the UI thread; given the same caller invoking a view member directly, when the unit test runs, then it fails — the helper is the only sanctioned route.
  - Given the marshalling helper is called with a `null` delegate, when it runs, then it throws `ArgumentNullException` naming the parameter.

#### US-103: Build a tracked agent from a provider selection

- **Story**: As a sample author, I want one call that turns a resolved provider selection into a ready `AIAgent` with tracking wired, so my sample's setup is one statement whatever endpoint it runs against.
- **Priority**: P0 · **Estimate**: M · **Depends on**: US-101
- **Acceptance criteria**:
  - Given a selection with `Api: Responses`, when the factory is called, then it builds `OpenAIClient(credential, options).GetResponsesClient().AsIChatClient(model)`; given `Api: ChatCompletions`, then it builds `…GetChatClient(model).AsIChatClient()` — and the two paths differ in exactly one expression, with the pipeline, agent, and options built afterwards identical.
  - Given either surface, when the pipeline is built, then `UseToolTracking` is applied before `UseFunctionInvocation`, and both `UseAgentToolClassification` and `UseMcpToolClassification` are enabled.
  - Given the returned agent, when a turn invokes a registered function tool, then the tool executes exactly once — proving `ChatClientAgent` did not insert a second `FunctionInvokingChatClient` around the pre-built pipeline.
  - Given a selection with a non-null `Endpoint`, when the client is built, then that base URL is used; given a null `Endpoint`, then the OpenAI default is used and no endpoint is set explicitly.
  - Given the `OPENAI001` evaluation-only attribute, when the library builds, then the suppression is a `#pragma` scoped to the Responses branch only, not a project-wide `NoWarn`, and the Chat Completions branch carries no suppression.
  - Given a `null` options argument, when the factory is called, then it throws `ArgumentNullException` naming the parameter.

#### US-104: Route tracking events out of band

- **Story**: As a sample reader, I want a tool-calling turn to be followed safely by another turn in the same conversation, so the conversation does not break the moment the agent uses a tool.
- **Priority**: P0 · **Estimate**: M · **Depends on**: US-103
- **Acceptance criteria**:
  - Given the pipeline, when it is constructed, then `ToolTrackingOptions.EmitProgressContent` and `EmitUsageReportContent` are both `false`, and tracking reaches the renderer through `IChatProgressObserver` → channel → `AssistantStatusReducer` instead. This is a settled decision from the earlier spike — Andes' synthetic content is not part of the `AIContent` polymorphic serialization contract and would corrupt `AgentSession` — and is not to be re-investigated.
  - Given a turn that invokes a function tool, when `SerializeSessionAsync(session)` is called afterwards, then it returns a `JsonElement` without throwing and the serialized history contains no progress or usage content.
  - Given the same session, when a second turn is sent, then the provider accepts the request without a 4xx error.
  - Given an observer whose target turn has already completed its channel, when an event arrives, then the event is dropped without throwing — the observer is invoked synchronously on arbitrary threads and must never block or fail.
  - Given a turn enumerated twice, when the second enumeration starts, then it throws `InvalidOperationException` rather than silently replaying.

#### US-105: Keep credentials out of every rendered string

- **Story**: As a sample reader, I want to be able to screenshot or paste my terminal without leaking a key, so demoing a sample is safe.
- **Priority**: P0 · **Estimate**: S · **Depends on**: US-103
- **Acceptance criteria**:
  - Given a resolved sentinel credential, when a full session runs and all output is captured, then the sentinel appears nowhere in it — asserted by a test that scans captured output.
  - Given a provider error body containing `Incorrect API key provided: sk-abc…************…wxyz`, when it is rendered, then the key-shaped substring is redacted and the rest of the message survives.
  - Given the configuration type holding a credential, when it is interpolated into a string or inspected in a debugger, then `ToString()` reports the provider and model and never the credential — so it must not be a positional `record`.
  - Given a credential of a shape the redactor does not recognize (for example an Azure key with no `sk-` prefix), when it is deliberately echoed by a stub provider, then the test documents the limitation and the library still never renders the credential it holds itself.

### EP-2: Provider and model selection

#### US-201: One gitignored configuration file reaching every sample

- **Story**: As a sample reader, I want to describe every endpoint I use once, in one file that git will never see, so I configure the repository rather than each sample.
- **Priority**: P0 · **Estimate**: M · **Depends on**: US-101
- **Acceptance criteria**:
  - Given `config/appsettings.json` at the repo root, when `git status` runs after editing it, then it is not listed — because `.gitignore` gained a `config/appsettings.json` entry — while `config/appsettings.sample.json` is tracked and contains no credential.
  - Given the file exists, when a consuming sample is built, then the single `Content` link in `Directory.Build.props` copies it to the sample's output directory as `appsettings.json`, and the running sample reads exactly that copy.
  - Given the file does not exist, when a consuming sample is built and run, then the build succeeds with no missing-file error and the shell falls back to the built-in provider (`OpenAI`, `Responses`, `OpenAI:ApiKey` / `OPENAI_API_KEY`, `gpt-4o-mini`), matching `HelloAgent`'s behaviour.
  - Given a file with malformed JSON, an unknown `Api` value, an empty `Providers[]` array, or a `DefaultProvider` naming no configured provider, when the sample starts, then it prints a message naming the offending entry and exits `1` before Terminal.Gui is initialized, with no stack trace.
  - Given the schema, when it is bound, then `Endpoint` and `ApiKeyRef` are optional and `Name`, `Api`, and a non-empty `Models[]` are required — covered by a unit test per case.

#### US-202: Resolve a provider's credential from its named reference

- **Story**: As a sample reader, I want the shell to find my key wherever I put it and tell me exactly what to type when it cannot, so a missing key is a two-second fix rather than a stack trace.
- **Priority**: P0 · **Estimate**: M · **Depends on**: US-201
- **Acceptance criteria**:
  - Given a provider whose `ApiKeyRef` is `OpenAI:ApiKey` and a value in the calling sample's user-secrets, when the provider is selected, then the credential resolves — proving user-secrets bind to the caller's `UserSecretsId` via a supplied assembly, not the library's.
  - Given no user-secret but the corresponding environment variable set (accepting both `OpenAI__ApiKey` and the flat `OPENAI_API_KEY` form), when the provider is selected, then that value is used; given both, then user-secrets win.
  - Given the referenced value set to an empty or whitespace string, when the provider is selected, then it is treated as absent.
  - Given no value from any source, when the provider is selected, then the message names the reference and the exact command — `dotnet user-secrets set "OpenAI:ApiKey" "sk-..." --project samples/02-agents/TrackedAgent` — using the calling sample's project path from options, with no stack trace and no key fragment.
  - Given a provider with no `ApiKeyRef` at all (Ollama, LM Studio), when it is selected, then no credential is required, no message appears, and the client is built with a placeholder credential the endpoint ignores.

#### US-203: Pick a provider and model at startup

- **Story**: As a local-model developer, I want to choose the endpoint and model when the sample starts, so I can compare the same sample across providers without editing anything.
- **Priority**: P0 · **Estimate**: M · **Depends on**: US-102, US-201, US-202
- **Acceptance criteria**:
  - Given three configured providers, when the sample starts, then a Terminal.Gui view lists them with their models, with `DefaultProvider` and `DefaultModel` preselected and focus on the confirm action.
  - Given a provider is highlighted, when the selection changes, then the model list updates to that provider's `Models[]` and no model from another provider remains selectable.
  - Given a provider whose credential is missing, when it is confirmed, then the actionable message from US-202 appears inline and the picker stays open so another provider can be chosen; the process does not exit.
  - Given the picker is dismissed without a selection, when the shell would open, then the process exits `1` with a message and the terminal restored.
  - Given the whole picker, when it is operated, then every action is reachable by keyboard alone with no mouse.

#### US-204: Skip the picker when the defaults are unambiguous

- **Story**: As a sample reader with one provider and one model, I want the sample to start straight into the conversation, so the common case costs no keystrokes.
- **Priority**: P1 · **Estimate**: S · **Depends on**: US-203
- **Acceptance criteria**:
  - Given exactly one configured provider with exactly one model and a resolvable credential, when the sample starts, then the chat shell opens directly and no picker is shown.
  - Given more than one provider or more than one model on the default provider, when the sample starts, then the picker is shown.
  - Given `Ui.AlwaysShowPicker` is `true`, when the sample starts, then the picker is shown even when the defaults are unambiguous.
  - Given the picker is skipped, when the shell opens, then the status bar shows the provider and model that were chosen for the reader.

#### US-205: Fail actionably when a provider does not implement Responses

- **Story**: As a local-model developer, I want a misconfigured API surface to tell me which line to change, so an Ollama endpoint does not fail as an opaque 404.
- **Priority**: P1 · **Estimate**: S · **Depends on**: US-103, US-202
- **Acceptance criteria**:
  - Given a provider declaring `Api: "Responses"` against an endpoint that returns 404 for `/responses`, when the first turn runs, then the message names the provider, the endpoint, and the `"Api": "ChatCompletions"` fix in `config/appsettings.json`, and the raw 404 body is not the primary text shown.
  - Given the same failure, when it is handled, then the shell stays open so the reader can quit cleanly rather than the process terminating.
  - Given an unrelated transport failure (DNS, connection refused, 401), when it occurs, then the message reflects the actual failure and does not wrongly suggest the `Api` fix.

### EP-3: Terminal.Gui chat shell

#### US-301: Render a live activity tree while the agent runs

- **Story**: As a sample reader, I want to see each tool call appear and complete while the agent works, so I can tell what the agent is actually doing.
- **Priority**: P0 · **Estimate**: L · **Depends on**: US-102, US-104
- **Acceptance criteria**:
  - Given a turn that invokes a function tool, when the tool starts, then a node appears in the tree within one redraw interval carrying a `fn` badge, the tool name, and a running elapsed time.
  - Given an activity started inside another activity, when it is rendered, then it appears nested under its parent rather than as a sibling.
  - Given an activity that reports a sub-status or a progress fraction, when the report arrives, then the node's sub-status or progress updates without the node being duplicated.
  - Given a completed activity, when the turn continues, then the node shows a terminal status as a word — not by colour alone — plus a final elapsed time, and stops updating.
  - Given every node update, when the code path is reviewed and exercised by the marshalling test, then each one reaches the view through `Application.Invoke` and never directly from the turn's thread.

#### US-302: Stream the answer into the answer pane

- **Story**: As a sample reader, I want the answer to appear as it is generated, beside the activity tree, so I see progress rather than a frozen screen.
- **Priority**: P0 · **Estimate**: M · **Depends on**: US-301
- **Acceptance criteria**:
  - Given a turn producing text, when deltas arrive, then the accumulated answer grows in the answer pane and the pane scrolls to keep the newest text visible.
  - Given the turn completes, when the shell settles, then the full answer is present exactly once — not duplicated by a final re-render.
  - Given a turn that produces tool calls but no text, when it completes, then the answer pane shows the previous turn's content unchanged rather than an empty flash.
  - Given an answer longer than the pane, when the reader focuses the pane and presses `PgUp`, then earlier text scrolls into view while the turn continues streaming.

#### US-303: Throttle redraws before marshalling

- **Story**: As a sample reader, I want the shell to stay responsive during a fast stream, so a long answer does not make the window stutter or the input freeze.
- **Priority**: P0 · **Estimate**: S · **Depends on**: US-301
- **Acceptance criteria**:
  - Given a snapshot stream emitting 500 snapshots in one second, when the throttle is applied, then no more than 13 `Invoke` calls are made — asserted by a unit test counting marshalling calls against a fake `TimeProvider`.
  - Given the first snapshot of a turn, when it arrives, then it passes the gate immediately so the first activity node appears without waiting an interval.
  - Given the final snapshot of a turn, when the turn ends, then it is always rendered regardless of the gate, so the last frame is never a stale one.
  - Given `Ui.RedrawIntervalMilliseconds` set to a negative number, when the shell is constructed, then it throws `ArgumentOutOfRangeException` naming the property.

#### US-304: Show the active provider and model in the status bar

- **Story**: As a sample reader, I want to see which endpoint and model I am talking to at all times, so I never mistake a local model's answer for a hosted one.
- **Priority**: P1 · **Estimate**: S · **Depends on**: US-301, US-203
- **Acceptance criteria**:
  - Given a session started with any provider, when the shell is open, then the status bar shows the provider name and the model id for the whole session.
  - Given a provider whose name or model contains control characters from a hand-edited configuration file, when it renders, then the text is sanitized and the layout is intact.
  - Given the status bar, when a turn is running, then it also shows a running indicator, and when idle, then it does not.

#### US-305: Sanitize untrusted text before it reaches any surface

- **Story**: As a sample reader, I want a hostile or malformed tool result to render harmlessly, so a tool cannot repaint or hijack my terminal.
- **Priority**: P0 · **Estimate**: S · **Depends on**: US-101
- **Acceptance criteria**:
  - Given text containing `\x1b[2J\x1b[H` and a raw `\r`, when it passes through the sanitizer, then the escape sequence and the stray carriage return are removed and the surrounding text survives — asserted by a unit test with the exact expected output.
  - Given ordinary text containing newlines and tabs, when it passes through the sanitizer, then those are preserved and the text is otherwise byte-for-byte unchanged.
  - Given the whole library, when the render paths are reviewed, then model output, tool arguments and results, MCP server messages, provider error bodies, and configuration-supplied names all pass through this one helper before reaching a Terminal.Gui view or the plain transcript.
  - Given a tool result that is sanitized, when it is also redacted by US-105's key-shape rule, then both apply and the order does not change the result.

#### US-306: Hold a multi-turn conversation over one session

- **Story**: As a sample reader, I want to ask follow-up questions in the same conversation, so I can see `AgentSession` doing its job.
- **Priority**: P0 · **Estimate**: M · **Depends on**: US-301, US-302
- **Acceptance criteria**:
  - Given a first turn that establishes a fact, when a second turn refers to it by pronoun, then the answer resolves the reference — proving both turns used the same `AgentSession` from `CreateSessionAsync()`.
  - Given a turn is running, when the reader types in the input field, then the field is read-only and `Enter` starts no second turn.
  - Given blank or whitespace-only input, when `Enter` is pressed, then nothing is submitted, no model call is made, and focus stays in the input field.
  - Given a turn that failed or was cancelled, when the next turn runs, then it succeeds on the same session rather than failing on inconsistent history.

#### US-307: Distinguish failed and cancelled activities and keep the final tree

- **Story**: As a sample reader, I want a failed or cancelled step to look different from a successful one and to stay on screen, so I can see where a run went wrong.
- **Priority**: P1 · **Estimate**: S · **Depends on**: US-301, US-302, US-305
- **Acceptance criteria**:
  - Given a tool that throws, when its node reaches a terminal state, then the node shows a failed status word — not a colour alone — plus the sanitized exception message.
  - Given a cancelled turn, when it unwinds, then the in-flight node shows a cancelled status and no node is left in a running state.
  - Given the turn ends for any reason, when the next prompt is accepted, then the previous turn's tree remains on screen as a static summary until the new turn replaces it.
  - Given `NO_COLOR` is set or the terminal is monochrome, when any terminal status renders, then succeeded, failed, and cancelled remain distinguishable by their words alone.

### EP-4: Keyboard control, cancellation, and exit contract

#### US-401: Cancel the turn in flight from the keyboard

- **Story**: As a sample reader, I want `Esc` to stop a long-running turn without ending my session, so I can retry a different prompt.
- **Priority**: P0 · **Estimate**: M · **Depends on**: US-306
- **Acceptance criteria**:
  - Given a turn in flight, when `Esc` is pressed or the Cancel button is activated, then the run's `CancellationToken` is cancelled, the in-flight node is marked cancelled, and the input field becomes writable again within 1 second.
  - Given no turn is running, when `Esc` is pressed, then nothing happens — `Esc` never quits and never closes the window.
  - Given a cancellation, when the turn unwinds, then `OperationCanceledException` is handled inside the shell and never surfaces as an unhandled exception or a crash dialog.
  - Given a cancelled turn, when a new prompt is submitted, then it runs normally on the same session.
  - Given `Console.CancelKeyPress`, when the TUI path is reviewed, then it is not subscribed at all — the driver owns the keyboard.

#### US-402: Quit cleanly and restore the terminal

- **Story**: As a sample reader, I want `Ctrl+Q` to end the session and hand my terminal back in working order, so the next command I type behaves normally.
- **Priority**: P0 · **Estimate**: S · **Depends on**: US-102, US-306
- **Acceptance criteria**:
  - Given an idle shell, when `Ctrl+Q` or File → Quit is used, then the application disposes and the process exits `0`.
  - Given a turn in flight, when `Ctrl+Q` is used, then the turn is cancelled first and the process still exits `0` without waiting for the model.
  - Given any exit path — normal quit, startup configuration failure, or an unhandled exception — when the process terminates, then the terminal is restored to cooked mode with the cursor visible and no partial control sequence on screen, verified by typing a command immediately afterwards.
  - Given a startup configuration failure, when the process exits, then the code is `1`; given a normal quit, then `0`. No TUI path returns `130`.

#### US-403: Publish the key map in-app

- **Story**: As a sample reader, I want to see which keys do what without leaving the shell, so I do not have to guess or read the README mid-session.
- **Priority**: P1 · **Estimate**: S · **Depends on**: US-401, US-402
- **Acceptance criteria**:
  - Given the shell is open, when `F1` or Help → Keys is used, then a dialog lists every binding in section 5's key map, and `Esc` closes the dialog without cancelling a running turn.
  - Given the status bar, when the shell is idle or busy, then the two most important bindings for that state (`Ctrl+Q` idle, `Esc` busy) are always visible without opening the dialog.
  - Given the key map dialog, when it is open, then it is dismissible and navigable by keyboard alone.

### EP-5: Token accounting

#### US-501: Show a per-turn token footer

- **Story**: As a sample reader, I want each turn to report its input, output, and total tokens, so I can see what a single question costs.
- **Priority**: P0 · **Estimate**: S · **Depends on**: US-104, US-301
- **Acceptance criteria**:
  - Given a completed turn whose response reports usage, when the turn settles, then the footer shows input, output, and total counts, and total equals input plus output.
  - Given a turn whose response reports no usage, when it completes, then the footer shows `n/a` for the missing values rather than `0`.
  - Given the out-of-band tracking route, when a turn completes, then the footer values come from the final `ChatUsageReport` and match the values the provider reported — asserted by a unit test over the report-to-footer mapping.
  - Given `Ui.ShowUsage` is `false`, when a turn completes, then no footer is shown and the session total is still accumulated internally.

#### US-502: Accumulate a running session total

- **Story**: As a sample reader, I want a running total across the whole session, so I know what the conversation has cost so far.
- **Priority**: P1 · **Estimate**: S · **Depends on**: US-501
- **Acceptance criteria**:
  - Given three completed turns, when the third footer updates, then the session total equals the arithmetic sum of the three per-turn totals — asserted by an xUnit test over `SessionUsage`.
  - Given a turn that reports no usage, when it completes, then the session total is unchanged and no exception is thrown.
  - Given the session ends by `Ctrl+Q` or end of piped input, when the process exits, then the final session total is shown once on the restored terminal.

#### US-503: Show per-activity token counts on tree nodes

- **Story**: As a sample reader, I want to see which tool consumed which share of the tokens, so I can spot an expensive step.
- **Priority**: P2 · **Estimate**: M · **Depends on**: US-501
- **Acceptance criteria**:
  - Given an activity whose tracking report includes token counts, when its node reaches a terminal state, then the node shows those counts.
  - Given an activity whose report includes no counts, when its node renders, then the token field is omitted entirely rather than shown as `0`.
  - Given all nodes in a turn report counts, when the turn ends, then the sum of node counts is less than or equal to the turn total — a nested agent's tokens are attributed to its calling scope once, not twice — asserted by a unit test on the merge logic.

### EP-6: Headless transcript path

#### US-601: Detect a non-TTY and run the plain path

- **Story**: As a sample reader piping output to a file or running in CI, I want the sample to run and print a readable transcript, so a redirected run does not crash.
- **Priority**: P0 · **Estimate**: M · **Depends on**: US-103, US-202, US-305
- **Acceptance criteria**:
  - Given `Console.IsOutputRedirected` is true or no TTY is available, when the sample starts, then Terminal.Gui is never initialized — no `Application.Create()` is called — and no exception is thrown.
  - Given the plain path, when a turn completes, then stdout contains one line per activity with its badge, terminal status, and elapsed time, followed by the full answer and the token footer, all sanitized by the same helper as the TUI.
  - Given output redirected to a file (`dotnet run … > out.txt`), when the process exits, then the file contains no ANSI or cursor-control sequence at all.
  - Given the plain path, when a provider must be chosen, then `DefaultProvider` and `DefaultModel` are used without prompting, and an ambiguous or unresolvable default is a `1` exit with the same actionable message.

#### US-602: Read prompts from redirected stdin

- **Story**: As a sample reader scripting a run, I want to pipe prompts in, so I can capture a transcript without typing.
- **Priority**: P1 · **Estimate**: M · **Depends on**: US-601
- **Acceptance criteria**:
  - Given `echo "hello" | dotnet run --project samples/02-agents/TrackedAgent`, when the process runs, then exactly one turn executes and the process exits `0`.
  - Given multi-line piped input, when it is consumed, then each non-blank line becomes one turn in order and blank lines are skipped.
  - Given end-of-input, when the read returns null, then the loop ends with exit code `0` rather than looping forever or throwing.
  - Given input of `exit` or `quit` on the plain path, when it is read, then the session ends with exit code `0`.

#### US-603: Honour SIGINT on the plain path

- **Story**: As a sample reader running a piped session, I want `Ctrl+C` to stop it the way every other CLI does, so the headless path behaves like a normal program.
- **Priority**: P1 · **Estimate**: S · **Depends on**: US-601
- **Acceptance criteria**:
  - Given a turn in flight on the plain path, when SIGINT arrives, then the turn is cancelled, a cancellation notice prints, and the process exits `130`.
  - Given the plain path exits by any route, when it terminates, then the `CancelKeyPress` handler is detached before the `CancellationTokenSource` is disposed, so no `ObjectDisposedException` is observable.
  - Given the TUI path, when the same code is reviewed, then this handler is not installed — confirming the two paths do not both claim the key.

### EP-7: MCP tool source

#### US-701: Attach an MCP server's tools to the agent

- **Story**: As a sample author, I want to name an MCP server and have its tools attached to my agent, so a sample demonstrating MCP is a few lines long.
- **Priority**: P1 · **Estimate**: M · **Depends on**: US-103
- **Acceptance criteria**:
  - Given an in-process source, when it connects, then a real JSON-RPC session runs over **two** paired in-memory pipes — one per direction, because a single bidirectional stream loops each side's writes back to itself — with the server's message loop started before the client connects, since `McpClient.CreateAsync` awaits the `initialize` response.
  - Given the connected source, when `ListToolsAsync()` returns, then its tools appear in the agent's tool list as `AITool` values wrapped with Andes' `WithTracking`.
  - Given a stdio source whose command does not exist or exits immediately, when connection is attempted, then `ConnectAsync` returns `false`, `FailureReason` explains why, a message names the source, the shell starts with the remaining tools, and no unhandled exception escapes.
  - Given an MCP tool invoked by the model, when it completes, then its node carries the `mcp` badge rather than `fn`, confirming `UseMcpToolClassification` is active.
  - Given the shell shuts down, when the source is disposed with `await using`, then the server is torn down and no orphaned process or leaked pipe remains.

#### US-702: Surface MCP progress on the owning node

- **Story**: As a sample reader, I want a long MCP call to show progress, so a slow round-trip does not look like a hang.
- **Priority**: P1 · **Estimate**: M · **Depends on**: US-701, US-301
- **Acceptance criteria**:
  - Given a tracked client, when a tool sends `notifications/progress`, then the owning node's progress indicator advances toward the reported total.
  - Given a tool that sends progress with no known total, when notifications arrive, then the node shows an indeterminate indicator rather than a bar stuck at zero.
  - Given a tool that sends no progress notifications at all, when it runs, then its node shows elapsed time only and no empty progress indicator is drawn.
  - Given progress notifications arriving faster than the redraw interval, when they are rendered, then they pass through the same producer-side throttle as every other snapshot.

### EP-8: TrackedAgent showcase sample

#### US-801: `samples/02-agents/TrackedAgent` runs end to end

- **Story**: As a sample reader, I want one sample that shows the whole shell working, so I can copy a known-good starting point.
- **Priority**: P0 · **Estimate**: M · **Depends on**: US-301, US-302, US-401, US-402
- **Acceptance criteria**:
  - Given a resolvable credential, when `dotnet run --project samples/02-agents/TrackedAgent` starts, then the shell opens and a prompt exercising the sample's function tool produces at least one `fn` node and a streamed answer.
  - Given the sample's `Program.cs`, when reviewed, then it declares only the agent name, instructions, tools, and one `AgentShell.RunAsync(options)` call, and its plumbing occupies ≤ 12 non-blank lines.
  - Given the sample project file, when reviewed, then it references `src/MafAgents.Tui` by `ProjectReference`, carries its own `UserSecretsId`, and declares no `Version` attribute on any `PackageReference`.
  - Given no credential configured, when the sample starts, then the message names `samples/02-agents/TrackedAgent` and the process exits `1` without a stack trace.
  - Given a second provider added to `config/appsettings.json`, when the sample is rebuilt and run, then that provider is selectable and usable with no change to any `.cs` file.

#### US-802: Demonstrate a nested agent tool

- **Story**: As a sample reader, I want to see a sub-agent invoked as a tool and rendered as a nested node, so I understand agent composition and how it appears at runtime.
- **Priority**: P1 · **Estimate**: S · **Depends on**: US-801
- **Acceptance criteria**:
  - Given a secondary agent exposed through Andes' `WithTracking`, when the primary agent calls it, then a node with the `agent` badge appears nested under the calling activity — not a plain `fn` node with no tokens.
  - Given the sub-agent is built on a plain, untracked chat client, when its tokens are attributed, then they are counted once against the calling scope and not twice — asserted by the US-503 merge test or, if US-503 is cut, by a direct test on the attribution helper.
  - Given the nested agent itself calls a function tool, when it runs, then that call renders as a further nested `fn` node with its own elapsed time.

#### US-803: Ship an in-process demo MCP server with the sample

- **Story**: As a sample reader, I want the MCP part of the sample to work with no external install, so `dotnet run` is the only prerequisite beyond a credential.
- **Priority**: P1 · **Estimate**: M · **Depends on**: US-701, US-702
- **Acceptance criteria**:
  - Given a clean machine with only the .NET SDK and a credential, when the sample runs, then the MCP demo executes with no `npx`, no network fetch, and no additional install step.
  - Given the demo server's long-running tool, when it is invoked, then it emits `notifications/progress` at least three times and the node's progress advances visibly between them.
  - Given the demo server runs in this process, when the shell's stdin and stdout are considered, then the server uses in-memory pipes and never shares the console streams, so no stray write can corrupt protocol framing and cancelling a turn cannot kill the server.
  - Given the sample exits, when the process terminates, then the demo server is shut down and no orphaned resource remains.

## 8. Milestones & rollout

**Phase 1 — Foundation** (US-101, US-102, US-103, US-104, US-105). Roll-up: L. Nothing else can start until the projects exist, the Terminal.Gui host and its marshalling rule are in place, and the tracked agent builds on either API surface. US-101 is the only story with no dependency, so it is first.

**Phase 2 — Provider and model selection** (US-201, US-202, US-203). Roll-up: M. The configuration file, its credential references, and the picker. At the end of this phase the "one file, zero lines of C#" success criterion is measurable.

**Phase 3 — MVP: a visible, cancellable turn** (US-301, US-302, US-303, US-305, US-306, US-401, US-402, US-501, US-601, US-801). Roll-up: L. The smallest set that delivers the headline value: a sample whose plumbing is ≤ 12 lines, whose tool calls are visible, whose answer streams, whose turn can be cancelled, whose token cost is shown, and which does not crash when piped. Three of the five success criteria become measurable here.

**Phase 4 — Polish** (US-204, US-205, US-304, US-307, US-403, US-502, US-602, US-603, US-802). Roll-up: M. Picker skip, the Responses-surface error, the status bar, failed and cancelled states, the in-app key map, session totals, scripted runs, SIGINT on the plain path, and the nested agent demo.

**Phase 5 — MCP** (US-701, US-702, US-803). Roll-up: M. MCP is late because nothing else depends on it and the shell is useful without it.

**Phase 6 — Optional** (US-503). Roll-up: M. Per-activity token counts; the first thing to cut.

Calendar dates and team composition are deliberately absent — see the assumption in section 9.

**Risks & mitigations**

| Risk | Mitigation |
| --- | --- |
| Terminal.Gui `Invoke` marshalling buckles under a high-frequency snapshot stream — snapshots arrive per text delta, so a 500-delta turn could enqueue 500 closures on the UI thread. | Throttle on the producer side *before* `Invoke` (US-303) and coalesce to latest-snapshot-wins, since snapshots are cumulative. Assert the rate with a unit test against a fake `TimeProvider`, and add the ≥ 500-delta soak to the scripted run's pass threshold. |
| Terminal.Gui 2.4.x is a young v2 line on a fast release cadence — `2.4.18-develop.31` is already published while 2.4.17 is the newest stable, and NuGet's flat-container and gallery views disagree about whether a 2.4.18 stable exists. | Pin exactly `[2.4.17]`, and re-check the feed at implementation time for a genuine 2.4.18 stable. Keep every view behind a thin internal renderer abstraction shared with the plain path, so a breaking Terminal.Gui change is contained to one folder. |
| A provider declaring `Responses` against an endpoint that lacks it fails as an opaque 404, which reads as "the sample is broken". | US-205 maps the failure to a message naming the provider, endpoint, and the `"Api": "ChatCompletions"` fix; `config/appsettings.sample.json` ships Ollama and OpenRouter already marked `ChatCompletions`. |
| `CentralPackageTransitivePinningEnabled` plus divergent floors — Andes 0.5.0 requires `Microsoft.Extensions.AI` ≥ 10.8.3 while Agent Framework 1.17.0 requires ≥ 10.7.0 — causes NU1605/NU1608, and warnings are errors here. | Pin `Microsoft.Extensions.AI` and `Microsoft.Extensions.AI.Abstractions` explicitly at 10.8.3 in US-101. Terminal.Gui's `Microsoft.Extensions.Configuration*` 10.0.7 floors are already satisfied by the repo's 10.0.9 pins. Treat any restore warning here as a blocking review item. |
| Terminal.Gui pulls a wider transitive set than Spectre did — ColorHelper, JetBrains.Annotations, Markdig, System.IO.Abstractions, TextMateSharp, TextMateSharp.Grammars, Wcwidth, plus `Microsoft.Extensions.Configuration/.Binder/.Json/.Logging.Abstractions/.Options` — widening the `NuGetAudit` surface. | `WarningsNotAsErrors` already exempts NU1901–NU1904, so a future advisory stays visible without breaking clones. The obligation is to watch, not to gate. Record the widened surface in the sample's README so it is not a surprise. |
| A crash, a SIGINT the driver does not trap, or an exception during `Run` leaves the terminal in raw mode with no cursor. | US-402 puts `app.Dispose()` in a `finally` and adds a top-level handler; its acceptance criteria require typing a command successfully after a deliberately failed run. |
| Untrusted tool or provider text containing ANSI escape sequences drives the terminal directly — the hazard that replaces Spectre's markup-parsing hazard. | US-305 makes control-character and ANSI sanitization mandatory, unit-tested with exact expected output, and P0 rather than a polish item. |
| Two libraries owning the screen. | Spectre.Console is removed entirely; US-101's acceptance criteria assert `rg -i spectre` finds nothing, and US-601 asserts Terminal.Gui is never initialized on the plain path. |
| The configuration file is gitignored, so a fresh clone has none and the reader hits an unhelpful failure. | US-201 defines the absent-file fallback to a built-in OpenAI provider matching `HelloAgent`, ships a tracked `config/appsettings.sample.json`, and makes a malformed file fail with the offending entry named. |
| Andes is a 0.x line; a minor release may break the API. | Pin the four Andes packages with bracket notation (`[0.5.0]`) so a floor bump cannot silently resolve a breaking minor. |
| Verification depends on a live model, so results vary run to run. | Pass thresholds are structural — badge types present, terminal statuses reached, totals reconcile, no cross-thread exception — not answer-content-based, and are checked over 3 consecutive runs. |
| Third-party MCP servers execute arbitrary local processes. | The showcase ships an in-process demo server (US-803); `McpToolSource` never discovers servers, it only launches a command the sample states explicitly. |

**Rollout & rollback**

The change is purely additive: two new projects, one new sample, `config/appsettings.sample.json`, and edits to `Directory.Packages.props`, `Directory.Build.props`, `.gitignore`, `maf-agents.slnx`, and the root `README.md` samples table. Nothing at runtime is replaced, because `HelloAgent` — the existing sample and the documented raw baseline — is untouched and keeps working regardless, so no feature flag is warranted. Back-out is a revert of the PR; nothing outside `src/`, `tests/`, `config/`, and `samples/02-agents/` references the new code. If EP-7 slips, the sample ships without the MCP demo and the MCP stories move to a follow-up PR, since no other epic depends on them. If Terminal.Gui itself proves unworkable at implementation time, the headless renderer from EP-6 is already a complete, shipping path and the TUI can be deferred without abandoning the library.

## 9. Assumptions & open questions

**Assumptions**

- The personas in section 3 are inferred from the repository's stated purpose; the request did not name users. The local-model developer persona is inferred from the decision to support Ollama and LM Studio.
- Calendar dates, team size, and velocity are unknown for a single-maintainer repository, so section 8 gives dependency-ordered phases and T-shirt roll-ups only. Any date is `TBD`.
- The key map in section 5 is a design choice made here, not a given requirement — in particular that `Esc` cancels but never quits, that `Ctrl+Q` is the only quit binding, and that `Ctrl+C` is left at Terminal.Gui's default rather than rebound. Veto any of these if a different convention is preferred.
- Exit codes are `0` normal, `1` startup configuration failure, `130` SIGINT on the plain path only. There is deliberately no distinct code for a turn-level failure, because a turn-level failure does not end the process.
- The shared configuration lives at `config/appsettings.json` with `Link="appsettings.json"` and `CopyToOutputDirectory="PreserveNewest"`. Because the `Content` item in `Directory.Build.props` is guarded only by `Exists(…)`, it also copies the file into `HelloAgent`'s output directory, where nothing reads it; `HelloAgent`'s source and behaviour are unchanged either way. If a stray file in that output is unacceptable, the refinement is to move the item to a new root `Directory.Build.targets` — which is imported after the project body and can therefore see it — gated on `'$(UseSharedAppSettings)' == 'true'` set by each consuming sample.
- `.gitignore` gains the single precise entry `config/appsettings.json` rather than a broad `appsettings.json` pattern, so a future legitimately-tracked `appsettings` file is not silently ignored.
- The absent-file fallback provider is `OpenAI` / `Responses` / `OpenAI:ApiKey` / `gpt-4o-mini`, chosen to match `HelloAgent` and the root `README.md` exactly.
- The default redraw throttle is 80 ms, carried over from the previous design, and is configurable through `Ui.RedrawIntervalMilliseconds`.
- Terminal.Gui is pinned at `[2.4.17]` — verified as the newest listed stable, with only `2.4.18-develop.*` prereleases above it. Re-check the feed at implementation time in case a genuine 2.4.18 stable has shipped by then.
- `ModelContextProtocol` is pinned at `2.1.0`, matching Andes 0.5.0's `ModelContextProtocol.Core` floor and the version the earlier spike ran against. `2.2.0` is also a listed stable release and may be taken instead if its API is unchanged.
- `Microsoft.Extensions.AI` and friends are pinned at `10.8.3`, the Andes floor. `10.9.0` is also stable; the lower pin is chosen to keep the graph at the minimum that satisfies every declared floor.
- Project names are `src/MafAgents.Tui` and `tests/MafAgents.Tui.Tests`, and the single sample-facing entry point is `AgentShell.RunAsync(options)`. The untracked `src/MafAgents.Interactive` build artifacts left by the reverted implementation are deleted as part of US-101.
- Terminal.Gui driver selection is left to the library's default (`app.Init` with no explicit driver); the PRD does not mandate one.
- The picker chooses a provider and a model only. Relaxing the "no menu-driven playground" non-goal for the picker does not relax it for sample discovery.
- A provider with no `ApiKeyRef` is built with a placeholder credential, because `OpenAIClient` requires an `ApiKeyCredential` even when the endpoint ignores it.
- The showcase's function tools, nested agent, and demo MCP tool are simple deterministic stubs (a weather lookup, a reviewer sub-agent, a paced corpus search) rather than anything calling an external service.
- The repository has no CI workflow and no telemetry or logging conventions today, so success criteria are measured by local commands, unit tests, and PR review rather than by emitted events. No telemetry story is proposed for that reason.
- Documentation outputs — the sample `README.md`, the root `README.md` samples row, any ADR for the new dependencies, and the `CHANGELOG.md` entry — are handled by the `se-technical-writer` flow after implementation, per `.claude/CLAUDE.md`, and are therefore deliberately absent from section 7.

**Open questions**

- Should the picker remember the last selection in a small untracked state file? It conflicts with the "no session persistence" non-goal but would remove a keystroke from every run. — repository owner.
- Should `DefaultModel` be per-provider rather than global? A single global default is odd when providers do not share model names. — repository owner.
- Should `Api` be auto-detected by probing the endpoint once and caching, instead of being declared per provider? Declaring it is simpler and offline-safe, but it is one more thing to get wrong. — repository owner.
- Should the shell offer a mid-session provider switch, or is quit-and-restart acceptable? — repository owner.
- Does taking Terminal.Gui (a third-party UI framework) and Andes (a 0.x library) as dependencies in a Microsoft-samples repository warrant an ADR alongside ADR-0001? — repository owner, drafted by `se-technical-writer`.
- Should `MafAgents.Tui` also expose a non-interactive `RunOnceAsync` for a future sample that wants a single answer with no shell at all? — repository owner.
