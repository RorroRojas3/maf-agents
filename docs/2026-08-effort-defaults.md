# August 2026 Reasoning-Effort Defaults

> **Update 2026-08-06**: the Copilot half of [section 3](#3-se-technical-writer-haiku--sonnet) is superseded — the Copilot writer moved back to Haiku 4.5 as a documented per-harness cost override (Copilot has no effort key, so the Haiku-ignores-effort concern doesn't apply there). The Claude side is unchanged: Sonnet + `xhigh`. A fifth subagent (`prd-generator`) now also pins `xhigh`. See [2026-08-repo-audit.md](2026-08-repo-audit.md).

**Date**: 2026-08-02
**Claude Code harness**: `.claude/settings.json`, `.claude/agents/csharp-code-reviewer.md`, `.claude/agents/angular-code-reviewer.md`, `.claude/agents/github-actions-reviewer.md`, `.claude/agents/se-technical-writer.md`, `.claude/CLAUDE.md`, `README.md`
**GitHub Copilot harness**: `.github/agents/se-technical-writer.agent.md` (model bump only — see [section 4](#4-github-copilot-no-per-agent-effort-key-yet))

This change makes **extra-high reasoning effort** (`xhigh`) the default across the Claude Code harness: session-wide via `settings.json`, and pinned per agent on all four subagents. It is grounded in the live Claude Code docs (`code.claude.com/docs` — `sub-agents.md`, `model-config.md`, `settings.md`, fetched 2026-08-02), not remembered behavior.

The configuration files remain the source of truth — this page explains why each change was made and how the two effort levels interact. If a value here ever disagrees with `settings.json` or an agent's frontmatter, trust the file.

## Why this change happened

Everything this repo delegates to a subagent is reasoning-heavy by design: the three reviewers exist to catch subtle async, state-management, and workflow-security problems that pattern matching misses, and the technical writer has to reconstruct *why* a change was made from the diff. Running that work at a middling default effort wastes the whole point of the delegation.

Claude Code exposes reasoning effort at two levels — a session-wide `effortLevel` setting and a per-agent `effort` frontmatter key — and before this change the repo set neither, so every session and every agent ran at whatever the harness default happened to be. The intent now is explicit: **agents should reason at the highest level their model supports, by default, without anyone having to remember to ask for it.**

Auditing this also surfaced a real bug in the previous setup: the SE Technical Writer ran on Haiku, and per the official model-config docs Haiku 4.5 does not support effort levels at all — models absent from the effort table silently ignore the setting. An effort default the writer could never honor is no default at all, so its model moved to Sonnet (see [section 3](#3-se-technical-writer-haiku--sonnet)).

## 1. Session-wide default: `effortLevel` in settings

**Where**: `.claude/settings.json`.

```json
"effortLevel": "xhigh"
```

Per the settings docs, `effortLevel` sets the default reasoning effort for the session and **persists across sessions** — it is checked into the repo, so every contributor's main session starts at `xhigh` without local setup. The repo's pinned `fable` model supports `xhigh`, so the setting takes effect immediately rather than being ignored.

This level covers the *main* session: planning, implementation, and everything not delegated to a subagent.

## 2. Per-agent pins: `effort: xhigh` frontmatter

**Where**: all four files in `.claude/agents/` — `csharp-code-reviewer.md` (sonnet), `angular-code-reviewer.md` (sonnet), `github-actions-reviewer.md` (opus), and `se-technical-writer.md` (sonnet).

```yaml
model: sonnet
effort: xhigh
```

Per the sub-agents docs, the frontmatter `effort` key accepts `low` / `medium` / `high` / `xhigh` / `max`, and an agent-level value **overrides the session-wide `effortLevel`** for that agent's run.

The two levels are deliberately redundant for the agents, and that redundancy is the design:

- The **settings default** makes `xhigh` the ambient level for the main session and anything that merely inherits it.
- The **per-agent pins** guarantee the subagents stay at `xhigh` even when the session level changes — a user dropping their session to `medium` for a quick edit should not silently degrade the next code review.

In short: the setting is the default, the frontmatter is the contract. `.claude/CLAUDE.md` (delegation rules) and `README.md` (structure comments and the subagent frontmatter conventions line) now document both.

## 3. SE Technical Writer: Haiku → Sonnet

**Where**: `.claude/agents/se-technical-writer.md` (`model: haiku` → `model: sonnet`) and, for cross-harness parity, `.github/agents/se-technical-writer.agent.md` (`Claude Haiku 4.5 (copilot)` → `Claude Sonnet 5 (copilot)`).

This is a consequence of the effort work, not an independent preference. The official model-config docs list which models support effort levels, and Haiku 4.5 is not in that table — models absent from it ignore the setting entirely. Pinning `effort: xhigh` on a Haiku agent would have been a no-op that *looked* configured. Sonnet 5 supports `xhigh`, so moving the writer to Sonnet is what makes its pin real.

The Copilot-side mirror keeps the two harnesses on the same model for the same agent, which is this repo's standing parity rule. It is the **only** Copilot-side change in this refresh.

## 4. GitHub Copilot: no per-agent effort key (yet)

**Deliberately not done**: no reasoning-effort configuration was added to the GitHub Copilot harness, because there is nothing to add today. GitHub Copilot has no per-agent reasoning-effort frontmatter key — effort there exists only as a global per-surface user setting (the VS Code model picker, or CLI configuration), which cannot be checked into a repo or scoped to one agent.

Per-agent effort is an open feature request upstream:

- CLI: [`github/copilot-cli` issue #2904](https://github.com/github/copilot-cli/issues/2904)
- VS Code: [`microsoft/vscode` issue #313546](https://github.com/microsoft/vscode/issues/313546)

When either ships a per-agent key, mirror the Claude Code pins into `.github/agents/*.agent.md` to restore full cross-harness parity. Until then, the Copilot agents' effort follows each user's own surface-level setting.

Also intentionally untouched: both skills trees (`.claude/skills/` and `.github/skills/`). They are mirrored byte-for-byte between harnesses, and `angular-developer` is hash-pinned in `skills-lock.json` — effort is runtime configuration and does not belong in skill content.

## Practical impact

- Every Claude Code session in this repo now starts at `xhigh` effort, and all four subagents run at `xhigh` regardless of the session level — no per-machine setup required.
- Expect deeper (and somewhat slower/costlier) reviewer and writer runs; that trade is the point. If a specific agent should run lighter, change its `effort` frontmatter — do not rely on lowering the session level, because the pins override it.
- The SE Technical Writer now runs on Sonnet in **both** harnesses. Any local notes or dashboards that still describe it as the "Haiku agent" are stale.
- GitHub Copilot users who want higher effort must set it per surface (VS Code model picker / CLI config) until the upstream per-agent key lands.
