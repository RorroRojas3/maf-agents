# Runbook

Operator and local-development procedures for the weather agent. For what each configuration key means, see [Configuration](configuration.md); for the request flow these steps exercise, see [Architecture overview](../architecture/overview.md) and [Hosting and protocols](../agent/hosting-and-protocols.md).

## Run the API locally

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://www.docker.com/), for the Cosmos DB emulator and for SQL Server 2025 — the `mcr.microsoft.com/mssql/server:2025-latest` image is **x64 only**, so an Arm64 host needs emulation
- A Microsoft Azure AI Foundry resource with a `gpt-5.6-luna`-class Responses API deployment, reachable at its `/openai/v1/` route, and an API key (an additional Chat Completions deployment for `MicrosoftFoundry:*` is optional and only needed if you're exercising that client)
- An Entra ID app registration for the API (only if you're testing real authentication rather than `AllowAnyAuthenticatedCaller`)

### Start the Cosmos DB emulator

The Linux-based (vNext) emulator only supports Gateway connectivity mode — which is why `appsettings.Development.json` sets `CosmosDb:UseGatewayMode: true` — and the .NET SDK requires HTTPS against it:

```bash
docker pull mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:vnext-latest

docker run --detach --name andes-cosmos \
  --publish 8081:8081 --publish 8080:8080 --publish 1234:1234 \
  mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:vnext-latest --protocol https
```

Wait for the readiness probe before connecting:

```bash
curl -sf http://localhost:8080/ready
```

The emulator uses the well-known account key regardless of environment. Trust its self-signed certificate once per machine (Windows PowerShell):

```powershell
curl.exe -k https://localhost:8081/_explorer/emulator.pem -o "$env:USERPROFILE\andes-cosmos-emulator.crt"
Import-Certificate -CertStoreLocation Cert:\CurrentUser\Root -FilePath "$env:USERPROFILE\andes-cosmos-emulator.crt"
```

`appsettings.Development.json` points `CosmosDb:AccountEndpoint` at `http://localhost:8081`; override it to `https://localhost:8081` alongside the key in user-secrets (next step) once the certificate is trusted.

### Start SQL Server 2025

```bash
docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=<password>" -p 1433:1433 --name andes-sql -d mcr.microsoft.com/mssql/server:2025-latest
```

Wait for the server to accept connections before running anything against it:

```bash
docker logs andes-sql --follow
# ... SQL Server is now ready for client connections. This is an informational message; no user action is required.
```

`sqlcmd` lives inside the image at `/opt/mssql-tools18/bin/sqlcmd` and needs `-C` to trust the container's self-signed certificate:

```bash
docker exec andes-sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "<password>" -C -Q "SELECT @@VERSION"
```

From Git Bash on Windows, prefix that `docker exec` with `MSYS_NO_PATHCONV=1` — otherwise Git Bash rewrites the leading `/` of `/opt/mssql-tools18/...` into a Windows path before Docker ever sees it.

### Configure user-secrets

From `weather-agent/Andes.Agents/Andes.Agents.Api/`:

```bash
dotnet user-secrets set "AzureOpenAI:Endpoint" "https://<your-foundry-resource>.services.ai.azure.com/openai/v1/"
dotnet user-secrets set "AzureOpenAI:ApiKey" "<your-api-key>"
dotnet user-secrets set "CosmosDb:AccountEndpoint" "https://localhost:8081"
dotnet user-secrets set "CosmosDb:Key" "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw=="
```

`MicrosoftFoundry:Endpoint`/`MicrosoftFoundry:ApiKey`/`MicrosoftFoundry:Model` are optional — set them the same way if you need the Chat Completions client; leaving `MicrosoftFoundry:Endpoint` unset skips that registration entirely.

```bash
dotnet user-secrets set "SqlDb:ConnectionString" "Server=localhost,1433;Database=AndesAgents;User Id=sa;Password=<password>;TrustServerCertificate=true"
dotnet user-secrets set "SqlDb:ApplyMigrationsOnStartup" "true"
```

`appsettings.Development.json` no longer exists, so `ApplyMigrationsOnStartup` — normally a Development-only default — is set as a user-secret like everything else here; without it, the database is never created or migrated on `dotnet run`, and every request against it fails.

Add the `AzureAd` values too if you're testing against a real Entra ID token rather than relying on the Development-only `AllowAnyAuthenticatedCaller`:

```bash
dotnet user-secrets set "AzureAd:TenantId" "<tenant-id>"
dotnet user-secrets set "AzureAd:ClientId" "<api-app-registration-id>"
```

Add `ApiDocs:ClientId`/`ApiDocs:Scopes` too if you want Scalar to sign callers in through Entra ID instead of pasting a bearer token:

```bash
dotnet user-secrets set "ApiDocs:ClientId" "<api-client-id>"
dotnet user-secrets set "ApiDocs:Scopes" "api://<api-client-id>/access_as_user"
```

On the app registration named by `ApiDocs:ClientId`, under **Authentication**, add a **Single-page application** platform — not Web — with the exact redirect URIs, trailing slash included:

- `https://localhost:7237/scalar/`
- `http://localhost:5072/scalar/`, for the http launch profile
- each deployed host's `https://<host>/scalar/`

The API must also expose the scope `ApiDocs:Scopes` names, in its fully qualified form. Two scope settings are easy to confuse: `AzureAd:Scopes` takes the **short** scope name (`access_as_user`), matched against the token's `scp` claim; `ApiDocs:Scopes` takes the **fully qualified** form (`api://<api-client-id>/access_as_user`). Neither should be `<client-id>/.default`, and `AzureAd:Audience` is normally left blank, which falls back to the client id.

### Run

```bash
dotnet run --project weather-agent/Andes.Agents/Andes.Agents.Api
```

`CosmosDb:CreateResourcesOnStartup: true` (the Development default) creates the `andes-agents` database and both containers on this run if they don't already exist; `SqlDb:ApplyMigrationsOnStartup: true` (set above) creates the `AndesAgents` database and applies `CreatePolicyTable` the same way. `/scalar` opens automatically (`launchSettings.json`'s `launchUrl`).

## Smoke test

A minimal path through every major piece: discovery, both protocols, the REST resource, and the failure modes that are meant to happen.

### Discovery and health

```bash
curl -s https://localhost:7237/.well-known/agent-card.json | jq .
curl -s https://localhost:7237/health/live      # 200, no body needed to pass
curl -s https://localhost:7237/health/ready      # 200 once Cosmos DB and SQL Server both answer
```

The card is anonymous and lists both A2A interfaces under `weather/a2a`; `/health/ready` fails until both the Cosmos DB emulator and the SQL Server container are reachable — check `docker ps` for both if it doesn't return `200`.

### Get a token

With `AllowAnyAuthenticatedCaller: true` (Development default), any token issued for a tenant you're signed into works — for example, a Microsoft Graph token via the Azure CLI, since the guard only checks that the token validates and carries an `oid`:

```bash
TOKEN=$(az account get-access-token --resource https://graph.microsoft.com --query accessToken -o tsv)
```

Against a real `AzureAd:Scopes`/`AppPermissions` configuration, request a token for this API's own audience instead (`api://<AzureAd:ClientId>`).

### AG-UI turn

```bash
curl -N https://localhost:7237/weather/ui \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -H "Accept: text/event-stream" \
  -d '{
    "messages": [
      { "role": "user", "content": "What'\''s the weather in Seattle right now?" }
    ]
  }'
```

Expect an SSE stream: `RUN_STARTED` (carries `threadId` and `runId`) → tool-call events → `TEXT_MESSAGE_CONTENT` deltas → `RUN_FINISHED`. Capture `threadId` from the first event, then continue the conversation with it:

```bash
curl -N https://localhost:7237/weather/ui \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -H "Accept: text/event-stream" \
  -d '{
    "threadId": "<threadId from the first response>",
    "messages": [
      { "role": "user", "content": "And the day after?" }
    ]
  }'
```

The second answer should use the place resolved in the first turn without calling `search_location` again — the surest sign the Cosmos-backed history round-tripped, including the model's own reasoning continuity across turns despite `store:false`.

### A2A turn (JSON-RPC)

```bash
curl -s https://localhost:7237/weather/a2a \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": "1",
    "method": "message/send",
    "params": {
      "message": {
        "kind": "message",
        "role": "user",
        "messageId": null,
        "parts": [ { "kind": "text", "text": "Will it rain in London this weekend?" } ]
      }
    }
  }' | jq .
```

The response's `result.contextId` is a fresh session id (Cosmos `sessionId`); reuse it in `params.message.contextId` on a follow-up request to continue the same conversation. Omitting `contextId` always starts a new one.

### The `api/conversations` resource

```bash
curl -s https://localhost:7237/api/conversations -H "Authorization: Bearer $TOKEN" | jq .
curl -s https://localhost:7237/api/conversations/<sessionId> -H "Authorization: Bearer $TOKEN" | jq .
curl -s https://localhost:7237/api/conversations/<sessionId>/messages -H "Authorization: Bearer $TOKEN" | jq .
curl -s -o /dev/null -w '%{http_code}\n' -X DELETE https://localhost:7237/api/conversations/<sessionId> -H "Authorization: Bearer $TOKEN"
curl -s -o /dev/null -w '%{http_code}\n' -X DELETE https://localhost:7237/api/conversations/<sessionId> -H "Authorization: Bearer $TOKEN"
```

Expect both sessions from the two turns above, newest first, each with a title, a message count, and non-zero usage; messages in ascending, contiguous `sequence`; the first delete `204`, the second `404` (see [Sessions and history](../conversations/sessions-and-history.md#the-apiconversations-rest-resource)).

### Failure modes

```bash
# No token → 401 problem+json
curl -s -o /dev/null -w '%{http_code}\n' https://localhost:7237/api/conversations

# skip=-1 → 400 validation problem
curl -s -o /dev/null -w '%{http_code}\n' "https://localhost:7237/api/conversations?skip=-1" -H "Authorization: Bearer $TOKEN"

# A threadId containing '/' → 400 (SessionIdValidator)
curl -N https://localhost:7237/weather/ui -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{ "threadId": "has/slash", "messages": [ { "role": "user", "content": "hi" } ] }'
```

**409 `conversation-busy`:** fire two AG-UI or A2A turns against the *same* `threadId`/`contextId` at the same time (two terminal windows, same id, launched together). One succeeds; the other gets `409` with problem type `conversation-busy`.

**429:** send the 31st agent turn (any mix of A2A/AG-UI) within the same rolling minute; expect `429` with a `Retry-After` header and problem type `too-many-requests`. `api/conversations` is unmetered, so this only applies to `weather/a2a` and `weather/ui`.

**A tool exception mid-stream:** temporarily throw from inside `WeatherToolProvider`, start a turn, and confirm the AG-UI stream ends with a `RUN_ERROR` event (A2A: a failed task status) rather than the connection just dropping. If it doesn't, that's a gap in `UsageRecordingAgent`'s exception handling to close before shipping, not something to route around downstream.

### Verify in Cosmos DB

Open the Data Explorer at `http://localhost:1234` (or your target account's Data Explorer) and confirm, under the caller's `oid`:

- `sessions`: one document per conversation exercised above, `_etag` different after the second turn than after the first.
- `messages`: one document per message, `sequence` contiguous from `1`, `id` shaped `{sessionId}:{sequence:D8}`.

### Verify telemetry

With `Telemetry:ConnectionString` set, run a turn and check Application Insights for the nested span shape and metric documented in [Hosting and protocols](../agent/hosting-and-protocols.md#telemetry) — `invoke_agent weather-agent` → `chat rrp-gpt-5.6-luna` → `execute_tool get_current_weather`, and a `gen_ai.client.token.usage` data point.

### Scalar

Open `https://localhost:7237/scalar`; every route in `api/conversations` should be listed with its `401`/`404`/validation-problem responses documented.

- **`ApiDocs:ClientId` blank (default):** the `Bearer` scheme is preselected — paste a token from [Get a token](#get-a-token) into the Authorization modal.
- **`ApiDocs:ClientId` set:** the `EntraId` scheme is preselected instead, with `ApiDocs:Scopes` pre-checked. Click **Authorize** and sign in; the popup closes, Scalar holds an access token, and `GET /api/conversations` sent from Scalar returns `200`. With `Telemetry:ConnectionString` set, the popup's `GET /scalar/` request in Application Insights carries no `code` in its URL, since Scalar requests `response_mode=fragment`.

## Migrations

`PolicyDbContext`'s migrations are generated with `dotnet-ef`, pinned exactly in the repo-root `dotnet-tools.json` to match the EF packages — a `dotnet-ef` already on `PATH` may be an older version with different behavior:

```bash
dotnet tool restore
```

Every command below names `Andes.Agents.Repository` as both `--project` and `--startup-project` — the class library builds its own design-time context through `PolicyDbContextDesignTimeFactory` — and `--context PolicyDbContext`, in case Repository ever holds a second context. None of these commands boot the Api host, so no Foundry, Cosmos DB, or `AzureAd` configuration is needed to run them.

Add a migration after changing the model:

```bash
dotnet tool run dotnet-ef migrations add <VerbSubject> \
  --project weather-agent/Andes.Agents/Andes.Agents.Repository \
  --startup-project weather-agent/Andes.Agents/Andes.Agents.Repository \
  --context PolicyDbContext --output-dir Sql/Migrations
```

`migrations add` never opens a connection, so the design-time factory falls back to a localhost placeholder connection string unless one is supplied. Commands that do connect — `database update`, a bundle run — take it either as `--connection` or as the `SqlDb__ConnectionString` environment variable, which the factory reads first:

```bash
SqlDb__ConnectionString="Server=localhost,1433;Database=AndesAgents;User Id=sa;Password=<password>;TrustServerCertificate=true" \
  dotnet tool run dotnet-ef database update \
  --project weather-agent/Andes.Agents/Andes.Agents.Repository \
  --startup-project weather-agent/Andes.Agents/Andes.Agents.Repository \
  --context PolicyDbContext
```

Before committing a migration, confirm the model and the migration still agree:

```bash
dotnet tool run dotnet-ef migrations has-pending-model-changes \
  --project weather-agent/Andes.Agents/Andes.Agents.Repository \
  --startup-project weather-agent/Andes.Agents/Andes.Agents.Repository \
  --context PolicyDbContext
```

Production never calls `Database.MigrateAsync` — `SqlDb:ApplyMigrationsOnStartup` is development only (see [Provisioning the policy database](#provisioning-the-policy-database)). Its pipeline applies either an idempotent script:

```bash
dotnet tool run dotnet-ef migrations script --idempotent \
  --project weather-agent/Andes.Agents/Andes.Agents.Repository \
  --startup-project weather-agent/Andes.Agents/Andes.Agents.Repository \
  --context PolicyDbContext -o policy-migrations.sql
```

or a self-contained migrations bundle, for a pipeline that would rather ship one executable than carry the .NET SDK:

```bash
dotnet tool run dotnet-ef migrations bundle \
  --project weather-agent/Andes.Agents/Andes.Agents.Repository \
  --startup-project weather-agent/Andes.Agents/Andes.Agents.Repository \
  --context PolicyDbContext
```

**Never regenerate a migration to absorb a CLR rename** — renaming a mapped property or the migration class itself. Edit the type-name strings the generated migration and its designer file already contain, then rerun `migrations has-pending-model-changes` to confirm the model and the migration still match. `.editorconfig` marks everything under `Sql/Migrations/` as generated code precisely so `dotnet format` never rewrites it out from under a hand edit like this.

## Provisioning Cosmos DB with IaC

Outside Development, `CosmosDb:CreateResourcesOnStartup` stays `false` — the identity the app runs as has data-plane RBAC only, which cannot create a database or container — so provisioning is infrastructure as code. The definition below matches `CosmosResourceProvisioner` exactly: hierarchical partition key `[/userId, /sessionId]`, TTL enabled with no default, and the one large field of each container excluded from indexing.

```bicep
param cosmosAccountName string
param location string = resourceGroup().location

resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2024-11-15' existing = {
  name: cosmosAccountName
}

resource database 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2024-11-15' = {
  parent: cosmosAccount
  name: 'andes-agents'
  properties: {
    resource: { id: 'andes-agents' }
  }
}

resource sessions 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-11-15' = {
  parent: database
  name: 'sessions'
  properties: {
    resource: {
      id: 'sessions'
      partitionKey: {
        paths: [ '/userId', '/sessionId' ]
        kind: 'MultiHash'
        version: 2
      }
      defaultTtl: -1
      indexingPolicy: {
        indexingMode: 'consistent'
        includedPaths: [ { path: '/*' } ]
        excludedPaths: [ { path: '/state/*' } ]
      }
    }
  }
}

resource messages 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-11-15' = {
  parent: database
  name: 'messages'
  properties: {
    resource: {
      id: 'messages'
      partitionKey: {
        paths: [ '/userId', '/sessionId' ]
        kind: 'MultiHash'
        version: 2
      }
      defaultTtl: -1
      indexingPolicy: {
        indexingMode: 'consistent'
        includedPaths: [ { path: '/*' } ]
        excludedPaths: [ { path: '/message/*' } ]
      }
    }
  }
}
```

Grant the application's managed identity a Cosmos DB **data-plane** role assignment (e.g. *Cosmos DB Built-in Data Contributor*) on the account — the control-plane role that could create containers is deliberately not what this app runs as.

## Provisioning the policy database

The same split as Cosmos DB above applies here, one layer down: outside Development, the identity the app runs as holds no DDL rights, so schema changes are a pipeline step, not something `dotnet run` does. The pipeline applies one of the two artifacts from [Migrations](#migrations) — the idempotent script through whatever SQL execution step it already has, or a migrations bundle where it would rather ship one self-contained executable than carry the EF tooling — using an identity that *does* hold DDL rights on the target database.

The application itself then connects as a narrower principal: a SQL login, or on Azure SQL a contained database user, holding only data rights on the `Core` schema:

```sql
CREATE USER [andes-agents-app] FOR LOGIN [andes-agents-app]; -- or FROM EXTERNAL PROVIDER on Azure SQL, for a managed identity
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::Core TO [andes-agents-app];
```

On Azure SQL, that principal is reached with Microsoft Entra ID rather than a SQL login: `Authentication=Active Directory Managed Identity;User Id=<client-id>` in `SqlDb:ConnectionString`. `Microsoft.Data.SqlClient` 6.1 — the version EF Core 10 resolves — still bundles the Entra ID authentication providers, so no extra package is needed for that connection-string keyword to work (see [ADR-0002](../adr/0002-sql-server-policy-store.md)); moving to SqlClient 7 would change that.

The target database's compatibility level must be **170** (SQL Server 2025 or Azure SQL only) — `SqlPersistenceConfiguration.ConfigureSqlServer` sets it explicitly on every connection, migrations included, because EF Core otherwise assumes 150 and avoids newer T-SQL. A database created by the idempotent script or a bundle already carries this, since both are generated from the same configuration the app runs with.

## Deployment notes

- **Ingress and forwarded headers.** `AddAndesForwardedHeaders` only trusts `X-Forwarded-For`/`X-Forwarded-Proto` from whatever can reach the container directly (`KnownIPNetworks`/`KnownProxies` are both cleared, since the ingress address isn't knowable in advance). This is safe exactly when the container is unreachable except through that ingress — confirm network rules enforce that before deploying, since a directly reachable container would let a caller forge those headers.
- **Single instance for A2A.** A2A's task store (`tasks/get`/`tasks/cancel`) is in-memory and per-process. Until a Cosmos DB–backed `ITaskStore` exists, run one instance, or route by sticky session if you must scale out — a task started on instance A is invisible to a `tasks/get` that lands on instance B. Ordinary agent turns (`message/send`/`message/stream`) and AG-UI aren't affected, since conversation state for those lives in Cosmos DB, not in the task store.
- **`/health/ready` is unauthenticated and unmetered**, and performs a real Cosmos DB container read on every probe. Keep probe intervals reasonable (the platform default is normally fine) — this is a known cost, not a bug to fix by adding caching that would make readiness lag reality.
- **Every `/health/ready` call now also performs a full SQL Server login.** The SQL check deliberately uses an unpooled connection so it reports the server as it is *right now*, not a pooled session the driver kept alive — but that means each probe costs a real login, on the same anonymous, unmetered endpoint as the Cosmos DB read above. Coalescing concurrent probes, or restricting who can reach `/health/ready` at all, is the open follow-up; neither is done yet.

## Troubleshooting

**A model call 404s instead of returning a completion.**
`AzureOpenAI:Endpoint` (or `MicrosoftFoundry:Endpoint`, if you're exercising that client) doesn't end in `/openai/v1/`. The OpenAI SDK routes requests against whatever `Endpoint` says, so a wrong path reads as "model not found" rather than "wrong endpoint" — there's no clearer error to see, which is exactly why each provider validates its `/openai/v1/` suffix at startup instead of leaving it to the first request. If startup passed but calls still 404, double-check the value doesn't have a trailing path segment beyond `/openai/v1/` (a deployment name, an api-version query string) — the v1 route wants neither.

**A turn returns `409` with problem type `conversation-busy`.**
Expected, not a bug: two turns raced on the same `contextId`/`threadId`. The client that lost should treat this like any other transient failure and retry the turn (a *new* turn, not a resend of the exact same request) once the winner's turn has finished — concurrent turns on one session are unsupported by design (see [Sessions and history](../conversations/sessions-and-history.md#conflict-and-heal-semantics)). If this fires on requests that were never actually concurrent, check for a client-side retry-with-the-same-idempotency-window bug sending the same turn twice.

**The application won't start, with a message naming `AzureAd:Scopes`, `AzureAd:AppPermissions`, or `AllowAnyAuthenticatedCaller`.**
The startup guard in `AddAndesAuthentication` refused to come up with no way to tell an authenticated-but-unauthorized caller from an authorized one. Set `AzureAd:Scopes` and/or `AzureAd:AppPermissions` to the values your app registration expects callers to present, or set `AzureAd:AllowAnyAuthenticatedCaller: true` if — and only if — accepting any token issued for this API's audience is actually the intended policy for that environment (Development's default; not a safe default anywhere else).

**The application won't start, with a message naming `AzureAd:Authority` or `AzureAd:Instance`/`AzureAd:TenantId`.**
The same startup guard couldn't resolve a token-issuing authority. Set `AzureAd:Authority` to the full authority URL, or set both `AzureAd:Instance` and `AzureAd:TenantId` — either form satisfies `Microsoft.Identity.Web`.

**The application won't start, with a message naming `RequestLogging:ExcludedPaths`.**
An entry in that array doesn't start with `/`. `AddAndesRequestLogging` rejects that at startup rather than letting `PathString` throw on the first request that arrives — fix the entry to include its leading slash (e.g. `/health`, not `health`).

**The application won't start, with a message naming `ApiDocs:Scopes`.**
`ApiDocs:ClientId` is set but `ApiDocs:Scopes` is blank — Scalar would have a client to sign in with but no scopes to request. Set `ApiDocs:Scopes` to the fully qualified scope(s) the API exposes, or blank `ApiDocs:ClientId` to fall back to bearer-token-only.

**Signing in to Scalar fails with `AADSTS50011` (redirect URI mismatch).**
Register the exact `/scalar/` URI for the host and scheme you're using — trailing slash included — on the `ApiDocs:ClientId` app registration (see [Configure user-secrets](#configure-user-secrets)).

**Signing in to Scalar fails with `AADSTS9002326` (redirect URI under the wrong platform).**
The redirect URI is registered under **Web** instead of **Single-page application**. Scalar redeems the code from the browser, and Entra ID allows that cross-origin redemption only for Single-page application redirect URIs — move it there.

**The application won't start, with a message naming `SqlDb:ConnectionString`.**
`SqlDbOptionsValidator` rejected the value — either blank, or not something `SqlConnectionStringBuilder` can parse into a connection string that names a database. The message never echoes the value itself, since a connection string can carry a password; check what's actually in `SqlDb:ConnectionString` (or the `SqlDb__ConnectionString` environment variable) rather than trusting the error text to show it.

**`/health/ready` reports `sql` Unhealthy with "SQL Server is unreachable or the database does not exist."**
The check opened its own connection and ran `SELECT 1`; a stopped container, wrong credentials, or a database that hasn't been created yet all surface this way. Confirm the container is running (`docker ps`), the credentials in `SqlDb:ConnectionString` are right, and the database has been migrated (see [Migrations](#migrations)).

**`/health/ready` reports `sql` Unhealthy with "A timeout occurred while running check."**
The probe didn't get an answer inside its 5-second budget (`HealthRegistration`'s `sql` timeout). A container still starting up, or a host that's unreachable rather than actively refusing the connection, both look like this instead of the message above — the distinction is whether SQL Server ever got a chance to answer.

**`dotnet run` (with `SqlDb:ApplyMigrationsOnStartup: true`) or `dotnet ef database update` fails, naming a pending model change.**
Since EF Core 9, `Database.MigrateAsync` (and the CLI's `database update`) throw rather than apply migrations when the mapped model has changed since the last migration was generated — the same condition `migrations has-pending-model-changes` reports. Add the missing migration (see [Migrations](#migrations)) before running either one again; this is EF Core refusing to leave the database out of sync with the code, not a bug in `SqlSchemaMigrator`.

**Tracking down a failure a caller reported.**
Every problem+json response carries `traceId` (the W3C trace id, and the Application Insights operation id — search for it there) and `requestId` (the connection-scoped id that same request's own log lines carry, from `RequestLoggingMiddleware` and `GlobalExceptionHandler` alike). Ask the caller for `traceId` first; it's the one that survives past this process.
