# CLAUDE.md

Project memory for **maf-agents** — runnable **Microsoft Agent Framework** agent examples in **C#/.NET**. It loads automatically every session and governs how Claude Code works here.

Layout: detailed C# standards in `.claude/rules/csharp.md` auto-apply to `*.cs`; skills and subagents live under `.claude/`; `settings.json` pins the model and reasoning effort (`"effortLevel": "xhigh"`); the root `CHANGELOG.md` is the running record of changes, owned by the `se-technical-writer` subagent.

## This repo

Every sample is a self-contained, runnable console app that demonstrates one Microsoft Agent Framework concept.

```
samples/<NN-category>/<SampleName>/     # e.g. samples/01-get-started/HelloAgent/
```

Categories mirror the official Agent Framework sample taxonomy: `01-get-started`, `02-agents`, `03-workflows`, `04-hosting`, `05-end-to-end`. Each sample folder carries its own `README.md` covering what it shows, prerequisites, and how to run it.

**Stable packages only — never pass `--prerelease`.** This is a standing constraint, not a default to be improved on. It is the reason samples use the OpenAI provider rather than Microsoft Foundry: as of `Microsoft.Agents.AI` 1.17.0 the core framework is GA, but `Microsoft.Agents.AI.Foundry`, `Azure.AI.Projects`, and every `Azure.AI.OpenAI` past 2.1.0 ship prerelease only. If a task seems to need a prerelease package, raise it with the user rather than adding one.

**Provider:** OpenAI direct, via `Microsoft.Agents.AI.OpenAI`. Prefer the Responses client (`client.GetResponsesClient()`) over Chat Completions — it carries the full hosted-tool surface.

**Central package management:** every version lives in `Directory.Packages.props`. A `PackageReference` in a `.csproj` carries no `Version` attribute.

Two deliberate carve-outs from the standards below — deviations by decision, not oversight:

- **Secrets.** The C# standard prefers `DefaultAzureCredential` + Key Vault over secrets. An OpenAI API key has no managed-identity equivalent, so samples read it from `dotnet user-secrets` (which stores outside the repo tree) or the `OPENAI_API_KEY` environment variable. The rule's intent — no secret is ever committed — still holds absolutely.
- **Tests.** The `[ProjectName].Tests` xUnit convention applies to shared library code. Samples call a live model and are not unit-tested.

## Communication & comments (always)

**Responses**

- Lead with the answer or the change. No preamble, no filler, no restating the request.
- Do not re-summarize a plan or diff the user already saw; report only what changed or went wrong.
- Match length to substance — a one-line answer is a complete answer.

**Code comments**

- Comment only what code cannot say: why a decision was made, constraints, non-obvious invariants, workarounds with links.
- Never narrate what code does. No per-function comment quota. No change-narration comments ("added X", "now uses Y").
- XML doc comments on public APIs are API documentation, not comments — that standard stands.

## C# coding standards (always)

Full standards live in `.claude/rules/csharp.md` (auto-applies to `*.cs`). The always-on core:

- Target the latest C# version (currently **C# 14**); honor `.editorconfig`; prefer pattern matching, switch expressions, and `nameof(...)`.
- Prefer **primary constructors**; capture each injected dependency into a `private readonly` `_camelCase` field and use the field in method bodies.
- Prefer **collection expressions** (`[]`, `[1, 2, 3]`, `[.. items]`) over `new List<T>()`, `new T[] { }`, or `Array.Empty<T>()`.
- PascalCase for types, methods, and public members; `_camelCase` private fields; camelCase locals and parameters; `I`-prefixed interfaces.
- Declare variables non-nullable; validate `null` at entry points only; use `is null` / `is not null` — **never** `== null` / `!= null`.
- Suffix async methods with `Async`; **never** block with `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()`; no `async void` outside event handlers; flow a `CancellationToken` through long-running operations (see the `csharp-async` skill).
- Centralize error handling; return errors as Problem Details (RFC 9457). **Never log PII or secrets.**
- XML doc comments on all public APIs (see the `csharp-docs` skill).
- xUnit tests in a `[ProjectName].Tests` project, named `MethodName_Scenario_ExpectedBehavior`; Arrange-Act-Assert structure but **no** `// Arrange` / `// Act` / `// Assert` comments (see the `csharp-xunit` skill).
- When reviewing, make only **high-confidence** suggestions; comment on _why_ a non-obvious design decision was made.

## Skills

The skill roster (names + descriptions) is always in context; routing notes the roster lacks:

- `microsoft-agent-framework` is the source of truth for agent and workflow work here — read `references/dotnet.md` for the .NET surface. The framework moves fast, so ground specifics in live docs rather than memory.
- `microsoft-docs` is the research lane for learn.microsoft.com and beyond; it wraps the `microsoft-learn` and `context7` MCP servers.
- `prd` is preloaded by `prd-generator`; `technical-writing` (document-type templates) by `se-technical-writer`.

## Detailed standards — `.claude/rules/`

`csharp.md` auto-loads whenever you edit a `*.cs` file. When reviewing or planning without editing, `Read` it directly.

## MCP servers — see `@.mcp.json`

`.claude/settings.json` sets `enableAllProjectMcpServers: true`, so the servers configured in `@.mcp.json` are available:

- **`microsoft-learn`** — ground version-specific Agent Framework / .NET answers in official docs (`microsoft_docs_search` → `microsoft_code_sample_search` → `microsoft_docs_fetch`) instead of memory.
- **`context7`** — docs outside learn.microsoft.com; resolve the library ID first, then query.

> **Trust gate:** since Claude Code v2.1.196, a checked-in `.claude/settings.json` cannot approve its own repo's MCP servers while the folder is **untrusted** — the key is ignored and servers sit at "Pending approval" until the workspace trust dialog is accepted. To have these servers auto-approve even before trusting, add a name-based list to your **user-level** `~/.claude/settings.json`: `"enabledMcpjsonServers": ["microsoft-learn", "context7"]`. If a server shows **Rejected**, a stale per-project choice is cached — run `claude mcp reset-project-choices` in that repo.

## Delegation rules

Every subagent is pinned to extra-high reasoning effort; model, tools, and preloaded skills live in each agent's frontmatter. Reviewer loops are capped: apply Critical/High findings, re-review only the changed files, at most two rounds, then surface anything still open to the user.

- **When the user asks to write a PRD, spec a feature, define requirements, or break a feature into epics/user stories**, delegate to `prd-generator` — do not write PRDs inline. Its report always starts with a `PRD-STATUS:` line. If it starts `PRD-STATUS: NEEDS-INPUT`, show its questions to the user verbatim (do not answer them yourself) and re-invoke the agent with the answers. It only creates GitHub issues when re-invoked with a statement that the user explicitly approved issue creation for the PRD path. `docs/prd/` is owned by `prd-generator`; a PRD is a pre-implementation artifact — writing one gets no `se-technical-writer` delegation and no changelog entry. Implementation plans for a feature that has a PRD should reference its story IDs (`US-xxx`).
- **After implementing or modifying C# code**, delegate a quality review to `csharp-code-reviewer`. It reports findings; it does not edit files.
- **After a sample or feature is implemented and the reviewer verdict passes**, ALWAYS delegate to `se-technical-writer` to author or update Markdown docs under `docs/` (create the folder if it does not exist) **and** add the entry to the root `CHANGELOG.md` under `[Unreleased]`. Also delegate to it whenever implementation details need documenting on their own.

## Changelog & feature tracking

Root `CHANGELOG.md`, [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) format:

- One entry per PR under `## [Unreleased]`, in the matching subsection (`### Added`, `### Changed`, `### Fixed`, `### Removed`, `### Deprecated`, `### Security`) — concise, reader-facing phrasing, not a commit list.
- `se-technical-writer` owns it; routine cleanups with no behavior change still get a one-line entry, even when they need no docs.
- On release, `[Unreleased]` is renamed to the version and date, and a fresh `[Unreleased]` section is started.

## Common commands

```bash
dotnet build                                          # compile the solution
dotnet format                                         # apply .editorconfig formatting
dotnet run --project samples/01-get-started/HelloAgent

# supply the OpenAI key without committing it
dotnet user-secrets set "OpenAI:ApiKey" "sk-..." --project samples/01-get-started/HelloAgent
```
