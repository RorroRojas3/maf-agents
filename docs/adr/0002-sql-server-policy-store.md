# ADR-0002: SQL Server 2025 as a second, relational store for policies

**Status**: Accepted
**Date**: 2026-09-10
**Deciders**: Rodrigo Rojas (repository owner)

## Context

Every store in the solution so far is Azure Cosmos DB, reached through the raw `Microsoft.Azure.Cosmos` SDK — a fit for the session and message documents it holds, but not a demonstration of how this solution's layering (`.claude/rules/api-architecture.md`) handles a relational store, migrations, or optimistic concurrency. The owner asked for a second, unrelated domain — one insurance policy per row — added purely to exercise that shape: generated migrations, check constraints, deliberate indexes, a concurrency token, audit stamps, connection resiliency, and a readiness probe, with production-shaped decisions throughout even though nothing consumes the table yet.

SQL Server 2025 (compatibility level 170) and EF Core 10.0.12 are both current at the time of this decision. `Repository` already has a provider-first shape for Cosmos DB (`Repository/Cosmos/`); the rule's own template reserves `DbContexts/`, `Configurations/`, and `Migrations/` for exactly this case.

## Decision

Add SQL Server 2025 as a second store, reached exclusively through EF Core 10.0.12, provider-first in `Repository/Sql/` — a sibling of `Repository/Cosmos/`, not a refactor of it. `PolicyDbContext` maps one entity, `Policy`, to `[Core].[Policy]`; nothing above the database layer (no repository, service, or endpoint) reads or writes it yet, and `Microsoft.EntityFrameworkCore.*` resolves in this one project only.

Several sub-decisions follow from treating this as production-shaped rather than a toy:

- **EF Core with migrations**, not the raw `Microsoft.Data.SqlClient` API a Cosmos-style hand-rolled client would parallel. A relational schema with constraints and indexes is exactly the case change tracking and a migrations history table earn their cost; `ef-core` guidance now has something in the solution to apply to.
- **An explicit compatibility level, 170**, set in code (`SqlPersistenceConfiguration.ConfigureSqlServer`) rather than left to the server's default. EF Core otherwise assumes 150 (SQL Server 2019) and never emits syntax newer than that, silently forfeiting SQL Server 2025 features a database at 170 actually has.
- **A sequential-GUID clustered key**, not `Guid.CreateVersion7()`. SQL Server orders `uniqueidentifier` by its last six bytes first; a v7 GUID's leading timestamp bytes would scatter every insert across the clustered index instead of appending to it the way a `uniqueidentifier`-native sequential generator does.
- **`rowversion` concurrency** on every row, so a stale update or delete throws `DbUpdateConcurrencyException` instead of silently overwriting a concurrent change — the same guarantee Cosmos DB's ETag gives sessions, in the vocabulary of a relational store.
- **Status stored by name, enforced by a binary-collation check constraint.** `PolicyStatuses` persists as `varchar`, not an integer, so a row is self-describing in a query tool without a lookup table; the check constraint (`Enum.GetNames<PolicyStatuses>()`, compared under `Latin1_General_100_BIN2`) keeps the column limited to real members under a case- and accent-sensitive comparison, catching both a bad value and — because the constraint is generated from the enum — a forgotten migration when a member is added.
- **The design-time factory lives in `Repository`**, not Api, because `dotnet ef` must build `PolicyDbContext` without booting the Api host — which would demand Foundry, Cosmos DB, and `AzureAd` configuration a migration author has no reason to supply. `PolicyDbContextDesignTimeFactory` calls the same `ConfigureSqlServer` the runtime path uses, so a migration is always generated against the provider settings the app actually runs with.
- **Migrations are applied by the deployment pipeline; startup migration is development only.** `SqlSchemaMigrator.MigrateAsync` (`Database.MigrateAsync`) runs from `Api/Startup/SqlBootstrapper.cs` only when `SqlDb:ApplyMigrationsOnStartup` is `true` — the same shape as `CosmosBootstrapper`. Everywhere else, an idempotent script (`migrations script --idempotent`) or a migrations bundle runs from the pipeline, with an identity that holds DDL rights the application's own login never does.
- **`SqlDb` is a required dependency, and the table stays database-layer-only for now.** Startup validates `ConnectionStrings:DefaultConnection` unconditionally and `/health/ready` includes a `sql` check, even though no request path touches `Policy` yet — an owner decision to prove the store is live and healthy before anything is built on top of it, rather than making the dependency optional until a consumer exists.

## Consequences

**Positive:**

- The solution now demonstrates the EF Core-and-migrations shape `api-architecture.md` reserves folders for, previously unexercised.
- `PolicyConfiguration`'s check constraints, indexes, and `rowversion` concurrency are enforced by SQL Server itself, not only by application code — the same protection under a direct `INSERT`/`UPDATE` as through the (still nonexistent) repository layer.
- The design-time factory and the shared `ConfigureSqlServer` method mean a migration can never drift from the provider settings the app runs with; there is exactly one place either changes.

**Negative:**

- **Every environment now needs a running SQL Server** — local Docker, or SQL Server 2025 / Azure SQL beyond it — where previously only Cosmos DB was required. A contributor who only needs the weather agent still has to stand up a database for a feature nothing calls yet.
- **`/health/ready` now depends on SQL Server too**, and the check performs a real, unpooled login on every call to report the server's live state rather than a pooled session's — the same class of cost the existing Cosmos DB check already accepted, now doubled and on the same anonymous, unmetered endpoint.
- **A future move to `Microsoft.Data.SqlClient` 7.0 (which EF Core 11 depends on) would require adding `Microsoft.Data.SqlClient.Extensions.Azure`** to keep any `Authentication=Active Directory *` connection string working — SqlClient 7.0 extracted the Entra ID authentication providers out of the core package. EF Core 10 still resolves SqlClient 6.1, which bundles them, so nothing needs to change today; it will the next time EF Core is upgraded past that boundary.

**Neutral:**

- This ADR covers the store and schema decision only. Adding a repository, service, or endpoint on top of `Policy` — and translating `SqlException` into a domain exception before it would otherwise reach `GlobalExceptionHandler` — is future work, not decided here.

## Alternatives Considered

**Option 1: Add policies to Cosmos DB instead of a second store.**

- Pros: no new infrastructure dependency; the solution stays on the one store it already operates, secures, and monitors.
- Cons: defeats the purpose of the exercise, which was specifically to add a *relational* store and demonstrate EF Core migrations, check constraints, and rowversion concurrency — none of which a document store expresses the same way. A policy's check constraints and composite index would become application-level validation with no enforcement at the store.

**Option 2: Raw ADO.NET or Dapper against SQL Server, no EF Core.**

- Pros: no ORM, full control over every statement, no migrations tooling to operate.
- Cons: hand-written schema management (no `dotnet ef migrations`, no generated idempotent script), hand-written optimistic-concurrency checks in every statement instead of `IsRowVersion()`, and no design-time tooling story — `api-architecture.md`'s `DbContexts/`, `Configurations/`, `Migrations/` folders exist for exactly the shape EF Core provides.

**Option 3: SQL Server 2022 instead of 2025.**

- Pros: a longer-established image, more prior art for the container and its tooling.
- Cons: compatibility level 170 is SQL Server 2025 (or Azure SQL) only; targeting 2022 would mean either accepting level 160 or documenting a level the running server can't actually support. The point of pinning 170 explicitly was to run at the version this decision names, not stop one short of it.

## References

- [ALTER DATABASE compatibility level](https://learn.microsoft.com/sql/t-sql/statements/alter-database-transact-sql-compatibility-level) — compatibility level 170 is SQL Server 2025 (17.x) and Azure SQL only
- [Managing migrations](https://learn.microsoft.com/ef/core/managing-schemas/migrations/managing#checking-for-pending-model-changes) — `has-pending-model-changes` and why `Migrate`/`MigrateAsync` throw on a pending model change since EF Core 9
- [Migrate to Microsoft.Data.SqlClient 7.0](https://learn.microsoft.com/sql/connect/ado-net/sql/azure-active-directory-authentication#migrate-to-microsoftdatasqlclient-70) — the Entra ID authentication providers this decision's SqlClient 6.1 still bundles, and what moves to `Microsoft.Data.SqlClient.Extensions.Azure` in 7.0
- [`Directory.Packages.props`](../../weather-agent/Andes.Agents/Directory.Packages.props) — the pinned EF Core and SqlClient versions this ADR justifies
- [Architecture overview](../architecture/overview.md#the-corepolicy-table) — the schema this decision produces
