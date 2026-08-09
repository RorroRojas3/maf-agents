#!/usr/bin/env node
// Cross-harness audit for this repo's two AI-assistant trees.
//
// Verifies that `.claude/` (Claude Code) and `.github/` (GitHub Copilot) still ship the
// same standards: mirrored skills, rule/instruction twins, agent twins with the agreed
// model mapping, registry listings, and a few cost-hygiene lints.
//
//   node scripts/repo-audit.mjs                 human-readable report
//   node scripts/repo-audit.mjs --json          machine-readable report
//   node scripts/repo-audit.mjs --strict        warnings also fail (exit 10)
//   node scripts/repo-audit.mjs --check=a,b     run a subset of checks
//
// Exit codes are the contract that /repo-audit branches on:
//   0  clean (warnings allowed)     10  findings     1  error (the script itself failed)
// Keeping 1 distinct from 10 is what stops a crashed run from being read as "drift".
//
// Line endings are deliberately tolerated: git's autocrlf means working trees on Windows
// legitimately hold CRLF where the twin holds LF (today: 7 files in ngrx-signal-store).
// Every comparison therefore normalizes EOL first; an EOL-only difference is not a finding.
// The script never writes files — detection is deterministic here, judgment is the model's.

import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { fileURLToPath } from 'node:url';
import { dirname, join, relative } from 'node:path';

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..');

// Exit 1 on any script failure so a crashed run is never read as "clean" or "drift".
process.on('uncaughtException', (err) => {
  console.error(`Audit failed: ${err.message}`);
  process.exit(1);
});

const CONFIG = {
  // Word budgets for the always-loaded files (~15% headroom over their current size).
  budgets: { '.claude/CLAUDE.md': 1400, '.github/copilot-instructions.md': 1100 },
  // Globs broad enough to tax context (Claude) and billed input tokens (Copilot) on
  // files the rule has nothing to say about.
  broadGlobs: ['**', '**/*', '**/*.json'],
  // Agents that exist only on the Copilot side by design — Claude Code's main session
  // plays these roles, so no `.claude/agents/` twin is expected.
  copilotOnlyAgents: [
    'planner-expert', 'csharp-expert', 'angular-expert', 'full-stack-expert',
    'csharp-dotnet-janitor', 'csharp-mcp-expert',
  ],
  // Default model mapping between a Claude agent's `model:` and its Copilot twin's.
  modelParity: {
    sonnet: 'Claude Sonnet 5 (copilot)',
    haiku: 'Claude Haiku 4.5 (copilot)',
    opus: 'Claude Opus 4.6 (copilot)',
    fable: 'Claude Fable 5 (copilot)',
  },
  // Documented per-harness cost overrides (README "Conventions for contributors").
  // An override must state the Claude model it was recorded against so it goes stale
  // loudly instead of silently excusing a future model change.
  modelParityOverrides: {
    'github-actions-reviewer': {
      claude: 'opus',
      copilot: 'Claude Sonnet 5 (copilot)',
      reason: 'deliberate: deepest review tier on Claude; Opus pricing not justified on Copilot AI Credits',
    },
    'se-technical-writer': {
      claude: 'sonnet',
      copilot: 'Claude Haiku 4.5 (copilot)',
      reason: 'template-driven docs; ~2x cheaper on AI Credits; Copilot has no effort key, so the Claude-side "Haiku ignores effort" concern does not apply on this harness',
    },
  },
};

const args = process.argv.slice(2);
const JSON_OUT = args.includes('--json');
const STRICT = args.includes('--strict');
const onlyArg = args.find((a) => a.startsWith('--check='));
const ONLY = onlyArg ? new Set(onlyArg.slice('--check='.length).split(',')) : null;
const runs = (name) => !ONLY || ONLY.has(name);

const findings = [];
function add(check, id, severity, message, paths, detail, fixHint) {
  findings.push({
    check, id: `${check}/${id}`, severity, message, paths,
    ...(detail ? { detail } : {}), ...(fixHint ? { fixHint } : {}),
  });
}

const norm = (s) => (s.charCodeAt(0) === 0xfeff ? s.slice(1) : s).replace(/\r\n?/g, '\n');
const read = (rel) => norm(readFileSync(join(ROOT, rel), 'utf8'));
const sha = (s) => createHash('sha256').update(s).digest('hex');
const words = (s) => s.split(/\s+/).filter(Boolean).length;
const stripQuotes = (v) =>
  (v.startsWith('"') && v.endsWith('"')) || (v.startsWith("'") && v.endsWith("'")) ? v.slice(1, -1) : v;

function walkFiles(dir, base = dir) {
  const out = [];
  for (const e of readdirSync(join(ROOT, dir), { withFileTypes: true })) {
    const rel = `${dir}/${e.name}`;
    if (e.isDirectory()) out.push(...walkFiles(rel, base));
    else out.push(rel.slice(base.length + 1));
  }
  return out;
}

// Minimal frontmatter access — top-level scalars and block lists only, which covers
// every key this audit needs (`name`, `model`, `effort`, `description`, `paths`, `applyTo`).
function splitFrontmatter(text) {
  const m = text.match(/^---\n([\s\S]*?)\n---\n?/);
  if (!m) return { fm: '', body: text, fmLines: 0 };
  return { fm: m[1], body: text.slice(m[0].length), fmLines: m[0].split('\n').length - 1 };
}
function fmScalar(fm, key) {
  const m = fm.match(new RegExp(`^${key}:[ \\t]*(.+)$`, 'm'));
  return m ? stripQuotes(m[1].trim()) : null;
}
function fmBlockList(fm, key) {
  const lines = fm.split('\n');
  const i = lines.findIndex((l) => l.trimEnd() === `${key}:`);
  if (i < 0) return null;
  const items = [];
  for (let j = i + 1; j < lines.length; j++) {
    const m = lines[j].match(/^\s+-\s*(.+)$/);
    if (!m) break;
    items.push(stripQuotes(m[1].trim()));
  }
  return items;
}

const stats = { mirroredFiles: 0, rulePairs: 0, agentTwins: 0, commandTwins: 0 };

// --- skills-mirror -----------------------------------------------------------
if (runs('skills-mirror')) {
  const claude = new Set(walkFiles('.claude/skills'));
  const github = new Set(walkFiles('.github/skills'));
  for (const f of claude) {
    if (!github.has(f)) {
      add('skills-mirror', 'missing-github', 'error', 'File exists only in the Claude tree',
        [`.claude/skills/${f}`], undefined, `Copy it to .github/skills/${f} (or delete both).`);
    }
  }
  for (const f of github) {
    if (!claude.has(f)) {
      add('skills-mirror', 'missing-claude', 'error', 'File exists only in the Copilot tree',
        [`.github/skills/${f}`], undefined, `Copy it to .claude/skills/${f} (or delete both).`);
    }
  }
  for (const f of claude) {
    if (!github.has(f)) continue;
    stats.mirroredFiles++;
    if (sha(read(`.claude/skills/${f}`)) !== sha(read(`.github/skills/${f}`))) {
      add('skills-mirror', 'differs', 'error', 'Mirrored skill file content differs (beyond line endings)',
        [`.claude/skills/${f}`, `.github/skills/${f}`], undefined,
        'git status/git diff shows which side carries the newer change; copy it over the other.');
    }
  }
}

// --- rules-parity ------------------------------------------------------------
const ruleNames = readdirSync(join(ROOT, '.claude/rules'))
  .filter((f) => f.endsWith('.md')).map((f) => f.slice(0, -3));
if (runs('rules-parity')) {
  const instructionNames = readdirSync(join(ROOT, '.github/instructions'))
    .filter((f) => f.endsWith('.instructions.md')).map((f) => f.slice(0, -'.instructions.md'.length));
  for (const n of instructionNames) {
    if (!ruleNames.includes(n)) {
      add('rules-parity', 'missing-twin', 'error', 'Instructions file has no Claude rule twin',
        [`.github/instructions/${n}.instructions.md`], undefined, `Create .claude/rules/${n}.md (or delete both).`);
    }
  }
  for (const n of ruleNames) {
    const rulePath = `.claude/rules/${n}.md`;
    const instrPath = `.github/instructions/${n}.instructions.md`;
    if (!existsSync(join(ROOT, instrPath))) {
      add('rules-parity', 'missing-twin', 'error', 'Claude rule has no Copilot instructions twin',
        [rulePath], undefined, `Create ${instrPath} (or delete both).`);
      continue;
    }
    stats.rulePairs++;
    const rule = splitFrontmatter(read(rulePath));
    const instr = splitFrontmatter(read(instrPath));

    const rGlobs = (fmBlockList(rule.fm, 'paths') ?? []).map((g) => g.trim()).sort();
    const iGlobs = (fmScalar(instr.fm, 'applyTo') ?? '').split(',').map((g) => g.trim()).filter(Boolean).sort();
    if (JSON.stringify(rGlobs) !== JSON.stringify(iGlobs)) {
      add('rules-parity', 'glob-mismatch', 'error', 'paths: and applyTo: globs are not the same set',
        [rulePath, instrPath], { claude: rGlobs, github: iGlobs },
        'Make the sets equal; the Claude paths: list is the canonical order.');
    }

    // Map the intentional cross-reference spellings before comparing bodies:
    // rule basenames rename to *.instructions.md, tree paths and the always-on file swap.
    let mapped = rule.body;
    for (const rn of ruleNames) {
      mapped = mapped
        .replaceAll(`\`.claude/rules/${rn}.md\``, `\`${rn}.instructions.md\``)
        .replaceAll(`\`${rn}.md\``, `\`${rn}.instructions.md\``);
    }
    mapped = mapped
      .replaceAll('.claude/skills/', '.github/skills/')
      .replaceAll('.claude/rules/', '.github/instructions/')
      .replaceAll('.claude/CLAUDE.md', '.github/copilot-instructions.md')
      .replaceAll('CLAUDE.md', 'copilot-instructions.md');
    const trim = (s) => s.split('\n').map((l) => l.trimEnd()).join('\n').replace(/\n+$/, '');
    const a = trim(mapped).split('\n');
    const b = trim(instr.body).split('\n');
    if (a.join('\n') !== b.join('\n')) {
      let i = 0;
      while (i < a.length && i < b.length && a[i] === b[i]) i++;
      add('rules-parity', 'body-drift', 'error', 'Bodies differ after normalization and reference mapping',
        [rulePath, instrPath],
        {
          claudeLine: rule.fmLines + i + 1, githubLine: instr.fmLines + i + 1,
          claude: (a[i] ?? '<end of file>').slice(0, 200), github: (b[i] ?? '<end of file>').slice(0, 200),
        },
        'Reconcile the wording and apply the same sentence to both files.');
    }
  }
}

// --- agent-twins -------------------------------------------------------------
if (runs('agent-twins')) {
  const claudeAgents = readdirSync(join(ROOT, '.claude/agents'))
    .filter((f) => f.endsWith('.md')).map((f) => f.slice(0, -3));
  const copilotAgents = readdirSync(join(ROOT, '.github/agents'))
    .filter((f) => f.endsWith('.agent.md')).map((f) => f.slice(0, -'.agent.md'.length));
  for (const n of copilotAgents) {
    if (!claudeAgents.includes(n) && !CONFIG.copilotOnlyAgents.includes(n)) {
      add('agent-twins', 'missing-claude-twin', 'error',
        'Copilot agent has no Claude twin and is not listed in copilotOnlyAgents',
        [`.github/agents/${n}.agent.md`], undefined,
        'Create the .claude/agents twin, or add the agent to copilotOnlyAgents in scripts/repo-audit.mjs.');
    }
  }
  for (const n of claudeAgents) {
    const cPath = `.claude/agents/${n}.md`;
    const gPath = `.github/agents/${n}.agent.md`;
    if (!copilotAgents.includes(n)) {
      add('agent-twins', 'missing-copilot-twin', 'error', 'Claude agent has no Copilot twin',
        [cPath], undefined, `Create ${gPath}.`);
      continue;
    }
    stats.agentTwins++;
    const cModel = fmScalar(splitFrontmatter(read(cPath)).fm, 'model');
    const gModel = fmScalar(splitFrontmatter(read(gPath)).fm, 'model');
    const override = CONFIG.modelParityOverrides[n];
    if (override && override.claude !== cModel) {
      add('agent-twins', 'model-parity', 'error',
        `Stale override: recorded against Claude model '${override.claude}' but the agent now pins '${cModel}'`,
        [cPath, 'scripts/repo-audit.mjs'], undefined,
        'Update or remove the modelParityOverrides entry so it matches reality.');
      continue;
    }
    const expected = override ? override.copilot : CONFIG.modelParity[cModel];
    if (!expected) {
      add('agent-twins', 'unknown-model', 'warn',
        `Claude model '${cModel}' has no entry in the modelParity table`,
        [cPath, 'scripts/repo-audit.mjs'], undefined, 'Add the mapping to CONFIG.modelParity.');
    } else if (gModel !== expected) {
      add('agent-twins', 'model-parity', 'error',
        `Copilot twin pins '${gModel}' but the parity ${override ? 'override' : 'table'} expects '${expected}'`,
        [cPath, gPath], override ? { override } : undefined,
        'Align the model, or record a documented override in CONFIG.modelParityOverrides (user approval required).');
    }
  }
}

// --- registry ----------------------------------------------------------------
if (runs('registry')) {
  const copilotMd = read('.github/copilot-instructions.md');
  const claudeMd = read('.claude/CLAUDE.md');
  const readme = read('README.md');

  const skillFolders = readdirSync(join(ROOT, '.github/skills'), { withFileTypes: true })
    .filter((e) => e.isDirectory()).map((e) => e.name);
  const availLine = copilotMd.split('\n').find((l) => l.includes('Available:')) ?? '';
  const listed = [...availLine.matchAll(/`([a-z0-9-]+)`/g)].map((m) => m[1]);
  for (const s of skillFolders) {
    if (!listed.includes(s)) {
      add('registry', 'skill-unlisted', 'error', `Skill folder '${s}' is missing from the copilot-instructions.md skills list`,
        ['.github/copilot-instructions.md', `.github/skills/${s}`], undefined, 'Add it to the "Available:" list.');
    }
  }
  for (const s of listed) {
    if (!skillFolders.includes(s)) {
      add('registry', 'skill-ghost', 'error', `copilot-instructions.md lists skill '${s}' but no such folder exists`,
        ['.github/copilot-instructions.md'], undefined, 'Remove the stale entry or restore the skill.');
    }
  }

  const commands = readdirSync(join(ROOT, '.claude/commands'))
    .filter((f) => f.endsWith('.md')).map((f) => f.slice(0, -3));
  const prompts = readdirSync(join(ROOT, '.github/prompts'))
    .filter((f) => f.endsWith('.prompt.md')).map((f) => f.slice(0, -'.prompt.md'.length));
  for (const c of commands) {
    if (!prompts.includes(c)) {
      add('registry', 'command-prompt-twin', 'error', `Command '/${c}' has no Copilot prompt twin`,
        [`.claude/commands/${c}.md`], undefined, `Create .github/prompts/${c}.prompt.md.`);
    }
  }
  for (const p of prompts) {
    if (!commands.includes(p)) {
      add('registry', 'command-prompt-twin', 'error', `Prompt '${p}' has no Claude command twin`,
        [`.github/prompts/${p}.prompt.md`], undefined, `Create .claude/commands/${p}.md.`);
    }
  }
  for (const c of commands) {
    if (!prompts.includes(c)) continue;
    stats.commandTwins++;
    const cDesc = fmScalar(splitFrontmatter(read(`.claude/commands/${c}.md`)).fm, 'description');
    const pDesc = fmScalar(splitFrontmatter(read(`.github/prompts/${c}.prompt.md`)).fm, 'description');
    if (cDesc !== pDesc) {
      add('registry', 'command-prompt-description', 'warn', `Command/prompt twin '${c}' descriptions differ`,
        [`.claude/commands/${c}.md`, `.github/prompts/${c}.prompt.md`],
        { claude: cDesc, github: pDesc }, 'Use the same description on both.');
    }
  }

  for (const n of readdirSync(join(ROOT, '.claude/agents')).filter((f) => f.endsWith('.md'))) {
    const base = n.slice(0, -3);
    if (!claudeMd.includes(base)) {
      add('registry', 'agent-unreferenced', 'warn', `Claude agent '${base}' is never referenced in CLAUDE.md`,
        ['.claude/CLAUDE.md', `.claude/agents/${n}`], undefined, 'Wire it into the delegation rules or remove it.');
    }
  }
  for (const n of readdirSync(join(ROOT, '.github/agents')).filter((f) => f.endsWith('.agent.md'))) {
    const display = fmScalar(splitFrontmatter(read(`.github/agents/${n}`)).fm, 'name');
    if (display && !copilotMd.includes(display)) {
      add('registry', 'agent-unreferenced', 'warn', `Copilot agent '${display}' is never referenced in copilot-instructions.md`,
        ['.github/copilot-instructions.md', `.github/agents/${n}`], undefined, 'Wire it into the agents section or remove it.');
    }
  }

  for (const n of ruleNames) {
    if (!readme.includes(`${n}.md`)) {
      add('registry', 'readme-mention', 'warn', `Rule '${n}.md' is not mentioned in README.md`,
        ['README.md', `.claude/rules/${n}.md`], undefined, 'Add it to the structure diagram / rules table.');
    }
  }
  for (const s of skillFolders) {
    if (!readme.includes(s)) {
      add('registry', 'readme-mention', 'warn', `Skill '${s}' is not mentioned in README.md`,
        ['README.md', `.github/skills/${s}`], undefined, 'Add it to the skills list.');
    }
  }
}

// --- cost-hygiene ------------------------------------------------------------
if (runs('cost-hygiene')) {
  for (const [file, budget] of Object.entries(CONFIG.budgets)) {
    const w = words(read(file));
    if (w > budget) {
      add('cost-hygiene', 'always-on-budget', 'warn',
        `${file} is ${w} words (budget ${budget}) — it loads in every session/request`,
        [file], { words: w, budget }, 'Trim or move detail into rules/skills; every word here is paid on every request.');
    }
  }
  const globOwners = new Map();
  for (const n of ruleNames) {
    const rulePath = `.claude/rules/${n}.md`;
    const { fm, body } = splitFrontmatter(read(rulePath));
    const globs = fmBlockList(fm, 'paths') ?? [];
    for (const g of globs) {
      if (CONFIG.broadGlobs.includes(g)) {
        add('cost-hygiene', 'broad-glob', 'warn',
          `Rule '${n}' fires on '${g}' — a very broad glob that injects its words into unrelated requests`,
          [rulePath, `.github/instructions/${n}.instructions.md`], { glob: g },
          'Narrow the glob (both trees) or fold the content into the general rule.');
      }
      if (!globOwners.has(g)) globOwners.set(g, []);
      globOwners.get(g).push({ rule: n, words: words(body) });
    }
  }
  for (const [g, owners] of globOwners) {
    if (owners.length < 2) continue;
    const total = owners.reduce((s, o) => s + o.words, 0);
    add('cost-hygiene', 'glob-overlap', 'warn',
      `${owners.length} rules all fire on '${g}' (${total} words loaded per matching edit): ${owners.map((o) => o.rule).join(', ')}`,
      owners.map((o) => `.claude/rules/${o.rule}.md`), { glob: g, totalWords: total },
      'Consider narrowing project-type-specific rules or converting them to on-demand skills.');
  }
  for (const f of readdirSync(join(ROOT, '.claude/agents')).filter((x) => x.endsWith('.md'))) {
    const { fm } = splitFrontmatter(read(`.claude/agents/${f}`));
    for (const key of ['model', 'effort']) {
      if (!fmScalar(fm, key)) {
        add('cost-hygiene', 'agent-pinning', 'warn', `Claude agent '${f}' does not pin '${key}'`,
          [`.claude/agents/${f}`], undefined, 'Pin it so cost does not silently follow the session default.');
      }
    }
  }
  for (const f of readdirSync(join(ROOT, '.github/agents')).filter((x) => x.endsWith('.agent.md'))) {
    if (!fmScalar(splitFrontmatter(read(`.github/agents/${f}`)).fm, 'model')) {
      add('cost-hygiene', 'agent-pinning', 'warn', `Copilot agent '${f}' does not pin 'model'`,
        [`.github/agents/${f}`], undefined, 'Pin it so cost does not silently follow the session picker.');
    }
  }
}

// --- changelog ---------------------------------------------------------------
if (runs('changelog')) {
  if (!existsSync(join(ROOT, 'CHANGELOG.md')) || !read('CHANGELOG.md').includes('## [Unreleased]')) {
    add('changelog', 'missing-unreleased', 'error', 'Root CHANGELOG.md is missing its ## [Unreleased] section',
      ['CHANGELOG.md'], undefined, 'Restore the Keep a Changelog structure.');
  }
}

// --- report ------------------------------------------------------------------
const errors = findings.filter((f) => f.severity === 'error');
const warnings = findings.filter((f) => f.severity === 'warn');
const checkNames = ['skills-mirror', 'rules-parity', 'agent-twins', 'registry', 'cost-hygiene', 'changelog'];
const report = {
  status: errors.length || (STRICT && warnings.length) ? 'findings' : 'clean',
  generatedAt: new Date().toISOString(),
  summary: {
    errors: errors.length,
    warnings: warnings.length,
    checks: Object.fromEntries(checkNames.map((c) => [c, {
      errors: findings.filter((f) => f.check === c && f.severity === 'error').length,
      warnings: findings.filter((f) => f.check === c && f.severity === 'warn').length,
    }])),
  },
  findings,
  affectedPaths: [...new Set(findings.flatMap((f) => f.paths))].sort(),
};

const summaryLine =
  `Repo audit ${report.status === 'clean' ? 'clean' : 'found drift'}: ` +
  `${stats.mirroredFiles} mirrored files, ${stats.rulePairs} rule pairs, ` +
  `${stats.agentTwins} agent twins, ${stats.commandTwins} command twin(s). ` +
  `${errors.length} error(s), ${warnings.length} warning(s)` +
  (warnings.length && !STRICT ? ' (warnings do not fail; use --strict to work them)' : '') + '.';

if (JSON_OUT) {
  console.log(JSON.stringify(report, null, 2));
} else {
  for (const c of checkNames) {
    const fs = findings.filter((f) => f.check === c);
    if (!fs.length) continue;
    console.log(`\n${c}`);
    for (const f of fs) {
      console.log(`  ${f.severity === 'error' ? 'ERROR' : 'WARN '} ${f.id}: ${f.message}`);
      for (const p of f.paths) console.log(`        ${p}`);
      if (f.detail) console.log(`        ${JSON.stringify(f.detail)}`);
    }
  }
  console.log(`\n${summaryLine}`);
}

process.exit(report.status === 'clean' ? 0 : 10);
