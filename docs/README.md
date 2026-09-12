# Andes Agents documentation

Engineer-facing reference for `weather-agent/Andes.Agents/` — an ASP.NET Core host for a weather agent built on [Microsoft Agent Framework](https://learn.microsoft.com/agent-framework/). Start with the architecture overview, then go to the doc for the area you're changing.

## Architecture

- [Overview](architecture/overview.md) — solution layout, project layering, how a request moves through the system, and the SQL Server policy database's schema and startup order.

## Conversations

- [Sessions and history](conversations/sessions-and-history.md) — the Cosmos DB data model, the session store and history provider, conflict handling, and the `api/conversations` REST resource.

## Agent

- [Hosting and protocols](agent/hosting-and-protocols.md) — the A2A and AG-UI endpoints, the agent card, the chat-client-to-agent pipeline, tools, the prompt, and telemetry.

## Operations

- [Configuration](operations/configuration.md) — every configuration key, its default, and how it's supplied (`appsettings.json`, Key Vault, user-secrets, environment variables).
- [Runbook](operations/runbook.md) — running the API locally (including SQL Server 2025), smoke-testing the agent end to end, generating and applying EF Core migrations, provisioning Cosmos DB and the policy database in a target environment, and troubleshooting.

## Decisions

- [ADR-0001: Pin the preview Agent Framework hosting packages](adr/0001-preview-hosting-packages.md)
- [ADR-0002: SQL Server 2025 as a second, relational store for policies](adr/0002-sql-server-policy-store.md)
- [ADR-0003: A database-owned agent catalog and a background session-usage projection](adr/0003-database-owned-agent-catalog-and-session-usage.md)
