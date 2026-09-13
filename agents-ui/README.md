# Agents UI

The Angular client for the agents platform — an Angular 22.1 workspace (project `agents-ui`, zoneless by default). It's configured, not yet a product: an app shell with a theme toggle and one lazy home page, calling no API yet.

For prerequisites, the npm scripts, project layout, styling, theming and the lint/build gates that enforce the layer rule, see [`docs/ui/angular-workspace.md`](../docs/ui/angular-workspace.md).

## Scripts

| Script                                    | Runs                                                          |
| ----------------------------------------- | ------------------------------------------------------------- |
| `npm start`                               | `ng serve` on `http://localhost:4200`                         |
| `npm run build`                           | Production build to `dist/agents-ui/`                         |
| `npm run watch`                           | Incremental development build                                 |
| `npm test`                                | Unit tests (Vitest, via `ng test`)                            |
| `npm run lint`                            | ESLint (`ng lint`)                                            |
| `npm run format` / `npm run format:check` | Prettier, write or check                                      |
| `npm run check:initial-chunk`             | Fails if a page or feature file leaks into the initial bundle |

See [`docs/ui/angular-workspace.md`](../docs/ui/angular-workspace.md) for what each script verifies.
