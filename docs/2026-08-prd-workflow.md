# August 2026 PRD Workflow

**Date**: 2026-08-04
**Claude Code harness**: `.claude/skills/prd/` (`SKILL.md` + `references/`), `.claude/agents/prd-generator.md`, `.claude/CLAUDE.md`
**GitHub Copilot harness**: `.github/skills/prd/` (content-identical mirror), `.github/agents/prd-generator.agent.md`, `.github/agents/planner-expert.agent.md`, `.github/copilot-instructions.md`
**Both**: `README.md` (reframed as a dual-harness configuration)

This change adds a **PRD (Product Requirements Document) generation workflow** to both harnesses: a shared `prd` skill and a PRD-generator agent that turn a feature request into a spec with measurable success criteria and a breakdown into epics and user stories — with optional GitHub-issue creation after explicit approval. The skill and agent files remain the source of truth; this page explains why the workflow exists and how the pieces fit.

## Why this exists

Requirements decided in chat evaporate. The scope lives in one person's scrollback, the plan references "the search thing", and issues written after the fact never quite match what was agreed. By the time implementation starts, nobody can point at the sentence that defines done.

A PRD fixes that only if it is *traceable*. So the workflow's core design is a stable ID scheme — `FR-n` for functional requirements, `EP-n` for epics, `US-xxx` for user stories (first digit(s) = epic number) — that plans, commits, and GitHub issues can all reference. The Planner Expert plans against `US-xxx` IDs; issue titles keep the IDs; every functional requirement must map to at least one story. The document is the single thread from "why are we building this" to "which issue closes it".

What a run produces: `docs/prd/<feature-slug>.md` following a canonical nine-section schema — success criteria that each name *metric + target + measurement source* (never "fast" or "intuitive"), epics as independently shippable slices, and stories with Given/When/Then acceptance criteria, P0/P1/P2 priorities, S/M/L estimates (XL must be split before it enters the document), and dependencies. Milestones are derived from the epic dependency graph, not invented separately.

## 1. The shared skill: one schema, two harnesses

**Where**: `.claude/skills/prd/` and `.github/skills/prd/` — content-identical mirrors, verified at the git-blob level per this repo's skills-mirror convention.

The skill is deliberately harness-neutral and uses progressive disclosure. `SKILL.md` carries only the workflow (discover → analyze → draft), the seven discovery gaps that must be closed before drafting (problem and urgency, users, scope boundary, success metrics, constraints, auth model, existing code touchpoints), and the quality bar (no fabricated constraints, every story testable, full FR → story traceability). Everything heavier sits in `references/`, read only when the task calls for it:

| Reference | Contents |
| --- | --- |
| `references/prd-template.md` | The canonical nine-section schema, the `FR-n`/`EP-n`/`US-xxx` ID scheme, formatting rules, and the final checklist |
| `references/story-breakdown.md` | Epics as shippable slices (not architectural layers), vertical slicing with SPIDR, INVEST, acceptance-criteria rules, the P0/P1/P2 sanity rule (>60% P0 means prioritization failed), S/M/L estimates, dependency-graph-derived milestones, and a coverage checklist (authn/authz, error states, accessibility, telemetry, migration, rollback) |
| `references/github-issues.md` | `gh` CLI recipes for turning section 7 into issues — read only after explicit user approval |

The schema lives in **exactly one place** on purpose. Both agents are instructed to follow `references/prd-template.md` exactly and never restate or invent their own outline — so the two harnesses cannot drift apart on document structure, and a schema change is one file edit (plus its mirror) that both agents pick up automatically.

The skill also draws a hard boundary: a PRD says *what* to build and *why*. Implementation planning — file-level steps, architecture, sequencing — belongs to plan mode and the Planner Expert, which reference the PRD's story IDs rather than duplicating its content.

## 2. Claude Code: delegation and the `PRD-STATUS` contract

**Where**: `.claude/agents/prd-generator.md` (Sonnet, `effort: xhigh`, preloads the `prd` skill), registered by a delegation rule in `.claude/CLAUDE.md`.

When the user asks to write a PRD, spec a feature, or break one into stories, the main session delegates to the `prd-generator` subagent rather than writing the PRD inline. The subagent analyzes the codebase first (architecture, similar features, auth mechanism, telemetry conventions — the PRD must name real integration points, and version-specific .NET/Azure claims are verified against the `microsoft-learn` MCP).

The interesting design problem: **subagents cannot converse mid-run**, but good discovery sometimes requires asking the user something. The solution is a report contract — the first line of the agent's report is always exactly one of:

```
PRD-STATUS: NEEDS-INPUT
PRD-STATUS: DRAFTED
PRD-STATUS: ISSUES-CREATED
```

- **`NEEDS-INPUT`** — returned *only* for a blocking gap, one where a wrong guess would invalidate most of the document. No files are written. The report carries at most 5 numbered questions, each stating why it blocks drafting and ending with *"If unanswered, I will assume: {default}"*. The main session relays the questions to the user **verbatim** (it must not answer them itself) and re-invokes the agent with the answers. At most one round-trip: on re-invocation the agent must draft, using its proposed defaults for anything still unanswered. Minor or codebase-inferable gaps never trigger this — the agent proceeds and records each guess in the PRD's *Assumptions & open questions* section.
- **`DRAFTED`** — the PRD was written to `docs/prd/<feature-slug>.md`; the report gives the path, epic/story counts by priority, the assumptions made, and the next steps (including how to request issue creation).
- **`ISSUES-CREATED`** — issues mode ran; the report is the PRD-ID → issue-URL table.

Two ownership rules round this out: `docs/prd/` is owned by `prd-generator`, and a PRD is a **pre-implementation artifact** — writing one triggers no `se-technical-writer` delegation and no `CHANGELOG.md` entry. (Which is why this page documents the *workflow* — a repo feature — while the PRDs it produces get no docs page of their own.)

## 3. GitHub Copilot: interactive chat mode and the Planner handoff

**Where**: `.github/agents/prd-generator.agent.md` ("PRD Generator", `target: vscode`, `Claude Sonnet 5 (copilot)`), registered in `.github/copilot-instructions.md`.

The Copilot side needs none of the report-protocol machinery because a chat mode *can* converse. The PRD Generator asks 3–5 clarifying questions via `vscode/askQuestions` before drafting — conversationally, each with a proposed default — then analyzes the codebase, confirms the output location, drafts per the same skill files (`.github/skills/prd/`), and iterates on the draft section by section until the user approves. `disable-model-invocation: true` keeps it a deliberate choice from the agents dropdown rather than something Copilot auto-selects.

The mode ends by pointing at its **"Plan: Planner Expert" handoff**, pre-filled to plan the P0 stories of the first unblocked epic and to reference story IDs (`US-xxx`) in plan steps. The connection is wired from both ends: the Planner Expert's own Discovery phase now checks `docs/prd/` and, when a PRD exists for the feature, plans against its story IDs — so the trace from spec to plan survives even when the user starts at the Planner instead of the handoff.

The intended flow in `copilot-instructions.md` is now: *(optional) spec with **PRD Generator** → plan with **Planner Expert** → implementation expert → code reviewer → SE Technical Writer.*

## 4. The issue-creation gate

Creating GitHub issues is the one step with side effects outside the repo, so both harnesses gate it identically: **presenting the PRD is never implicit approval.**

- On Claude Code, the subagent enters issues mode only when the invocation explicitly states the user approved issue creation and names the PRD path — otherwise it is always in draft mode.
- On Copilot, the chat mode asks about issues only after the PRD itself is approved, and acts only on explicit confirmation.

Both then follow `references/github-issues.md`: preflight (`gh auth status`, `gh repo view`, label setup with `--force` for idempotency), an **idempotency check** that searches existing issues by PRD ID so re-runs never duplicate, epics created first as parent issues, stories referencing their epic (`Part of #n`) and dependencies (`Blocked by #n`) with acceptance criteria as task-list checkboxes, and finally each epic's body edited to carry a task list of its story issues. The run ends with a PRD-ID → issue-URL table, listing anything skipped or failed. The [`gh` CLI](https://cli.github.com) is the only extra requirement.

## Alongside: the README reframe

This feature also landed the `README.md` reframe as a **dual-harness** configuration: one set of standards shipped for two harnesses, with a Claude Code ↔ GitHub Copilot concept-mapping table (memory files, path-scoped rules, skills, agents, commands/prompts, MCP config), shields.io badges (Claude Code, GitHub Copilot, .NET/C# 14, Angular/NgRx, MIT, PRs welcome), both trees in the structure diagram, and Getting started split into per-harness sections. The PRD workflow made the parallel structure impossible to present as an afterthought — the same feature now genuinely exists twice, shaped by each harness's strengths — so the README now says so up front.
