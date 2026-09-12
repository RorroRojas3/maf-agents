---
paths:
  - "**/src/app/**"
  - "**/src/testing/**"
---

# UI architecture (Angular `src/app/`)

Where a file goes and what it is called. Code is grouped by **kind** at the top level and by **feature** one level down: every page is under `pages/`, every store under `state/`, and a feature's pieces share one folder name across those kinds. Dependencies run one way and ESLint enforces it. The rule is generic; the examples are this repository's.

```
src/app/
├── pages/<feature>/          route destinations, + <feature>.routes.ts if it owns child routes
├── components/<feature>/     components only that feature renders
├── state/                    root stores flat; state/<feature>/ for provided stores
├── services/<domain>/        transports, and clients two or more consumers share
├── core/<topic>/             auth, config, http, errors, events, store features, telemetry
├── shared/
│   ├── components/<name>/    reusable components (presentational, or bound to root stores only)
│   ├── directives/  pipes/
│   ├── models/               DTOs and shared types — no Angular, no NgRx
│   └── utils/<topic>/        pure functions — no Angular, no NgRx
├── app.ts  app.config.ts  app.routes.ts
src/testing/                  fixtures, fakes, the providers file (@testing/*)
```

## Rules that apply everywhere

- **Kind first, feature second.** A feature named `chat` has `pages/chat/`, `components/chat/`, `state/chat/`. The same folder name in every kind folder is what makes a feature findable.
- **Component files are suffix-free**: `chat.ts` / `chat.html` / `chat.scss`, class `Chat`, selector `app-chat`. Every other kind carries its suffix: `-store.ts`, `-client.ts` / `-service.ts`, `.guard.ts`, `.interceptor.ts`, `.routes.ts` (a `Routes` array), `-route.ts` (a URL or query-param contract), `.token.ts`, `.model.ts`, `with-*.ts`. The CLI generates the component form; the suffix on everything else is what a file search keys on. The separator is part of the convention: a **dash** where the suffix says what the file is one of (`-store`, `-client`, `-service`, `-route`), a **dot** on the Angular artefact kinds (`.guard`, `.interceptor`, `.routes`, `.token`, `.model`).
- **One component per folder only under `shared/components/`** (`shared/components/empty-state/empty-state.ts`). Under `components/<feature>/` files sit flat, nesting one level when a feature has sub-areas (`components/chat/transcript/assistant-turn.ts`). No `models/` or `utils/` sub-folders inside `components/` — a feature type is `<name>.model.ts` beside its consumer, because `components/admin/models/` is the model-catalog tab, not a types folder.
- **No barrel `index.ts`.** Import by full aliased path. Barrels hide the layer an import crosses and defeat tree-shaking of lazy chunks.
- **Specs are colocated**: `foo.spec.ts`, `foo.a11y.spec.ts`; one spec may cover a folder of tiny presentational components. Specs are exempt from every layer ban. Fixtures and fakes live in `src/testing/`, outside `src/app`.
- **One path alias per top-level folder**: `@pages/*`, `@components/*`, `@state/*`, `@services/*`, `@core/*`, `@shared/*`, `@testing/*`. Cross-layer imports use the alias; imports inside one folder may be relative.
- **Comments** follow the repository standard: one line, only the *why*, no story ids.

## Dependency direction

```
pages → components/<feature> → shared/components|directives|pipes → state → services → core → shared/utils → shared/models
```

- **Each layer imports only itself and the layers below it.** `shared/` is split across the graph on purpose: `shared/components` sits above `state` and may bind to root stores (a toast region, a user footer, a theme toggle), while `shared/utils` and `shared/models` sit at the bottom and import nothing from `@angular/*` or `@ngrx/*`, so they test in Node without a `TestBed`. Every `shared/` folder keeps its place in the graph and in the bans whether or not a given repo has filled it yet — an empty `shared/pipes/` is the layout, not drift.
- **`core` never imports `state`.** Guards and interceptors must not depend on what screens render. A core service that must kick a store dispatches an event the store observes in `withEventHandlers`, the way sign-out already resets every store. A store that a guard, interceptor or core service injects (`SessionStore`, `ToastStore`) is core infrastructure and stays in `core/`.
- **Cross-feature imports are banned** between `components/<a>` and `components/<b>` and between `pages/<a>` and `pages/<b>`. Pages are where cross-feature composition legally happens. `state/<a>` may import `state/<b>`: folders under `state/` are domains, not screens, and the domain graph stays acyclic by convention.
- **`shared/components` may inject root stores, never `state/<feature>/` stores.** A `NullInjectorError` is the symptom of a shared component that crossed a provider boundary.
- **Two features want the same store-connected component.** Presentational → `shared/components`. Bound to root stores only → `shared/components`. Bound to a provided store → a pageless feature, `components/<thing>/` + `state/<thing>/`, that pages compose (`components/composer/` + `state/composer/`). Two features that must *talk* → a contract in `core/<topic>/` (a token or an event group), never an import.
- **Enforcement** is one `no-restricted-imports` block per folder, spec files exempt, patterns anchored as `**/app/<folder>/*` so a relative import a refactor left behind is caught too. Per-feature blocks are generated by reading `src/app/components` and `src/app/pages` and negating the feature's own path — the top level only, so a feature's own sub-areas share one block and are not isolated from each other.

| Files | May not import |
| --- | --- |
| `shared/models/**` | `@angular/*`, `@ngrx/*`, anything else under `src/app` |
| `shared/utils/**` | same, except `shared/utils` |
| `core/**` | `shared/components`, `shared/directives`, `shared/pipes`, `services`, `state`, `components`, `pages` |
| `services/**` | `shared/components`, `shared/directives`, `shared/pipes`, `state`, `components`, `pages` |
| `state/**` | `shared/components`, `shared/directives`, `shared/pipes`, `components`, `pages` |
| `shared/{components,directives,pipes}/**` | `state/<feature>/*`, `components`, `pages` |
| `components/<f>/**` | `pages`, `components/<other>` |
| `pages/<f>/**` | `pages/<other>` |

## Placement

| I have a… | It goes to | Because |
| --- | --- | --- |
| Route component (a `loadComponent` target) | `pages/<feature>/<name>.ts` | The chunk boundary; the initial-chunk gate roots here |
| Feature route table | `pages/<feature>/<feature>.routes.ts`, default export | Only a feature owning child routes needs one; a nested detail route adds a second, `<entity>-detail.routes.ts`. Route-level `providers:` live here when child routes share one store instance |
| Component one feature renders | `components/<feature>/<name>.ts` | Screens own their parts |
| Component two or more features render, bound to inputs or root stores only | `shared/components/<name>/<name>.ts` | Reusable; lint caps it at root stores |
| Component two or more features render that needs a provided store | `components/<thing>/` + `state/<thing>/`, a pageless feature | Pages compose it; `components/<a> → components/<b>` stays banned |
| `signalStore({ providedIn: 'root' })` | `state/<name>-store.ts`, flat | App-wide state is one folder; a store core itself injects stays in `core/` |
| Store provided on one page or route | `state/<feature>/<name>-store.ts` | The folder is the domain; `providers:` is the scope |
| Non-root store provided by two features | `state/<domain>/<name>-store.ts` | Provided twice on purpose — the instances must not share; "root" is a provider scope, not a folder |
| Component-provided `@Injectable` written by one feature, read by another | `core/<topic>/<name>.ts` | A seam is a contract nobody owns; scope still comes from `providers:` on the page |
| HTTP call one store owns | in that store, `state/<name>-store.ts` | A client wrapping a single store's fetch is indirection, not a layer |
| Streaming transport, or a client two or more consumers share | `services/<domain>/<name>-client.ts`; non-HTTP `-service.ts`; its test-seam token beside it | Always a domain sub-folder — a flat `services/` reads as noise past four files |
| Reusable `signalStoreFeature` | `core/state/with-<name>.ts` | Store policy; imports only `core/` |
| Event group | `core/events/<domain>-events.ts` | The only legal upward signal out of `core` |
| `InjectionToken` with one owner | beside the owner, `<name>.token.ts` | A token exists for its consumer |
| `InjectionToken` implemented across features | `core/<topic>/<name>.token.ts`, its `provideX()` in the same file | `core` is the one layer every side reaches |
| Signal Forms policy (limits, pure validators) | `components/<feature>/<name>-form.ts` beside the dialog; `shared/utils/<domain>/` once a store reads it | One consumer is not policy |
| Route path constants | `core/navigation/route-paths.ts`, one file | Guards read them and `core` cannot import `pages` |
| URL or query-param contract | `pages/<feature>/<feature>-route.ts`; `shared/utils/navigation/` once a store reads it | The lowest layer that reads it |
| Wire DTO | `shared/models/api/<resource>.model.ts`; a vendored contract under `shared/models/<topic>/` | Mirrors the API's DTO project |
| Pure function | `shared/utils/<topic>/<name>.ts`, no suffix | Node-testable by lint, not by convention |
| Generic directive or pipe | `shared/directives/<name>.ts`, `shared/pipes/<name>.ts`, flat | Small files; a folder each is noise |
| Directive that injects a feature store | `components/<feature>/<name>.ts` | Injecting a feature store makes it that feature's |
| `provide*()` function | page-scoped → `components/<feature>/`; app-scoped → `core/<topic>/` | Only `app.config.ts`, `*.routes.ts` and pages call them |
| Test fixture or fake | `src/testing/<topic>.ts` | Outside the app graph; may import anything |

## Pages and routing

- **`app.routes.ts` is the only eager route table.** Every page is reached through `loadComponent` or `loadChildren`; guards and matchers come from `core/`; query params arrive as `input()` through `withComponentInputBinding()`. Eager exceptions (the sign-in callback, the failure pages) are listed in the table, never implied.
- **The initial graph is what `main.ts`, `app.*` and `core/**` statically import.** Nothing under `pages/`, `components/` or `state/<feature>/` may be imported from there.
- **A page's `providers:` lists the stores and `provide*()` calls whose files are reached only from its own chunk.** Nothing only a lazy screen renders is `providedIn: 'root'` — root provision does not pull code into the initial graph by itself, but the static import that accompanies it does. The build gate that checks the initial chunk names the page files it roots at; moving a page means updating them.

## State

- **One store per entity type.** Root stores flat in `state/`; provided stores in `state/<feature>/`; the store's lifetime is decided by `providedIn` or `providers:`, never by its folder.
- **Compose the shared `core/state/with-*` features**, and the reset-on-sign-out feature **last** — it snapshots state at construction, so a state-contributing feature after it would survive sign-out.
- **`rxMethod` with `switchMap` wherever requests can overlap**; `protectedState` stays on; state is written only through `patchState` with standalone updaters.
- **Cross-store coordination goes through `core/events/`.** A component never subscribes to `Events`; the store does, in `withEventHandlers`.

## Forms

- **Signal Forms**: `form()` over a `signal(model)`, schema rules, `[formField]` bindings. Never `ReactiveFormsModule` or `FormsModule` in new code.
- The form component is `components/<feature>/<name>-dialog.ts` or `<name>-form.ts`; its limits and pure validators sit in a sibling `<name>-form.ts`; server validation maps through the errors module in `core/errors/`.

## Components

- `ChangeDetectionStrategy.OnPush`, `input()` / `output()` / `model()`, `inject()`, `host: {}` instead of `@HostBinding`, `@if` / `@for` / `@empty`.
- `imports:` alphabetized. Import statements ordered Angular → `@shared/models` → `@core` → `@services` → `@state` → `@shared` → `@components` → relative, which is the dependency graph read bottom-up.
