# August 2026 Cross-Harness Audit & Copilot Cost Tuning

**Date**: 2026-08-06
**Shared tooling**: `scripts/repo-audit.mjs` (new — repo-maintenance tooling, part of neither drop-in tree)
**Claude Code harness**: `.claude/commands/repo-audit.md` (new), `.claude/rules/aspnet-rest-apis.md`, `.claude/rules/blazor-wasm.md`, `.claude/agents/csharp-code-reviewer.md`, `.claude/agents/angular-code-reviewer.md`, `.claude/agents/github-actions-reviewer.md`, `.claude/CLAUDE.md`, `README.md`
**GitHub Copilot harness**: `.github/prompts/repo-audit.prompt.md` (new), `.github/instructions/aspnet-rest-apis.instructions.md`, `.github/copilot-instructions.md`, `.github/agents/se-technical-writer.agent.md`, plus the review-loop cap edits in `csharp-expert`, `angular-expert`, `csharp-mcp-expert`, `full-stack-expert`, and all three reviewer agents

The configuration files remain the source of truth — this page records why each change was made. If a value here ever disagrees with a frontmatter key or the script's `CONFIG`, trust the file.

## Why this change happened

GitHub Copilot moved to usage-based billing (AI Credits) on 2026-06-01: every agent request is billed per token at the selected model's price. Cost is therefore *model price × context size × number of agent hops*, and this repo controls all three — the models its agents pin, the instructions its globs inject, and the review/documentation loop its contract mandates. Separately, the two harness trees had already drifted in three places (a model-parity gap, a one-comma body drift in the Blazor rules, and an unbounded review loop), and the only guard was a manual `git hash-object` convention nobody runs. This change adds a real audit and applies the cost fixes that don't trade away review quality.

## 1. `/repo-audit` — the cross-harness audit twin

**Where**: `scripts/repo-audit.mjs`, `.claude/commands/repo-audit.md`, `.github/prompts/repo-audit.prompt.md`.

A zero-dependency Node ≥18 script does all deterministic work; the command/prompt twin only acts on what it flags — the same division of labor as `/ngrx-signals-sync`. Checks: `skills-mirror` (content hashes of the two `skills/` trees, EOL-normalized — line-ending-only differences are deliberately tolerated), `rules-parity` (glob sets and bodies of each rule/instruction pair, after mapping the intentional cross-reference spellings), `agent-twins` (existence + model parity per the `CONFIG.modelParity` table and its documented overrides), `registry` (skills list, command/prompt twins, agent references, README mentions), `cost-hygiene` (always-on word budgets, broad globs, glob overlap, missing model/effort pins — warnings only), and `changelog` (`## [Unreleased]` present).

Exit codes are the contract: `0` clean (the command prints one line and stops — the routine path costs nearly nothing), `10` findings (the model reads only `affectedPaths`), `1` script failure (never read as either). `--strict` promotes warnings to failures; `--fix` on the *command* applies only mechanical repairs and always leaves the diff in the working tree — it never commits.

On its first run the script found and led to fixing: the Blazor body drift ("Durable cross-device" vs "Durable, cross-device") and the model-parity gap below.

## 2. Model-parity convention: tier parity + documented overrides

**Where**: `README.md` (conventions), `scripts/repo-audit.mjs` (`CONFIG.modelParityOverrides`).

The old convention — Copilot agent models "kept in parity with the Claude twin" — was already broken in practice (the GitHub Actions Reviewer ran Opus on Claude and Sonnet 5 on Copilot) and is the wrong rule when one harness bills per token and the other doesn't. The convention is now **tier parity by default, with per-harness cost overrides allowed when they are recorded in `modelParityOverrides` and in a docs record**. Two overrides exist:

- **`github-actions-reviewer`** — Opus on Claude Code (deliberate: the deepest review tier for workflow security), Sonnet 5 on Copilot (Opus is $5/$25 per MTok on AI Credits; not justified there).
- **`se-technical-writer`** — Sonnet on Claude Code, **Haiku 4.5 on Copilot** (section 3).

An override records the Claude model it was written against, so if either side's pin later changes, the audit fails loudly instead of the override silently excusing it.

## 3. Copilot SE Technical Writer: Sonnet 5 → Haiku 4.5

**Where**: `.github/agents/se-technical-writer.agent.md` (`Claude Sonnet 5 (copilot)` → `Claude Haiku 4.5 (copilot)`).

The writer is the last, lowest-risk hop of every feature loop: it turns an existing diff and summary into docs and a changelog entry using the `technical-writing` skill's templates — format-driven work well within Haiku's ability. On AI Credits, Haiku 4.5 ($1/$5 per MTok) is ~2× cheaper than Sonnet 5 at its promotional price ($2/$10, ends 2026-08-31) and 3× cheaper after.

This supersedes the Copilot half of [2026-08-effort-defaults.md §3](2026-08-effort-defaults.md), which moved the writer to Sonnet *on both harnesses* for effort reasons. That reasoning was Claude-specific: Haiku 4.5 ignores the `effort` frontmatter key, so a Haiku pin would have made the Claude-side `effort: xhigh` a silent no-op. Copilot has no effort key at all, so the concern does not apply there — the Claude side keeps Sonnet + `xhigh`, unchanged. (That doc also says "all four subagents" pin `xhigh`; five do now — `prd-generator` was added after it was written.)

## 4. Review-loop caps

**Where**: `.github/copilot-instructions.md` (implementation-agent contract), the four implementation/orchestration agents that restate it, all six reviewer agents (both harnesses), `.claude/CLAUDE.md` (delegation rules).

The contract previously said "re-run it until the verdict is Approve" — an unbounded loop where each round re-reads the diff, the rules, and the skills at billed token prices. Now: **at most two review rounds**; the re-review covers **only the files changed since the first round**; if the verdict still doesn't pass, the agent stops and reports the outstanding findings to the user instead of iterating. The reviewers themselves are told that prior verdicts on untouched files carry forward, so a re-review round is a fraction of the first. Review *depth* is unchanged — the cap bounds repetition, not rigor.

## 5. `aspnet-rest-apis` no longer fires on every `.json` file

**Where**: `.claude/rules/aspnet-rest-apis.md` (`paths`), `.github/instructions/aspnet-rest-apis.instructions.md` (`applyTo`) — `**/*.json` removed from both.

The rule body contains no JSON guidance at all; the glob injected ~360 words into every request that touched any `.json` file (settings, config, lockfiles). On Copilot that is billed input on every such request. The `**/*.cs` glob is unchanged. The audit's `cost-hygiene` check still reports the remaining deliberate overlap (four rules on `**/*.cs`, two on `**/*.csproj`) as a standing warning, so the trade-off stays visible without failing the audit.

## Practical impact

- Run `/repo-audit` (Claude Code) or the `repo-audit` prompt (Copilot) after any change to standards, skills, or agents — or `node scripts/repo-audit.mjs` directly in CI (fail on exit 10). Clean runs cost one script invocation and one line of output.
- A feature loop on Copilot now costs measurably less: the writer hop runs at Haiku prices, review rounds are bounded at two with scoped re-reads, and `.json` edits no longer drag in an unrelated instruction file.
- `git hash-object` mirror checks are obsolete — the audit's `skills-mirror` check replaces them and, unlike hashing, ignores Windows CRLF noise.
