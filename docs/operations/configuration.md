# Configuration

## Overview

Configuration follows the standard ASP.NET Core layering: `appsettings.json` (checked in, every key present, secrets blank) → `appsettings.Development.json` (Development-only overrides) → Azure Key Vault (when enabled) → environment variables → `dotnet user-secrets` in Development. Every section below binds to a strongly typed `Options` class, validated with `ValidateDataAnnotations().ValidateOnStart()` (or an inline `.Validate(...)`), so a missing or malformed value fails **at startup**, not on the first request that needs it.

Local secrets go in `dotnet user-secrets` (the Api project's `UserSecretsId` is already set) or environment variables — never in a tracked file. See the [runbook](runbook.md) for the exact commands to seed a local run.

## `AzureAd` — authentication

Bound by `Api/Options/AzureAdOptions.cs`, then handed to `Microsoft.Identity.Web`'s `AddMicrosoftIdentityWebApi` for token validation; the app's own authorization policy and startup guards read the same options.

| Key | Meaning | Default | Required |
|---|---|---|---|
| `AzureAd:Instance` | Entra ID cloud instance. Paired with `TenantId` to build the authority. | `https://login.microsoftonline.com/` | See below |
| `AzureAd:TenantId` | Tenant that issues tokens for this API. Paired with `Instance`. | _(blank)_ | See below |
| `AzureAd:Authority` | Full authority URL, accepted in place of `Instance` + `TenantId` — Microsoft.Identity.Web supports either form. | _(blank)_ | See below |
| `AzureAd:ClientId` | This API's app registration id (the token audience). | _(blank)_ | Yes |
| `AzureAd:Audience` | Expected `aud` claim, when it differs from `ClientId`. | _(blank)_ | No |
| `AzureAd:Scopes` | Space-separated delegated scopes a user token must carry one of. | _(blank)_ | See below |
| `AzureAd:AppPermissions` | Space-separated app roles a daemon (client-credentials) token must carry one of. | _(blank)_ | See below |
| `AzureAd:AllowAnyAuthenticatedCaller` | Skip the scope/app-permission check — any token issued for this API's audience is accepted. | `false` | No |

**Startup guards:** two independent checks run before the app accepts traffic.

- An authority must be resolvable: either `AzureAd:Authority`, or both `AzureAd:Instance` and `AzureAd:TenantId`.
- At least one of `Scopes`, `AppPermissions`, or `AllowAnyAuthenticatedCaller: true` must be set. A validated token alone only proves *some* app in the tenant obtained one for this audience — a scope or app role is what proves the caller was actually granted access; `AllowAnyAuthenticatedCaller` is an explicit, named opt-out of that check, not a silent default. `appsettings.Development.json` sets it to `true` so a local run doesn't need an app registration with scopes configured. Both a scope and an app permission may be configured together, so the same endpoints serve delegated (user, `scp` claim) and daemon A2A (app-only, `roles` claim) callers.

Either guard failing throws on startup, naming the section it failed in.

## `KeyVault`

| Key | Meaning | Default | Required |
|---|---|---|---|
| `KeyVault:Enabled` | Load configuration from Azure Key Vault. | `false` | No |
| `KeyVault:VaultUri` | Vault URI. | _(blank)_ | Yes, when `Enabled` |
| `KeyVault:ReloadIntervalMinutes` | How often secrets are re-read; `0` disables reloading. | `30` | No |

When enabled, Key Vault is added to configuration **before** the DI container is built (`builder.AddAndesKeyVault()` is the first call in `Program.cs`), authenticating with the same shared Azure credential the rest of the app uses (see `Azure` below). Secret **names** use `--` where configuration keys use `:`, because that's the separator the Key Vault configuration provider maps:

| Secret name in Key Vault | Configuration key it becomes |
|---|---|
| `AzureOpenAI--ApiKey` | `AzureOpenAI:ApiKey` |
| `CosmosDb--Key` | `CosmosDb:Key` |
| `Telemetry--ConnectionString` | `Telemetry:ConnectionString` |

Any other secret-bearing key in the tables below follows the same pattern.

## `Azure` — shared credential

| Key | Meaning | Default | Required |
|---|---|---|---|
| `Azure:ManagedIdentityClientId` | Client id of a **user-assigned** managed identity. | _(blank)_ | No |

Backs `DefaultAzureCredential`, used for Key Vault, Cosmos DB (when `CosmosDb:Key` is blank), and Azure Monitor (when `Telemetry:UseManagedIdentity` is `true`). Blank selects the system-assigned identity in Azure, or falls through `DefaultAzureCredential`'s local-development chain (Visual Studio, Azure CLI, …) outside Azure.

## `AzureOpenAI` and `MicrosoftFoundry` — model providers

Two keyed `IChatClient` registrations reach the same Microsoft Azure AI Foundry resource through its OpenAI-compatible `/openai/v1/` route, differing only in which deployment they call. Both option classes derive from `Api/Options/OpenAIEndpointOptions.cs` (`Endpoint`, `ApiKey`, `Model`), and both endpoints must end in `/openai/v1/` — any other path 404s on the first model call rather than failing at startup with a clear message, which is exactly why each is validated at startup instead.

### `AzureOpenAI` — required

The Responses API deployment that drives the agent, registered as the keyed client `azure-openai` (`ChatClientKeys.AzureOpenAI`).

| Key | Meaning | Default | Required |
|---|---|---|---|
| `AzureOpenAI:Endpoint` | Foundry resource endpoint. **Must end in `/openai/v1/`**. | _(blank)_ | Yes |
| `AzureOpenAI:ApiKey` | API key of the resource. | _(blank)_ | Yes |
| `AzureOpenAI:Model` | Responses API deployment name that drives the agent. | `rrp-gpt-5.6-luna` | Yes |

Stored output stays disabled on every call (`store:false`), so Cosmos DB remains the only place conversation state lives. Key Vault secret name: `AzureOpenAI--ApiKey`. See [Hosting and protocols](../agent/hosting-and-protocols.md#agent-pipeline) for how this client is built and wrapped.

### `MicrosoftFoundry` — optional

A Chat Completions deployment on the same resource and route, registered as the keyed client `microsoft-foundry` (`ChatClientKeys.MicrosoftFoundry`). Nothing in the solution consumes it yet — it's infrastructure for a future service that needs a plain model call rather than an agent turn.

| Key | Meaning | Default | Required |
|---|---|---|---|
| `MicrosoftFoundry:Endpoint` | Chat Completions deployment endpoint, usually the same resource and route as `AzureOpenAI:Endpoint`. A blank value registers no client and does not fail startup. | _(blank)_ | No |
| `MicrosoftFoundry:ApiKey` | API key of the resource. | _(blank)_ | Only once `Endpoint` is set |
| `MicrosoftFoundry:Model` | Chat Completions deployment name. | _(blank)_ | Only once `Endpoint` is set |

Naming `MicrosoftFoundry:Endpoint` opts the whole section into the same startup validation `AzureOpenAI` gets — the `/openai/v1/` suffix, and `ApiKey`/`Model` both required. Key Vault secret name: `MicrosoftFoundry--ApiKey`.

## `CosmosDb` — persistence

| Key | Meaning | Default | Required |
|---|---|---|---|
| `CosmosDb:AccountEndpoint` | Account endpoint URL. | _(blank)_ | Yes |
| `CosmosDb:Key` | Account key; blank uses the shared Azure credential (RBAC data-plane access) instead. | _(blank)_ | No |
| `CosmosDb:DatabaseId` | Database id. | `andes-agents` | Yes |
| `CosmosDb:SessionsContainerId` | Container id for session documents. | `sessions` | Yes |
| `CosmosDb:MessagesContainerId` | Container id for message documents. | `messages` | Yes |
| `CosmosDb:UseGatewayMode` | Use Gateway connectivity instead of Direct. The local emulator supports Gateway only. | `false` | No |
| `CosmosDb:CreateResourcesOnStartup` | Create the database and both containers on startup if they don't exist. **Development only** — data-plane RBAC in a real environment cannot create containers; production provisions the same definition through infrastructure as code (see the [runbook](runbook.md#provisioning-cosmos-db-with-iac)). | `false` | No |

Key Vault secret name: `CosmosDb--Key`. See [Sessions and history](../conversations/sessions-and-history.md#data-model) for the container definitions this produces.

## `Sessions`

| Key | Meaning | Default | Required |
|---|---|---|---|
| `Sessions:MaxHistoryMessages` | How many of the most recent messages are replayed to the model at the start of each turn. | `50` | No |

## `AgentCard`

| Key | Meaning | Default | Required |
|---|---|---|---|
| `AgentCard:PublicBaseUrl` | Absolute URL clients reach this host at; the A2A agent card's interface URLs are built on it. | `https://localhost:7237` | Yes |
| `AgentCard:Version` | Version string the agent card advertises. | `1.0.0` | Yes |

## `Telemetry`

| Key | Meaning | Default | Required |
|---|---|---|---|
| `Telemetry:ConnectionString` | Application Insights connection string. Blank keeps telemetry in-process only (no export). | _(blank)_ | No |
| `Telemetry:UseManagedIdentity` | Authenticate ingestion with the shared Azure credential instead of the key embedded in the connection string. | `false` | No |
| `Telemetry:EnableSensitiveData` | Record prompts, completions, and tool arguments on spans. | `false` | No |

Key Vault secret name: `Telemetry--ConnectionString`. Prompt capture is off by default in **every** environment, Development included; opt in locally with `Telemetry:EnableSensitiveData: true` (or the environment variable `OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT=true` anywhere). See [Hosting and protocols](../agent/hosting-and-protocols.md#telemetry) for what gets recorded.

## `RequestLogging`

| Key | Meaning | Default | Required |
|---|---|---|---|
| `RequestLogging:Enabled` | Log one line per request: method, matched route, status code, elapsed milliseconds. | `true` | No |
| `RequestLogging:SlowRequestThresholdMilliseconds` | Elapsed time above which that line logs at Warning instead of Information. | `5000` | No |
| `RequestLogging:ExcludedPaths` | Path prefixes never logged, so probes don't dominate the sink. Each entry must start with `/`, or startup fails. | `["/health"]` | No |

The line never carries a request body, prompt, message text, query value, or the caller's `oid` — it names the request by its matched route pattern, not its literal path, so an identifier like a session id never reaches a log sink. See [Architecture overview](../architecture/overview.md#where-things-live) for `Middleware/` and `Observability/RequestDescriptor.cs`.

## `Cors`

| Key | Meaning | Default | Required |
|---|---|---|---|
| `Cors:AllowedOrigins` | List of allowed browser origins (scheme + host), e.g. `https://app.example.com`. An empty list allows no cross-origin calls at all. | `[]` | No |

`appsettings.Development.json` sets this to `["http://localhost:4200"]`. AG-UI is the protocol a browser client calls directly, so this is what gates it.

## `RateLimiting`

| Key | Meaning | Default | Required |
|---|---|---|---|
| `RateLimiting:PermitPerMinute` | Agent turns one caller (partitioned by `oid`, falling back to remote IP) may start per minute, over a sliding window. | `30` | No |

Applies to the A2A and AG-UI routes only (the `AgentTurns` policy) — `api/conversations` is unmetered. Exceeding it returns `429` with a `Retry-After` header and a `too-many-requests` problem.

## `ApiDocs`

| Key | Meaning | Default | Required |
|---|---|---|---|
| `ApiDocs:Enabled` | Serve `/openapi/v1.json` and the `/scalar` reference UI outside Development. | `false` | No |

The Development environment serves both regardless of this flag; `appsettings.Development.json` also sets it to `true` explicitly.

## Development overrides at a glance

`appsettings.Development.json` layers on top of `appsettings.json`:

```jsonc
{
  "AzureAd": { "AllowAnyAuthenticatedCaller": true },
  "CosmosDb": {
    "AccountEndpoint": "http://localhost:8081",
    "UseGatewayMode": true,
    "CreateResourcesOnStartup": true
  },
  "Cors": { "AllowedOrigins": ["http://localhost:4200"] },
  "ApiDocs": { "Enabled": true }
}
```

`Telemetry:EnableSensitiveData` is **not** part of this override — prompt capture stays off in Development like everywhere else; turn it on explicitly (user-secrets or the environment variable) if you need to inspect span content locally.

`AzureOpenAI:ApiKey`, `AzureOpenAI:Endpoint`, and `AzureAd:TenantId`/`ClientId` are still required and still not committed anywhere — supply them with `dotnet user-secrets` (see the [runbook](runbook.md#run-the-api-locally)).
