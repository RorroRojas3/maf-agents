# awesome-claude-copilot

[![Claude Code](https://img.shields.io/badge/Claude_Code-config-d97757?logo=claude&logoColor=white)](https://code.claude.com)
[![GitHub Copilot](https://img.shields.io/badge/GitHub_Copilot-config-8957e5?logo=githubcopilot&logoColor=white)](https://github.com/features/copilot)
[![.NET](https://img.shields.io/badge/.NET-C%23_14-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com)
[![Angular](https://img.shields.io/badge/Angular-NgRx_Signals-DD0031?logo=angular&logoColor=white)](https://angular.dev)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg)](https://github.com/RorroRojas3/awesome-claude-copilot/pulls)

A curated, reusable **AI-assistant configuration** for **C#/.NET back ends and Angular front ends** — one set of standards, shipped for **two harnesses**: [Claude Code](https://code.claude.com) and [GitHub Copilot](https://github.com/features/copilot). Drop the tree for your harness (or both) into your repository and stop re-explaining your team's standards in every prompt.

There is no application source code here. This repo is purely portable assistant configuration: project memory, path-scoped rules, skills, subagents/custom agents, reusable prompts, and MCP servers.

The two trees are parallel and **self-contained** — nothing in `.github/` references `.claude/`:

| Concept | Claude Code (`.claude/`) | GitHub Copilot (`.github/`) |
| --- | --- | --- |
| Always-on standards | `CLAUDE.md` | `copilot-instructions.md` |
| Path-scoped standards | `rules/*.md` | `instructions/*.instructions.md` |
| Skills | `skills/` | `skills/` — content-identical mirror of `.claude/skills/` (verified at the git-blob level; working-tree line endings may differ on Windows) |
| Agents | `agents/*.md` (subagents) | `agents/*.agent.md` (custom agents) |
| Reusable commands | `commands/*.md` (slash commands) | `prompts/*.prompt.md` |
| MCP servers | `.mcp.json` | `.vscode/mcp.json` |

---

## Repository structure

```
.
├── .claude/                      # ── Claude Code harness ──
│   ├── CLAUDE.md                 # Always-loaded project memory: C# + Angular standards, delegation rules
│   ├── rules/                    # Path-scoped rules (auto-apply when a matching file is edited)
│   │   ├── csharp.md                       → **/*.cs
│   │   ├── aspnet-rest-apis.md             → **/*.cs
│   │   ├── azure-functions-csharp.md       → **/*.cs, **/host.json, **/local.settings.json, **/*.csproj
│   │   ├── blazor-wasm.md                  → **/*.razor, **/*.razor.cs, **/*.razor.css
│   │   ├── csharp-mcp-server.md            → **/*.cs, **/*.csproj
│   │   └── terraform.md                    → **/*.tf
│   ├── skills/                   # Invokable skills (Skill tool)
│   │   ├── angular-developer/              # official Angular team skill (pinned via skills-lock.json)
│   │   ├── csharp-async/SKILL.md
│   │   ├── csharp-docs/SKILL.md
│   │   ├── csharp-xunit/SKILL.md
│   │   ├── ef-core/SKILL.md
│   │   ├── github-actions-efficiency/      # CI-minutes and cost audits
│   │   ├── github-actions-hardening/       # workflow security review
│   │   ├── github-actions-runtime-upgrade-conventions/SKILL.md
│   │   ├── microsoft-agent-framework/      # + references/dotnet.md
│   │   ├── microsoft-docs/SKILL.md         # Learn MCP first; Context7 for the rest
│   │   ├── ngrx-signal-store/    # Progressive-disclosure, self-updating (see below)
│   │   │   ├── SKILL.md
│   │   │   ├── sources.json                # pinned upstream doc shas + @ngrx/signals version
│   │   │   ├── scripts/check-updates.mjs   # drift check against the live NgRx docs
│   │   │   └── references/                 # read on demand, not loaded up front
│   │   ├── prd/                  # PRDs + feature → epics/stories breakdown (see below)
│   │   │   ├── SKILL.md
│   │   │   └── references/                 # prd-template, story-breakdown, github-issues
│   │   └── technical-writing/              # doc-type templates + writing process, read on demand by se-technical-writer
│   ├── agents/                   # Subagents
│   │   ├── csharp-code-reviewer.md         # Sonnet (xhigh), read-only C#/.NET review
│   │   ├── angular-code-reviewer.md        # Sonnet (xhigh), read-only Angular review
│   │   ├── github-actions-reviewer.md      # Opus (xhigh), read-only workflow review
│   │   ├── prd-generator.md                # Sonnet (xhigh), writes PRDs under docs/prd/
│   │   └── se-technical-writer.md          # Sonnet (xhigh), writes docs under docs/
│   ├── commands/                 # Slash commands
│   │   ├── ngrx-signals-sync.md            # refresh the NgRx skill from upstream docs
│   │   └── repo-audit.md                   # audit cross-harness parity + cost hygiene
│   ├── settings.json             # Model, effort (xhigh) + MCP defaults
│   └── skills-lock.json          # pin for the installed angular-developer skill
│
├── .github/                      # ── GitHub Copilot harness (self-contained) ──
│   ├── copilot-instructions.md   # Repository instructions: standards + agent/skill registry
│   ├── instructions/             # Path-scoped instructions (applyTo globs) — twins of .claude/rules/
│   ├── agents/                   # Custom agents (planner, experts, reviewers, PRD generator, writer)
│   ├── prompts/                  # Reusable prompts (ngrx-signals-sync, repo-audit)
│   └── skills/                   # Content-identical mirror of .claude/skills/
│
├── .vscode/mcp.json              # MCP servers for GitHub Copilot in VS Code
├── .mcp.json                     # MCP servers for Claude Code
├── scripts/repo-audit.mjs        # repo-maintenance tooling — not part of either drop-in tree
└── LICENSE                       # MIT
```

---

## What's covered

**C#/.NET** (latest C# / C# 14): file-scoped namespaces, pattern matching, `nameof`; PascalCase/camelCase and `I`-prefixed interfaces; nullable reference types with `is null` / `is not null`; async (`Async` suffix, no `.Result`/`.Wait()`/`async void`, `CancellationToken`, `ConfigureAwait(false)`); validation (FluentValidation/DataAnnotations) and Problem Details (RFC 9457); `ILogger<T>` structured logging, never logging PII/secrets, `DefaultAzureCredential` + Key Vault; XML doc comments; xUnit conventions.

Framework specifics live in `.claude/rules/` (Claude Code) and `.github/instructions/` (Copilot), which auto-load when you edit a matching file:

| Topic | Claude Code rule | Copilot instructions |
| --- | --- | --- |
| General C# | `csharp.md` | `csharp.instructions.md` |
| ASP.NET Core REST APIs | `aspnet-rest-apis.md` | `aspnet-rest-apis.instructions.md` |
| Azure Functions (isolated worker) | `azure-functions-csharp.md` | `azure-functions-csharp.instructions.md` |
| Blazor WebAssembly (standalone) | `blazor-wasm.md` | `blazor-wasm.instructions.md` |
| MCP servers in C# | `csharp-mcp-server.md` | `csharp-mcp-server.instructions.md` |
| Terraform | `terraform.md` | `terraform.instructions.md` |

**Angular**: general implementation guidance via the official `angular-developer` skill; NgRx Signal Store state management — see below.

**GitHub Actions**: workflow security hardening, CI-efficiency audits, and runtime/version upgrades — three skills preloaded by the read-only `github-actions-reviewer` (Opus), each also invokable on its own.

**Feature specs & backlog breakdown**: the `prd` skill plus a PRD-generator agent turn a feature request into a Product Requirements Document — measurable success criteria and a breakdown into epics and user stories with acceptance criteria, priorities (P0/P1/P2), t-shirt estimates, and dependencies — written to `docs/prd/`, with optional GitHub-issue creation (`gh`) after you approve. On Claude Code the main session delegates to the `prd-generator` subagent; on Copilot the **PRD Generator** chat mode interviews you directly and hands off to the **Planner Expert**, which plans against the PRD's story IDs.

**Skills** (both harnesses): `angular-developer`, `csharp-async`, `csharp-docs`, `csharp-xunit`, `ef-core`, `github-actions-efficiency`, `github-actions-hardening`, `github-actions-runtime-upgrade-conventions`, `microsoft-agent-framework`, `microsoft-docs`, `ngrx-signal-store`, `prd`, `technical-writing`.

---

## Angular / NgRx Signal Store

`ngrx-signal-store` is the most involved skill here, and the only self-updating one.

It uses **progressive disclosure**. `SKILL.md` is short and loads whenever the skill triggers: the mental model, the production defaults (keep `protectedState` on, inject via default parameters, standalone state updaters), the decision rules that are easiest to get wrong (`signalState` vs `signalStore`; `rxMethod` vs `signalMethod` vs a plain method; when a custom feature or the Events plugin is actually warranted), and the traps — including that `withDevtools` is *not* part of core NgRx, a common hallucination.

Everything else sits in `references/` and is read only when the task calls for it:

| Reference | Read when |
| --- | --- |
| `store-composition.md` | Authoring or reshaping a store's structure |
| `async-and-rxjs.md` | The store talks to HTTP or any async source |
| `entity-management.md` | State holds a keyed collection |
| `custom-features.md` | Logic repeats across stores |
| `testing.md` | Writing or fixing store tests |
| `events-plugin.md` | Event-based state, or several stores reacting to one event |
| `recipes.md` | Starting a new store from a known-good shape |
| `api-reference.md` | Checking a signature, import path, or entry point |

### Keeping it current

NgRx guidance goes stale, and a skill that confidently teaches last year's API is worse than no skill. So the skill is **pinned** to a snapshot of the upstream docs in `sources.json` — a blob sha per doc page, the `@ngrx/signals` version it was written against, and a `mapsTo` list saying which reference file each upstream page feeds.

```bash
# Is the skill still current? Exit 0 = yes, 10 = drifted, 1 = check failed.
node .claude/skills/ngrx-signal-store/scripts/check-updates.mjs
```

The check costs a handful of unauthenticated HTTP requests (set `GITHUB_TOKEN` to raise the 60/hour limit; a weekly cadence never approaches it). Run the whole refresh through the slash command:

```
/ngrx-signals-sync              # check, and update the skill if upstream moved
/ngrx-signals-sync --check-only # report drift without editing anything
```

When nothing has changed it prints one line and stops. When something has, it fetches only the changed pages, propagates the substantive differences into the reference files named by `mapsTo`, re-pins the shas, and **leaves the edits in the working tree for you to review** — it never commits. This repo *is* the guidance, and there are no tests that would catch a bad semantic diff, so a human approves it.

To run it on a schedule, drive the command from a loop:

```
/loop 7d /ngrx-signals-sync
```

A `/loop` only fires while a Claude Code session is open, so treat it as a convenience rather than a guarantee. Because all the logic lives in the command and the script, the same refresh can be driven by `/schedule` as a real cron routine, or by CI (fail the job on exit code 10), without changing the skill. On the Copilot side, the same refresh is available as the `ngrx-signals-sync` prompt.

> The docs are fetched from the markdown behind `ngrx.io` (`ngrx/platform`, `projects/www/src/app/pages/guide/signals/`). `ngrx.io` itself is a JavaScript SPA and cannot be scraped — fetching it returns an empty nav shell, which is why the pipeline points at the source repo.

---

## Keeping the two trees in sync

Parity between `.claude/` and `.github/` is enforced by an audit, not by memory:

```
/repo-audit                    # Claude Code — report drift; --fix applies mechanical repairs
node scripts/repo-audit.mjs    # the same check directly (CI: fail on exit 10)
```

It verifies the mirrored `skills/` trees (tolerating Windows CRLF noise), every rule/instruction pair, agent twins and their model parity, the registries, and a few cost-hygiene lints. Exit `0` is clean, `10` is findings, `1` is a failed run — the same contract as the NgRx sync. On the Copilot side the same flow is the `repo-audit` prompt. Like everything else here, it never commits: findings become working-tree edits for a human to review.

---

## MCP servers

Configured in `.mcp.json` for Claude Code (`.claude/settings.json` sets `enableAllProjectMcpServers: true`) and in [`.vscode/mcp.json`](.vscode/mcp.json) for GitHub Copilot in VS Code — the same four servers in both:

| Server | Transport | Use |
| --- | --- | --- |
| `microsoft-learn` | HTTP (`https://learn.microsoft.com/api/mcp`) | Ground .NET/Azure answers in official Microsoft Learn docs |
| `angular-cli` | stdio (`npx @angular/cli mcp`) | Ground Angular answers in the installed Angular version |
| `terraform` | stdio (Docker: `hashicorp/terraform-mcp-server`) | Infrastructure-as-code |
| `context7` | stdio (`npx @upstash/context7-mcp`) | Docs outside learn.microsoft.com (VS Code, GitHub, Aspire) — used by the `microsoft-docs` skill |

---

## Getting started

### Claude Code

1. Copy [`.claude/`](.claude/) and [`.mcp.json`](.mcp.json) into the root of your repository.
2. Start Claude Code there. It loads `.claude/CLAUDE.md` every session, and the matching `.claude/rules/*.md` whenever you edit a relevant file.
3. The skills, the subagents (`csharp-code-reviewer`, `angular-code-reviewer`, `github-actions-reviewer`, `prd-generator`, `se-technical-writer`), and the `/ngrx-signals-sync` command become available. Delegation is described in `CLAUDE.md`: asking to spec a feature or break it into stories routes to `prd-generator` (writes PRDs under `docs/prd/`); after changing C#, `csharp-code-reviewer` reviews it (read-only); after changing Angular, `angular-code-reviewer`; after changing GitHub Actions workflows, `github-actions-reviewer`; implemented features go to `se-technical-writer`, which writes Markdown under `docs/` and maintains `CHANGELOG.md`.

### GitHub Copilot

1. Copy [`.github/`](.github/) and [`.vscode/mcp.json`](.vscode/mcp.json) into the root of your repository.
2. Copilot loads `.github/copilot-instructions.md` automatically; the path-scoped `instructions/*.instructions.md` apply via their `applyTo` globs.
3. The custom agents appear in the VS Code agents dropdown. Intended flow: (optional) spec with **PRD Generator** → plan with **Planner Expert** → hand off to the recommended implementation expert (C#, Angular, MCP Server, Full-Stack, Janitor) → its reviewer subagent checks the work → **SE Technical Writer** documents it and updates `CHANGELOG.md`.

Requirements: Node 18+ for the NgRx sync and repo-audit scripts (both use only Node built-ins and have no dependencies); the [`gh` CLI](https://cli.github.com) if you want PRD stories turned into GitHub issues.

---

## Conventions for contributors

- **Both harnesses, always:** a change to standards, skills, or agents lands in `.claude/` **and** its `.github/` twin in the same PR. Sync points: `CLAUDE.md` ↔ `copilot-instructions.md` registries, the two `skills/` trees (keep the mirrored files content-identical — verify with `/repo-audit`, or `node scripts/repo-audit.mjs` directly), and this README's structure diagram and lists.
- **Rules / instructions:** a `.claude/rules/*.md` file with a `paths:` block list applies only to matching files; without `paths:` it loads at launch for every session. The Copilot twin is an `applyTo:`-scoped `.github/instructions/*.instructions.md`.
- **Subagents (Claude):** `.claude/agents/*.md` frontmatter uses `name`, `description`, a `model` (`opus`/`sonnet`/`haiku`/`inherit`), an `effort` level (`low`/`medium`/`high`/`xhigh`/`max` — this repo pins every agent to `xhigh`, matching the `"effortLevel": "xhigh"` default in `settings.json`), and either a comma-separated `tools` list or a `skills:` list.
- **Custom agents (Copilot):** `.github/agents/*.agent.md` frontmatter uses a display-case `name`, `description`, `model` (kept in **tier parity** with the Claude twin by default; a per-harness cost override is allowed when it is recorded in `modelParityOverrides` in `scripts/repo-audit.mjs` **and** in a docs record — see `docs/2026-08-repo-audit.md`), lowercase `tools` ids, and optional `argument-hint`, `handoffs`, and `agents`. There is no effort key — Copilot has no equivalent yet.
- **Skills:** one folder per skill containing `SKILL.md` with `name` and `description` frontmatter. Keep `SKILL.md` under ~500 lines; anything longer belongs in `references/`, pointed to from a table that says *when* to read each file.
- **Never hand-edit the shas in `sources.json`.** They are machine-maintained — run `node .claude/skills/ngrx-signal-store/scripts/check-updates.mjs --pin`.
