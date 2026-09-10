# Andes Agents documentation

Engineer-facing reference for `weather-agent/Andes.Agents/` — an ASP.NET Core host for a weather agent built on [Microsoft Agent Framework](https://learn.microsoft.com/agent-framework/). Start with the architecture overview, then go to the doc for the area you're changing.

## Architecture

- [Overview](architecture/overview.md) — solution layout, project layering, and how a request moves through the system.

## Conversations

- [Sessions and history](conversations/sessions-and-history.md) — the Cosmos DB data model, the session store and history provider, conflict handling, and the `api/conversations` REST resource.

## Agent

- [Hosting and protocols](agent/hosting-and-protocols.md) — the A2A and AG-UI endpoints, the agent card, the chat-client-to-agent pipeline, tools, the prompt, and telemetry.

## Operations

- [Configuration](operations/configuration.md) — every configuration key, its default, and how it's supplied (`appsettings.json`, Key Vault, user-secrets, environment variables).
- [Runbook](operations/runbook.md) — running the API locally, smoke-testing the agent end to end, provisioning Cosmos DB in a target environment, and troubleshooting.

## Decisions

- [ADR-0001: Pin the preview Agent Framework hosting packages](adr/0001-preview-hosting-packages.md)
