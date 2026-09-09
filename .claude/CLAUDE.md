# CLAUDE.md

Project memory for **maf-agents** — agents built with the [Microsoft Agent Framework](https://learn.microsoft.com/agent-framework/) in C# / .NET. It loads automatically every session and governs how Claude Code works in this repository.

This file lives at `.claude/CLAUDE.md`, **not** the repo root. Detailed standards in `.claude/rules/` auto-apply by file path; skills and subagents live under `.claude/`; `settings.json` pins `model: opus`, `effortLevel: xhigh`, and ten official plugins.

## About this repository

The purpose is to build and explore agents with the Microsoft Agent Framework on .NET.

**Read this before describing the repository — to the user or to yourself.** The current branch is early. One solution exists, `weather-agent/Andes.Agents/`, and it is a scaffold: **no Agent Framework package is referenced anywhere in the tree**. There is no agent, no model client, no tool, no prompt. Do not infer otherwise from the repository name or from the skills that are installed.

An earlier shape of this repo — console samples under `samples/<NN-category>/` (`HelloAgent`, `ImageAgent`), two ADRs under `docs/adr/`, a shared `config/appsettings.sample.json`, central package management, and a root `maf-agents.slnx` — lives on `origin/main` and is **not present on this branch**. `git show origin/main:<path>` reads any of it and `git checkout origin/main -- <path>` restores it, but until someone does, none of it is current. `docs/` exists on disk as an empty, untracked directory; `README.md` and `CHANGELOG.md` are tracked but empty.

### `weather-agent/Andes.Agents/` — the only code

Five projects in `Andes.Agents.slnx`, each declaring `net10.0`, `Nullable` and `ImplicitUsings` **in its own csproj**, because no `Directory.Build.props` exists on this branch to hold shared settings.

- **`Andes.Agents.Api`** — `Microsoft.NET.Sdk.Web`. Its one package reference is `Microsoft.AspNetCore.OpenApi` with an **inline `Version="10.0.12"`**. `Program.cs` is the unmodified `dotnet new webapi` template: `AddControllers()`, `AddOpenApi()`, `MapOpenApi()` under Development, `UseHttpsRedirection()`, `UseAuthorization()`, `MapControllers()`. There is **no `Controllers/` directory**, so the app starts and maps no routes.
- **`Andes.Agents.Common`, `.Entity`, `.Repository`, `.Service`** — **empty class libraries**: a `.csproj` each and zero `.cs` files. No project references are wired between any of them.

The names imply the layering `Api → Service → Repository → Entity`, plus `Common`. Nothing expresses or enforces that yet. `.claude/rules/api-architecture.md` describes exactly this shape in placeholder form and is the right thing to read when filling the projects in — even though its `paths:` glob never matches here.

The solution lives at `weather-agent/Andes.Agents/Andes.Agents.slnx`. **There is no root-level solution file.**

## Build and tooling reality

Four facts that the tree does not reveal on inspection:

- **No `Directory.Build.props`, `Directory.Packages.props`, or `global.json`.** So there is no central package management — versions go inline in the csproj, as `Andes.Agents.Api` does — no shared target framework, no `TreatWarningsAsErrors`, and no SDK pin. Central package management is the convention on `origin/main`; it is not in force here, so do not add a version-less `PackageReference`.
- **`.editorconfig` is authored but unenforced at build time.** Its 118 lines encode `.claude/rules/csharp.md`: `csharp_style_namespace_declarations = file_scoped:error`, `IDE0055` (formatting), `IDE1006` (naming), `IDE0005` (unused usings) and `CA1507` (`nameof`) promoted to `warning`, plus naming rules for `_camelCase` private fields, `I`-prefixed interfaces and PascalCase members. Without `Directory.Build.props` there is no `EnforceCodeStyleInBuild`, so **`dotnet build` will not fail on any of it**. These surface in the IDE and through `dotnet format` only.
- **`.gitattributes` forces `* text=auto eol=lf`.** A Windows clone with `core.autocrlf=true` would otherwise fail `dotnet format --verify-no-changes` on every file.
- **No CI, no tests.** There is no `.github/` directory on any branch and no test project anywhere. `dotnet build` and `dotnet format --verify-no-changes` are local conventions; nothing gates a push.

`.gitignore` still carries `config/appsettings.json`, `config/appsettings.*.json` and `output/` entries left over from the sample layout. They match nothing on this branch.

## Secrets

Nothing sensitive is committed, and no config template exists on this branch to hold a key.

When a model client is wired up, read credentials from `dotnet user-secrets` (stored outside the repo tree) or from environment variables. The C# standard prefers `DefaultAzureCredential` + Azure Key Vault over secrets in code or config; an OpenAI API key has no managed-identity equivalent, so user-secrets is the accepted substitute in that one case. The rule's intent — **no secret is ever committed** — binds absolutely either way.

## Communication & comments (always)

**Responses**

- Lead with the answer or the change. No preamble, no filler, no restating the request.
- Do not re-summarize a plan or diff the user already saw; report only what changed or went wrong.
- Match length to substance — a one-line answer is a complete answer.

**Code comments**

Write for a senior engineer who knows the language and can read the code. A comment supplies what the code cannot — nothing else.

- **Default to no comment.** Add one only where a competent reader would still ask _why_. No per-type or per-member quota.
- **One or two lines.** Three needs a reason; past six the content belongs in `docs/` with a one-line pointer from the code.
- **Never reference PRD story ids, epics, or design frames.** Traceability belongs in the PR description and `docs/`; in source it goes stale and says nothing about the code in front of the reader. Test names describe behaviour, not story ids.
- **Never explain the language, framework, or API** — not what a primary constructor is, not how `IAsyncEnumerable` works, not what `nameof` returns.
- **Never narrate.** No "added X", "now uses Y". No restating the statement below it.
- **Do write**: why the non-obvious choice beat the obvious one, an invariant a caller must hold, a workaround with its link or issue, a spec or vendor constraint. State the cause once — not the argument, the alternatives weighed, or the history.
- **Compress rather than delete.** A real _why_ buried in a paragraph becomes one sentence; it does not disappear.

```csharp
// Bad — narrates, teaches the platform, cites a story
/// <summary>
/// US-118 asks the tool to check the image before the call. A primary constructor captures
/// the client, and IAsyncEnumerable lets the caller stream results as they arrive. Added
/// local validation here so we fail fast instead of round-tripping to the service.
/// </summary>

// Good — the one thing the code cannot say
/// <remarks>Checked locally: the service rejects masks over 4 MB with an opaque 400.</remarks>
```

## C# coding standards (always)

Full standards live in `.claude/rules/csharp.md` (auto-applies to `**/*.cs`), and `.editorconfig` encodes them for `dotnet format`. The always-on core:

- Target the latest C# version (currently **C# 14**); prefer pattern matching, switch expressions, and `nameof(...)`.
- **File-scoped namespaces** — `.editorconfig` sets this at `error` severity. The only existing `.cs` file uses top-level statements, so there is no legacy block-scoped code to work around.
- Prefer **primary constructors**; capture each injected dependency into a `private readonly` `_camelCase` field and use the field in method bodies.
- Prefer **collection expressions** (`[]`, `[1, 2, 3]`, `[.. items]`) over `new List<T>()`, `new T[] { }`, or `Array.Empty<T>()`.
- PascalCase for types, methods, and public members; `_camelCase` private fields; camelCase locals and parameters; `I`-prefixed interfaces.
- Declare variables non-nullable; validate `null` at entry points only; use `is null` / `is not null` — **never** `== null` / `!= null`.
- Suffix async methods with `Async`; **never** block with `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()`; no `async void` outside event handlers; flow a `CancellationToken` through long-running operations (see the `csharp-async` skill).
- **Never log PII or secrets.**
- **XML docs** — a one-sentence `<summary>` on public API surface; interfaces carry the docs and implementations use `/// <inheritdoc />`. `<param>`/`<returns>` only where they add what the signature does not. `<remarks>` is for a caveat a caller must know, two sentences at most — not rationale, not history. `<example>`/`<code>` only where correct usage is genuinely non-obvious. `internal` and test types are not API surface: comment them only where a _why_ exists. **This overrides the `csharp-docs` skill wherever the two disagree** — that skill describes .NET's framework-reference house style, which is not this repository's.
- When reviewing, make only **high-confidence** suggestions; comment on _why_ a non-obvious design decision was made.

**Tests.** None exist yet. When they do: xUnit **v3**, named `MethodName_Scenario_ExpectedBehavior`, Arrange-Act-Assert structure but **no** `// Arrange` / `// Act` / `// Assert` comments, plain xUnit `Assert` (no FluentAssertions — v8+ is commercially licensed), isolation with **NSubstitute** (see the `csharp-xunit` skill). That convention covers library code. Agent code that calls a live model is not unit-tested.

## Skills

The roster (names + descriptions) is always in context; these are the routing notes it lacks.

- **`microsoft-agent-framework`** is the primary skill for this repository. The framework is in public preview, so ground its advice in live docs rather than memory — route lookups through `microsoft-docs`, which spans Microsoft Learn and Context7.
- `csharp-async`, `csharp-docs`, `csharp-xunit` and `ef-core` are preloaded by the `csharp-code-reviewer` subagent; each is invokable on its own. `ef-core` has nothing to act on until a persistence layer exists.
- `prd` is preloaded by `prd-generator`; `technical-writing` (document-type templates) by `se-technical-writer`.
- The three `github-actions-*` skills are the lanes preloaded by `github-actions-reviewer`. **There is no `.github/` directory in this repository**, so all four are idle until someone adds workflows.

## Rules — `.claude/rules/`

Five files, auto-loaded when you edit a matching path; the globs live in each rule's frontmatter under a `paths:` list, and none carry a `description:`. Only one of the five is about this repository:

| Rule | `paths:` glob | Applies here? |
| --- | --- | --- |
| `csharp.md` | `**/*.cs` | **Yes** — the standards above. |
| `aspnet-rest-apis.md` | `**/*.cs` | Loads on every C# edit. Relevant once `Andes.Agents.Api` grows real endpoints. |
| `azure-functions-csharp.md` | `**/*.cs` | Loads on every C# edit, but there are no Azure Functions here. Ignore it. |
| `terraform.md` | `**/*.tf` | No `.tf` files exist; never matches. |
| `api-architecture.md` | `enterprise-gpt-api/**` | Never matches — but it describes the `Api → Service → Repository → Entity` shape `Andes.Agents` uses, so `Read` it directly when building those projects out. **Untracked.** |

When reviewing or planning without editing, `Read` the matching rule directly.

## MCP servers — see `@.mcp.json`

`.claude/settings.json` sets `enableAllProjectMcpServers: true`, so the servers in `@.mcp.json` are available. Two are defined:

- **`microsoft-learn`** (http) — ground version-specific .NET, Azure and Agent Framework answers in official docs (`microsoft_docs_search` → `microsoft_code_sample_search` → `microsoft_docs_fetch`) instead of memory.
- **`context7`** (npx) — docs outside learn.microsoft.com; resolve the library id first, then query.

`settings.json` also lists `terraform` under `enabledMcpjsonServers`, but `.mcp.json` does not define it, so that entry does nothing.

> **Trust gate:** a checked-in `.claude/settings.json` cannot approve its own repo's MCP servers while the folder is **untrusted** — the key is ignored and servers sit at "Pending approval" until the workspace trust dialog is accepted. To auto-approve regardless, add a name-based list to your **user-level** `~/.claude/settings.json`: `"enabledMcpjsonServers": ["microsoft-learn", "context7"]`. If a server shows **Rejected**, a stale per-project choice is cached — run `claude mcp reset-project-choices` in this repo.

## Delegation

Four subagents, all pinned to extra-high reasoning effort; `csharp-code-reviewer` and `github-actions-reviewer` run on `opus`, the other two on `sonnet`. Tools and preloaded skills live in each agent's frontmatter. Reviewer loops are capped: apply Critical/High findings, re-review only the changed files, at most two rounds, then surface anything still open to the user.

- **After implementing or modifying C# code**, delegate a quality review to `csharp-code-reviewer`. It reports findings; it does not edit files.
- **After a feature is implemented and the reviewer verdict passes**, delegate to `se-technical-writer` to author or update Markdown docs under `docs/` and add the entry to the root `CHANGELOG.md`. Both are empty right now, so the first such delegation establishes them — `CHANGELOG.md` in [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) format under a single `## [Unreleased]` heading, one reader-facing entry per PR in the matching subsection.
- **`github-actions-reviewer`** has nothing to review until a `.github/workflows/` tree exists. If workflows are added, route to it.
- **PRDs are not checked in.** `prd-generator` and the `prd` skill remain installed for a user who explicitly asks; nothing routes to them by default, and a PRD written that way is a scratch artifact.

## Common commands

```bash
# from the repo root
dotnet build weather-agent/Andes.Agents/Andes.Agents.slnx
dotnet run --project weather-agent/Andes.Agents/Andes.Agents.Api
dotnet format weather-agent/Andes.Agents/Andes.Agents.slnx
dotnet format weather-agent/Andes.Agents/Andes.Agents.slnx --verify-no-changes   # read-only check
```

Both the build and the format check pass on the current tree. There is no `dotnet test` — no test project exists. There is no root solution, so every command names its path explicitly.
