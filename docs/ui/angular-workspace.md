# Angular workspace (`agents-ui/`)

## Overview

`agents-ui/` is the Angular client for the agents platform: an Angular **22.1** workspace (project `agents-ui`, prefix `app`), zoneless by default. It is configured, not yet a product — an app shell with a theme toggle and one lazy page — and calls no API yet: no `HttpClient`, no auth, no dev proxy to `agents-api/`. Its job today is the scaffolding a real feature will land in: the folder layout, the lint rules that keep imports flowing one way, and the build gate that keeps a route out of the initial bundle.

Use this page to get the workspace running and to understand the checks its `npm` scripts run. For where a new file goes and which layers may import which, go straight to [`.claude/rules/ui-architecture.md`](../../.claude/rules/ui-architecture.md) — this page doesn't restate its tables.

## Prerequisites and first run

- **Node** `^22.22.3 || ^24.15.0 || >=26` (the Angular CLI's supported range).
- **npm** as the package manager (`packageManager: npm@11.16.0` in `package.json`).

```bash
cd agents-ui
npm ci
npm start
```

`npm start` runs `ng serve` on `http://localhost:4200`, rebuilding on save. There's nothing to sign in to and nothing to point at `agents-api/` yet.

## npm scripts

| Script | Runs | What it verifies |
| --- | --- | --- |
| `npm start` | `ng serve` | Dev server on `http://localhost:4200`, no checks. |
| `npm run build` | `ng build` (production by default) | Compiles to `dist/agents-ui/` and enforces the production budgets — a warning at 750 kB and a hard error at 1 MB for the initial bundle, 4 kB/8 kB for any single component's styles. |
| `npm run watch` | `ng build --watch --configuration development` | Unoptimized incremental build for local iteration; no budgets. |
| `npm test` | `ng test` | Unit tests via the Angular build system's Vitest 4 integration, on jsdom. `TestBed` is zoneless by default, matching the app. Pass `-- --watch=false` for a single run. |
| `npm run lint` | `ng lint` (`@angular-eslint/builder:lint`) | ESLint over `src/**/*.ts` and `*.html` — angular-eslint's recommended and accessibility template rules, the NgRx `signalsTypeChecked` rules, the OnPush and `inject()`/signal-API preferences, the `FormsModule`/`ReactiveFormsModule` ban, and the layer-boundary bans described below. |
| `npm run format` / `npm run format:check` | Prettier (write / check) | Formatting plus import order (`@ianvs/prettier-plugin-sort-imports`): Angular, then third-party, then `@shared/models` → `@core` → `@services` → `@state` → `@shared` → `@components` → `@pages` → `@testing` → relative. |
| `npm run check:initial-chunk` | `ng build --stats-json` then `node scripts/check-initial-chunk.mjs` | The initial-chunk gate — see below. |

Verified locally so far: a production build with no warnings, 7 unit tests passing, a clean lint and a clean Prettier check, the gate passing and failing correctly on a deliberately static-imported page, and every layer ban firing (and not firing) on deliberate violations. A headless-browser run against `ng serve` also confirmed no `zone.js` and no console errors, the brand palette in both themes (computed colors of the common Bootstrap components, with every text pair at WCAG AA or better), the icon font, the lazy home page, the tooltip (its text updating while it stays open), the OS color scheme followed while nothing is stored, and a toggled theme surviving a reload.

## Project layout and naming

Files are grouped by kind at the top level (`pages/`, `components/`, `state/`, `services/`, `core/`, `shared/`) and by feature one level down, with one path alias per top-level folder (`@pages/*`, `@components/*`, `@state/*`, `@services/*`, `@core/*`, `@shared/*`, `@testing/*`) and no `baseUrl` — TypeScript 6 deprecates it, so `tsconfig.json` carries only `paths`. The full layout, naming suffixes, dependency direction and placement tables live in [`.claude/rules/ui-architecture.md`](../../.claude/rules/ui-architecture.md); it applies automatically to anything under `src/app/` and `src/testing/`.

Two schematic defaults worth knowing before you run `ng generate`: components generate with no stylesheet (`"style": "none"`; pass `--style scss` when a component genuinely needs one), and guards and interceptors use a **dot** type separator (`auth.guard.ts`, not `auth-guard.ts`), matching the rule's dot-vs-dash convention for Angular artifact kinds.

## Styling

Bootstrap is compiled from source with the Andes brand palette. `src/styles.scss` loads it with `@use 'bootstrap/scss/bootstrap' with (...)`, so buttons, focus rings, form controls, active states and alert tints all derive from the palette, and Bootstrap picks readable text for each button fill. Bootstrap Icons still loads as precompiled CSS from `angular.json`'s `styles` array. Angular compiles Sass with `quietDeps`, so Bootstrap's internal deprecation notices stay silent and the build prints no warnings.

| Token | Hex | Light theme | Dark theme |
| --- | --- | --- | --- |
| Summit Navy | `#14324F` | `primary`: buttons, links, headings, active states | Elevated surfaces (`--bs-secondary-bg`; the navbar is a Navy/Ink mix) |
| Ink | `#0B1F33` | Body text, and text on Glacier Blue and Andes Green fills | Body background, and text on Glacier Blue and Andes Green fills |
| Glacier Blue | `#21A8D8` | `info`: accents, spinners | `info`, links, and `primary`: buttons, focus rings, active states |
| Andes Green | `#3FB56A` | `success` | `success` |
| Slate | `#5A6E82` | `secondary`, muted text, icons | `secondary` fills, icons on the Ink background |
| Snow | `#F7FAFC` | Body background | Body text; muted text is Snow at 75% |

Three constraints come from contrast, all measured in both themes:

- **Primary changes color in the dark theme.** Bootstrap builds `$primary` into its components once for both themes, and Summit Navy is 1.27:1 against Ink. A `[data-bs-theme='dark']` block in `styles.scss` switches the following to Glacier Blue: primary buttons, focus rings, checked inputs, nav pills, dropdown and list-group active items, pagination, and progress bars. Any other Bootstrap component with primary baked in stays navy in dark mode, so add it to that block when the app first uses it.
- **Muted text is lighter in the dark theme.** Slate is 3.17:1 on Ink, below the 4.5:1 AA minimum, so dark muted text is Snow at 75% (9.4:1). Slate stays on icons, where 3:1 is enough on Ink. On the lighter dark navbar it drops to 2.8:1, so the dark `.btn-outline-secondary` uses the muted tone instead.
- **Glacier Blue and Andes Green aren't light-theme text colors.** They reach only 2.6:1 and 2.5:1 on Snow. Use them as fills, icons and indicators; for text on light backgrounds use `text-info-emphasis` and `text-success-emphasis`, both about 10:1.

Bootstrap and Icons CSS are most of the initial bundle's weight: about 315 kB raw of a current initial total of about 577 kB raw (~109 kB estimated transfer), against a production budget that warns at 750 kB and errors at 1 MB.

`@ng-bootstrap/ng-bootstrap` (21.0.0) was installed with plain `npm install`, not `ng add`. Its schematic registers `NgbModule` application-wide and writes a Bootstrap SCSS `@import` into `styles.scss`, a form current Sass flags as deprecated where the workspace's `@use … with` compiles clean. Import the standalone `Ngb*` directives (`NgbTooltip`, and so on) per component instead. `@angular/localize` was added separately, through its own `ng add`, because ng-bootstrap's components call `$localize`; it's the app's only polyfill (`polyfills: ["@angular/localize/init"]` in `angular.json`).

## Theming: `ThemeStore`

`src/app/state/theme-store.ts` is a root NgRx SignalStore (`{ providedIn: 'root' }`, `protectedState` on by default) holding one field, `mode: 'light' | 'dark'`. Three things make it worth reading before you add another root store:

- **The initial mode falls back through three sources**: a stored choice in `localStorage`, then `prefers-color-scheme`, then `light`.
- **`watchState` in `withHooks` is what actually paints the theme** — it sets `data-bs-theme` on `<html>` on every state change, which is all Bootstrap needs to repaint.
- **Only an explicit choice is persisted.** `setMode()` and `toggle()` write to `localStorage` (key `andes.theme`); the constructor's OS-following fallback never does. A visitor who never touches the toggle keeps following their OS theme across visits; one who does stays on their choice even if the OS changes.

```typescript
export const ThemeStore = signalStore(
  { providedIn: 'root' },
  withState<ThemeState>(() => ({ mode: initialMode(inject(DOCUMENT)) })),
  withMethods((store, document = inject(DOCUMENT)) => ({
    setMode(mode: ThemeMode): void {
      patchState(store, setThemeMode(mode));
      writeStoredMode(document, mode); // explicit choice only — the constructor's fallback never persists
    },
    toggle(): void {
      patchState(store, toggleThemeMode());
      writeStoredMode(document, store.mode());
    },
  })),
  withHooks({
    onInit(store) {
      const document = inject(DOCUMENT);
      watchState(store, ({ mode }) => document.documentElement.setAttribute('data-bs-theme', mode));
    },
  }),
);
```

`shared/components/theme-toggle/` is the store's one consumer: a button with a sun/moon icon, an `ngbTooltip`, and an `aria-label`, both driven off `store.mode()`. It lives under `shared/components/` rather than `components/<feature>/` because it binds to a root store only — see the rule's placement table for that distinction.

## Enforcing the layer rule: ESLint

`eslint.config.js` turns the rule's dependency-direction table into one `no-restricted-imports` block per folder, with per-feature blocks generated at lint time from the top-level folders under `src/app/components` and `src/app/pages` — add a feature folder and its block appears on the next lint run, no config edit needed. Spec files are exempt everywhere, so a test can reach across layers to set up its fixtures.

**Known limit: the bans match import strings, not resolved module paths.** A relative import that never spells out an `app/` segment — `../../state/theme-store` from three folders deep, say — slips past every pattern, because the patterns are anchored on `**/app/<folder>/*` and the alias form. The practical rule: cross-layer imports always use the alias (`@state/theme-store`), never a relative path; relative imports are for files inside the same folder.

Two smaller things worth knowing if you touch `eslint.config.js`: flat config replaces a rule's options wholesale when two blocks overlap, so the `FormsModule`/`ReactiveFormsModule` ban is repeated in every layer block rather than declared once; and an exception pattern negates the whole directory (`!@components/<feature>`, not a narrower glob), because a gitignore-style matcher can't re-include a child of a directory it already excluded.

## The initial-chunk gate

`npm run check:initial-chunk` builds with `--stats-json` and then walks the resulting `dist/agents-ui/stats.json` (`scripts/check-initial-chunk.mjs`) to answer one question the bundler's budgets can't: not *how big* the initial chunk is, but *what's in it*. It fails when:

- anything under `src/app/pages/`, `src/app/components/` or `src/app/state/<feature>/` is statically reachable from `main.ts` or the polyfills entry point — the initial-graph rule in `ui-architecture.md`; or
- a file listed in the script's `pageRoots` array is **not** a lazy entry point of its own.

`pageRoots` is a plain list of route-destination files (currently just `src/app/pages/home/home.ts`). **Adding or moving a page means adding or updating its entry in `pageRoots`.** The first check covers every file under `pages/` whether or not it's listed; the list adds proof that each page is still its own lazy entry point, which the gate can't infer from `app.routes.ts`. A listed page that moves fails the gate until its entry is updated.

## See also

- [`.claude/rules/ui-architecture.md`](../../.claude/rules/ui-architecture.md) — the layout, naming and dependency-direction rule this workspace follows.
- The root `.mcp.json` also defines an `angular-cli` MCP server (`npx @angular/cli@22 mcp --read-only`) for querying Angular CLI documentation and schematics from Claude Code.
