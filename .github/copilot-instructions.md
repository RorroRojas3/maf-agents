# Repository instructions for GitHub Copilot

Reusable standards for **C#/.NET back ends and Angular front ends**. This repository contains no application code — it ships GitHub Copilot configuration under `.github/`: these repository instructions, path-scoped instructions (`instructions/`), custom agents (`agents/`), and agent skills (`skills/`). Follow these instructions for all C# and Angular work.

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

Full standards live in `instructions/csharp.instructions.md` (auto-applies to `*.cs`). The always-on core:

- Target the latest C# version (currently **C# 14**); honor `.editorconfig`; prefer pattern matching, switch expressions, and `nameof(...)`.
- Prefer **primary constructors**; capture each injected dependency into a `private readonly` `_camelCase` field and use the field in method bodies.
- Prefer **collection expressions** (`[]`, `[1, 2, 3]`, `[.. items]`) over `new List<T>()`, `new T[] { }`, or `Array.Empty<T>()`.
- PascalCase for types, methods, and public members; `_camelCase` private fields; camelCase locals and parameters; `I`-prefixed interfaces.
- Declare variables non-nullable; validate `null` at entry points only; use `is null` / `is not null` — **never** `== null` / `!= null`.
- Suffix async methods with `Async`; **never** block with `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()`; no `async void` outside event handlers; flow a `CancellationToken` through long-running operations.
- Centralize error handling; return errors as Problem Details (RFC 9457). **Never log PII or secrets**; prefer `DefaultAzureCredential` + Azure Key Vault / Managed Identity over secrets in code or config.
- XML doc comments on all public APIs.
- xUnit tests in a `[ProjectName].Tests` project, named `MethodName_Scenario_ExpectedBehavior`; Arrange-Act-Assert structure but **no** `// Arrange` / `// Act` / `// Assert` comments.
- When reviewing, make only **high-confidence** suggestions; comment on _why_ a non-obvious design decision was made.

## Angular / NgRx standards (always)

- Standalone components, `ChangeDetectionStrategy.OnPush`, signals for state. Assume zoneless.
- Non-trivial state belongs in an **NgRx Signal Store** (`@ngrx/signals`), not a hand-rolled `BehaviorSubject` service. Keep `protectedState` on; write state only via `patchState` with standalone updaters.
- Use `rxMethod` (not `signalMethod`) whenever requests can overlap — `switchMap` prevents a stale response overwriting a fresh one. One store per entity type; `withEntities` for keyed collections.
- This is **not** classic NgRx: no actions, reducers, or effects unless the Events plugin is a deliberate choice.

## Skills — `.github/skills/`

Before working in an area, read that skill's `SKILL.md`, then only the reference files it points to. Available: `angular-developer`, `csharp-async`, `csharp-docs`, `csharp-xunit`, `ef-core`, `github-actions-efficiency`, `github-actions-hardening`, `github-actions-runtime-upgrade-conventions`, `microsoft-agent-framework`, `microsoft-docs`, `ngrx-signal-store`, `prd`, `technical-writing`. Routing notes: `ngrx-signal-store` is the source of truth for Angular state management; `technical-writing` is the SE Technical Writer's template pack.

## Path-scoped instructions — `.github/instructions/`

These apply automatically via their `applyTo` globs when working on matching files (`csharp`, `aspnet-rest-apis`, `azure-functions-csharp`, `blazor-wasm`, `csharp-mcp-server`, `terraform`); when reviewing or planning, read the matching file explicitly.

## Implementation-agent contract

After modifying code, invoke the matching reviewer subagent (`C# Code Reviewer` / `Angular Code Reviewer`) on the diff, apply its Critical and High findings yourself, and re-run it **once, on only the files changed since the first round** — at most two review rounds in total. If the verdict still is not **Approve** or **Approve with changes** after the second round, stop and report the outstanding findings to the user instead of iterating. If the change touched GitHub Actions workflows (`.github/workflows/*.yml` or composite actions), also invoke the `GitHub Actions Reviewer` on those files. Once the verdict passes, invoke the `SE Technical Writer` to create or update the feature's docs under `docs/` (creating the folder if absent) and add the `CHANGELOG.md` entry under `[Unreleased]`, passing it a short summary of what was implemented, the files touched, and design decisions worth recording. Exception: if your delegation prompt says documentation is handled by the orchestrator (Full-Stack Expert flow), skip the writer and report that documentation was deferred.

## Custom agents — `.github/agents/`

Intended flow: (optional) spec with **PRD Generator** → plan with **Planner Expert** → hand off to the recommended implementation agent (**C# Expert**, **C# MCP Server Expert**, **C#/.NET Janitor**, **Angular Expert**, or **Full-Stack Expert**) → that agent follows the implementation-agent contract above. Contracts the frontmatter descriptions don't carry:

- **Full-Stack Expert** orchestrates cross-stack features: fixes the API contract first, delegates to C# Expert and Angular Expert in parallel, verifies the integrated seam, and invokes the SE Technical Writer **once** for the whole feature — sub-experts skip their own documentation step.
- **C#/.NET Janitor** exception: routine cleanups with no behavior change skip the writer; the Janitor appends its own one-line `CHANGELOG.md` entry.
- **PRD Generator** writes PRDs under `docs/prd/` and creates GitHub issues only after explicit approval. A PRD is a pre-implementation artifact — it gets no changelog entry.

When the microsoft-learn or angular-cli MCP servers are available, use them to ground version-specific .NET/Azure and Angular answers instead of relying on memory. When the context7 MCP server is available, use it for docs that live outside learn.microsoft.com (VS Code, GitHub, Aspire) — see the `microsoft-docs` skill. Repo-level MCP configuration for VS Code lives in `.vscode/mcp.json`.

## Changelog & feature tracking

The root `CHANGELOG.md` follows the [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) format:

- One entry per PR under `## [Unreleased]`, in the matching subsection (`### Added`, `### Changed`, `### Fixed`, `### Removed`, `### Deprecated`, `### Security`) — concise, reader-facing phrasing, not a commit list.
- The **SE Technical Writer** owns it (Janitor exception above).
- On release, `[Unreleased]` is renamed to the version and date, and a fresh `[Unreleased]` section is started.

## Commands

```bash
dotnet build     # compile
dotnet test      # run xUnit tests
dotnet format    # apply .editorconfig formatting
```
