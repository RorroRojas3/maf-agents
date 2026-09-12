# ADR-0003: A database-owned agent catalog and a background session-usage projection

**Status**: Accepted
**Date**: 2026-09-12
**Deciders**: Rodrigo Rojas (repository owner)

## Context

Cosmos DB's `sessions` container already keeps a cumulative `SessionUsage` per session (see [Sessions and history](../conversations/sessions-and-history.md)), but that number has no model attribution, no price, and no way to be reported on relationally — grouping by agent or model, filtering by period, or summing cost across every session is exactly what a document store answers badly and a relational one answers for free. The solution already operates a relational store for this shape of problem: [ADR-0002](0002-sql-server-policy-store.md) added SQL Server 2025 through EF Core specifically to demonstrate migrations, constraints, and concurrency, and `PolicyDbContext` is sitting there as the natural place to extend rather than stand up a third store.

Separately, which model each agent runs was implicit in configuration (`AzureOpenAI:Model`), with no history, no price recorded anywhere, and no enforced relationship to anything else.

Both problems point at the same fix: give the agent-to-model relationship, and its prices, a real schema with real constraints, and let the existing relational store hold a reporting projection of usage instead of trying to query it out of Cosmos DB.

## Decision

Add three reference tables under `[Core.Ref]` (`Agent`, `Model`, `AgentModelMapping`) and one reporting table, `[Core].[Session]` (entity `SessionSummary`), all mapped by the existing `PolicyDbContext`. See [Architecture overview](../architecture/overview.md#the-agent-catalog-and-session-usage-summaries) for the schema.

**1. The catalog lives in the database, seeded by migration, cached for 24 hours, and never written by the app.** The migration that creates the tables seeds the weather agent through `HasData`, keyed by `Common/Constants/AgentIds.cs`, and inserts `GPT 5.6 Luna` and the mapping between them as hand-written `InsertData`: operators own those two rows once they exist, so they never enter the EF model, where a later migration would update or delete them. After that, a price change is a database change alone; moving an agent to another model is a database change plus `AzureOpenAI:Model`, because the deployment its chat client calls stays configuration. `Service/Caching/AgentCatalogCache.cs` loads every active mapping in one query and holds it in `IMemoryCache` for 24 hours, so a summary write adds no catalog query of its own. `Api/Startup/AgentCatalogBootstrapper.cs` warms the cache and refuses to start unless every hosted agent has an active model whose deployment matches what its chat client is actually configured to call — the two must agree, because usage is attributed to and priced from the catalog's model. This makes SQL Server a required dependency at startup, not only at `/health/ready`.

**2. Usage is projected into `[Core].[Session]` by a background, cumulative, priced-delta writer — never inline on the turn.** After a session document is saved or deleted in Cosmos DB, `PersistedAgentSessionStore` and `SessionService` enqueue the session's latest counts on a bounded channel; `SessionSummaryProcessor`, a `BackgroundService`, drains it in order and upserts through `ISessionSummaryRepository`. Each upsert compares the new counts to what's stored, ignores a write whose counts are lower (an out-of-order or duplicate write), and otherwise prices only the *delta* at the catalog's current price and adds it to a running `EstimatedCost` — so a session's cost is exact regardless of how many partial writes land, and pricing always reflects the price in effect at write time rather than whatever it was when the turn ran.

**3. A session's continuation id must be a GUID.** `[Core].[Session].SessionId` is a `uniqueidentifier`, and `[Core].[Session].UserId` is too, so the id Cosmos DB was previously happy to store as any non-slash string now has to parse as one. `SessionIdValidator` accepts only the hyphenated (`D`) or plain (`N`) GUID form; both hosts already issue `N` ids, so this narrows what a client-supplied id may look like without changing what either host generates.

## Consequences

**Positive:**

- Reporting queries — cost per agent, cost per model, usage in a period — run directly against `[Core].[Session]` with ordinary joins and `GROUP BY`, instead of scanning Cosmos DB documents client-side.
- The agent-model relationship is now schema-enforced: a filtered unique index guarantees exactly one active model per agent, a check constraint guarantees `DateDeactivated >= DateCreated`, and foreign keys guarantee a session summary can't name an agent or model that doesn't exist.
- A price change takes effect without a redeploy — within the 24-hour cache lifetime, or immediately after a restart. Moving an agent to another model still sets `AzureOpenAI:Model` and restarts, and startup refuses to run until the two agree.
- The projection never touches the turn: a slow or unavailable SQL Server delays only reporting, never a caller's response.

**Negative:**

- **SQL Server is now required at startup, not only at `/health/ready`.** A contributor who only needs the weather agent, and previously could defer standing up SQL Server, no longer can — `AgentCatalogBootstrapper` fails startup outright without it.
- **Two breaking changes ship alongside this:** `dateUpdated`/`DateUpdated` is renamed to `dateModified`/`DateModified` everywhere (Cosmos DB document, `api/conversations` response, and `[Core].[Policy]`), and a session id that isn't a GUID is now rejected where it previously wasn't. Both are documented in [Sessions and history](../conversations/sessions-and-history.md) and the [changelog](../../CHANGELOG.md).
- **A queued write can be lost**: a process crash, a full channel (10,000 items), or a non-transient failure all drop the item rather than blocking the request that produced it. The session's next save or deletion re-sends complete counts, so the gap self-heals on the next turn — except a lost deletion stamp, which stays lost until an operator notices. This tradeoff is what keeps the projection off the turn path; making it durable would mean putting SQL Server (or a durable queue) back in the request's critical path, the opposite of what this decision is for.
- **A price or mapping change takes up to 24 hours to reach a running instance** unless it's restarted, because the catalog cache doesn't know the database changed underneath it.

**Neutral:**

- `[Core].[Policy]` changes only by the `DateUpdated` → `DateModified` column rename, applied in place with its data. `AuditTimestampInterceptor` now keeps a timestamp the caller supplied, but nothing sets one on a policy, so its stamps behave as before.

## Alternatives Considered

**Option 1: Keep the catalog in `appsettings.json`, alongside `AzureOpenAI:Model`.**

- Pros: no new tables; the same pattern already used for `AzureOpenAI`/`MicrosoftFoundry`.
- Cons: the owner rejected this. Configuration has no way to express "exactly one active model per agent" or "a model's deployment name is unique" short of hand-written validation, gives no history of when a mapping changed, and — the main complaint — turns a price update into a configuration change that needs a redeploy or at least a restart, for data that changes on a business cadence, not a release cadence.

**Option 2: Seed the catalog with EF Core's `UseSeeding`/`UseAsyncSeeding` instead of `HasData`.**

- Pros: `UseSeeding` is EF Core's own recommended replacement for `HasData`-based "model managed data," runs arbitrary code, and isn't captured into the migration snapshot.
- Cons: `UseSeeding` only runs when EF Core itself performs the database operation — `EnsureCreated`, `Migrate`/`MigrateAsync`, `dotnet ef database update`, or a migration bundle. It does **not** run when a generated script is executed by an external SQL tool, which is exactly this solution's production deployment path (`migrations script --idempotent`, applied with `sqlcmd`; see [ADR-0002](0002-sql-server-policy-store.md) and the [runbook](../operations/runbook.md#migrations)). `HasData` seeds through ordinary `InsertData` calls that *do* appear in the generated script, so it is the only one of the two that actually seeds a database provisioned this way.

**Option 3: Write the session summary inline, in the same call that saves the Cosmos DB session document.**

- Pros: no channel, no background service, no residual-loss window — the summary is as fresh as the session document itself.
- Cons: `SaveSessionAsync` is called by both hosts with no retry or compensating error handling around it, and AG-UI in particular calls it only after the SSE stream has already finished sending `RUN_FINISHED` to the client. A synchronous SQL write inserted there would stall or fail the save on ordinary SQL Server latency or a transient failure, with no good way to surface that to a client that already got its answer — turning a reporting concern into an availability risk for the conversation itself.

**Option 4: Price each turn immediately, in the session's state bag, instead of pricing the delta when the summary writer runs.**

- Pros: the estimated cost would be visible in the same place the running token totals already are, with no separate pricing step.
- Cons: pricing a turn requires the catalog's current price, which means either the turn path takes a dependency on `IAgentCatalogCache` (and, on a cache miss, on SQL Server) or the price gets baked into session state that a much-later turn might replay against a stale figure. Pricing only once, in the background writer, keeps both the cache and SQL Server entirely off the turn path — the same property Option 3 was rejected for giving up.

## References

- [Data seeding](https://learn.microsoft.com/ef/core/modeling/data-seeding#configuration-options-useseeding-and-useasyncseeding-methods) — `UseSeeding`/`UseAsyncSeeding` versus `HasData`, and that a SQL script run by an external tool invokes neither
- [Applying migrations — idempotent SQL scripts](https://learn.microsoft.com/ef/core/managing-schemas/migrations/applying#idempotent-sql-scripts) — the deployment path this decision seeds through
- [ADR-0002: SQL Server 2025 as a second, relational store for policies](0002-sql-server-policy-store.md) — the store this decision extends, and why it's EF Core and compatibility level 170
- [Architecture overview](../architecture/overview.md#the-agent-catalog-and-session-usage-summaries) — the schema this decision produces
- [Sessions and history](../conversations/sessions-and-history.md#session-usage-reporting) — the queue, the writer, and its failure handling
