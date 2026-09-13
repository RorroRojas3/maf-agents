import { readFileSync } from 'node:fs';

// Every route destination. Adding or moving a page means updating this list.
const pageRoots = ['src/app/pages/home/home.ts'];

// .claude/rules/ui-architecture.md, "Pages and routing": none of these may reach the initial graph.
const lazyOnly = [/^src\/app\/pages\//, /^src\/app\/components\//, /^src\/app\/state\/[^/]+\//];

const isInitialEntryPoint = (entryPoint) =>
  entryPoint === 'src/main.ts' || entryPoint?.startsWith('angular:polyfills');

const { outputs } = JSON.parse(
  readFileSync(new URL('../dist/agents-ui/stats.json', import.meta.url), 'utf8'),
);

// Mirrors the builder: an output is initial when an initial output reaches it by a static import.
const initial = new Set();
const pending = Object.keys(outputs).filter((file) =>
  isInitialEntryPoint(outputs[file].entryPoint),
);
while (pending.length > 0) {
  const file = pending.pop();
  if (initial.has(file)) {
    continue;
  }
  initial.add(file);
  for (const { path, kind } of outputs[file].imports) {
    if (kind === 'import-statement' && path in outputs) {
      pending.push(path);
    }
  }
}

const failures = [];

if (![...initial].some((file) => outputs[file].entryPoint === 'src/main.ts')) {
  failures.push('No output has entryPoint src/main.ts; the stats.json shape may have changed.');
}

for (const file of initial) {
  for (const input of Object.keys(outputs[file].inputs)) {
    if (lazyOnly.some((pattern) => pattern.test(input))) {
      failures.push(`${input} is in the initial output ${file}.`);
    }
  }
}

for (const page of pageRoots) {
  const lazyEntry = Object.keys(outputs).some(
    (file) => outputs[file].entryPoint === page && !initial.has(file),
  );
  if (!lazyEntry) {
    failures.push(
      `${page} is not a lazy entry point; restore its loadComponent or update pageRoots.`,
    );
  }
}

if (failures.length > 0) {
  console.error(`Initial chunk gate failed:\n${failures.map((f) => `  - ${f}`).join('\n')}`);
  process.exitCode = 1;
} else {
  console.log(
    `Initial chunk gate passed: ${initial.size} initial outputs, ${pageRoots.length} page roots lazy.`,
  );
}
