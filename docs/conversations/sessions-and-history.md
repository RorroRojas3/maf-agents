# Sessions and history

## Overview

Every turn of the weather agent needs somewhere durable to keep the conversation, because the model client is configured with `store:false` (see [Hosting and protocols](../agent/hosting-and-protocols.md)) — Cosmos DB is the *only* place a session's state and message history live. Two Cosmos DB containers hold it: `sessions` (one document per conversation) and `messages` (one document per chat message). A caller resumes a conversation by sending back the same continuation id — the A2A `contextId` or the AG-UI `threadId` — which becomes the Cosmos DB `id` and partition value.

A separate, much smaller pipeline also projects each session's cumulative token usage into a SQL Server reporting row — never into the conversation itself, and never on the request path above. See [Session usage reporting](#session-usage-reporting).

This page covers the document shapes, how the Microsoft Agent Framework session store and history provider read and write them, what happens when two turns race, the `api/conversations` REST resource that lets a caller browse and delete their own conversations, and how each session's usage is projected into SQL Server for reporting.

## Data model

Database `andes-agents`, both containers on the hierarchical partition key `[/userId, /sessionId]` — `userId` is the caller's Entra object id, `sessionId` is the continuation id. A hierarchical key means a query naming only `userId` (list a caller's sessions) is a partition-prefix scan across every one of their sessions, and a query naming both (a session's messages) targets exactly one logical partition — so a turn's transactional batch write always lands in a single partition.

### `sessions` container — `SessionDocument`

| Field | Type | Meaning |
|---|---|---|
| `id` | string | Equals `sessionId`. |
| `userId` | string | Entra object id of the owner; first partition key level. |
| `sessionId` | string | The A2A `contextId` / AG-UI `threadId`; second partition key level. |
| `agentId` | string | Id of the agent the session belongs to (`weather-agent`). |
| `title` | string? | The first user message, truncated to 80 characters; `null` until the first turn is stored. |
| `messageCount` | int | Number of messages stored for the session. |
| `usage` | `SessionUsage` | Cumulative `{ inputTokens, outputTokens, totalTokens }` across every turn. |
| `dateCreated` | DateTimeOffset | UTC time the session was created. |
| `dateModified` | DateTimeOffset | UTC time of the last save. |
| `lastMessageAt` | DateTimeOffset? | UTC time of the last stored message. |
| `state` | JSON | The Agent Framework's own serialized `AgentSession`; opaque to this application. Excluded from indexing (see below). |

This field was renamed from `dateUpdated`, to match `[Core].[Session].DateModified` below: a document written before the rename still carries `dateUpdated` on disk, so `SessionDocument.DateModified` reads as its default value until that session's next save overwrites the document with the new field name. `api/conversations` returns `dateModified` on `SessionDto` for the same reason — a breaking change for any existing client reading `dateUpdated`.

### `messages` container — `SessionMessageDocument`

| Field | Type | Meaning |
|---|---|---|
| `id` | string | `{sessionId}:{sequence:D8}` — deterministic, so a duplicate write is a Cosmos DB conflict, never a silent duplicate. |
| `userId` | string | First partition key level. |
| `sessionId` | string | Second partition key level. |
| `agentId` | string | Id of the agent that took part in the turn. |
| `sequence` | long | One-based position within the session; the only ordering key. |
| `role` | string | Chat role as the model client names it (`user`, `assistant`, `tool`, …). |
| `text` | string? | Plain-text projection for listing; `null` for tool calls and tool results. |
| `messageId` | string? | Provider-assigned message id, when the provider gave one. |
| `dateCreated` | DateTimeOffset | UTC time the message was stored. |
| `message` | JSON | The full `ChatMessage`, serialized with `AIJsonUtilities.DefaultOptions`. Excluded from indexing. |

Both `DateTimeOffset` fields are written as a fixed-width UTC string (`yyyy-MM-ddTHH:mm:ss.fffffffZ`, `UtcDateTimeOffsetJsonConverter`), so `ORDER BY c.dateCreated` is a valid lexicographic string sort — Cosmos DB has no native temporal ordering, and this is what makes `SELECT * FROM c ORDER BY c.dateCreated DESC` behave correctly.

### Indexing and TTL

Both containers enable TTL (`DefaultTimeToLive = -1`) with no default — nothing expires until a document sets its own `ttl`, but retention can be added later per document without recreating the container. The indexing policy excludes the one large opaque field each container carries: `/state/*` on `sessions`, `/message/*` on `messages`. Indexing them would cost write RUs on every save for a payload nothing ever queries by field.

## Session store and history provider

Two Service-layer classes implement Microsoft Agent Framework's session extensibility points, both singletons over `ISessionRepository`/`ISessionMessageRepository` — the store-agnostic contracts in `Andes.Agents.Repository/Sessions/`, currently implemented by `CosmosSessionRepository`/`CosmosSessionMessageRepository` in `Repository/Cosmos/Sessions/`. Neither Service type is named after a storage technology, by design: swapping the store behind the interfaces changes nothing here.

- **`PersistedAgentSessionStore`** (`: AgentSessionStore`) owns the *session document* — identity, title, counts, usage, and the serialized `AgentSession` blob.
- **`PersistedChatHistoryProvider`** (`: ChatHistoryProvider`) owns the *message documents* — replaying history before a turn and appending new messages after one.

They're registered with `.WithSessionStore(..., withIsolation: false)`. The framework's isolation wrapper composes `"{oid}::{contextId}"` into one opaque string; the repositories need the two halves separately for the hierarchical partition key, so `PersistedAgentSessionStore` reads `ICallerContext.UserId` itself, once, at lookup — and every downstream write keys off the `userId` bound into the session at that point, not off an ambient `HttpContext`. That's also why `SaveSessionAsync` can run without an HTTP request in scope.

### Lookup — `GetSessionAsync(agent, sessionId)`

1. `SessionIdValidator.EnsureValid(sessionId)` — rejects an id Cosmos DB couldn't store as a document id or partition value (see below) before any I/O happens.
2. Point-read `(userId, sessionId)` in the `sessions` container.
3. **Found:** `agent.DeserializeSessionAsync(document.State)` restores the `AgentSession`; the history state seeded into its state bag takes `MessageCount` as `Max(document.MessageCount, MaxSequenceAsync(...))` and records the document's current ETag.
4. **Not found:** `agent.CreateSessionAsync()` creates a fresh session; its history state starts unbound-turned-bound with `MessageCount` from `MaxSequenceAsync(...)` (see the heal case below) and no ETag.

Re-checking the maximum stored message sequence on every lookup, not just trusting the session document's `messageCount`, is what makes the crash case below self-healing without an operator step.

### Turn — `ProvideChatHistoryAsync` / `StoreChatHistoryAsync`

Before the model runs, `ProvideChatHistoryAsync` loads the session's most recent messages — `Sessions:MaxHistoryMessages` (default 50) — from Cosmos DB, deserializes each `ChatMessage`, and trims any leading tool-result messages so the replayed window never opens on a tool result whose call fell outside the window (a shape the model rejects).

After the model runs, `StoreChatHistoryAsync` numbers the turn's new messages `state.MessageCount + 1, + 2, …` and appends them to the `messages` container in one Cosmos DB **transactional batch** (chunked at 100 operations, the SDK's batch limit). The batch is scoped to a single partition by construction, so it's atomic: either every message of the turn lands, or none does.

### Save — `SaveSessionAsync`

The protocol handler calls this once the turn completes. It serializes the `AgentSession` and cumulative usage, then either:

- **Creates** the session document, if this is the session's first save (no ETag yet), or
- **Replaces** it with an `IfMatchEtag` precondition, if a document already exists.

## Conflict and heal semantics

Two failure modes are made visible instead of silently corrupting a session, both deliberate given concurrent turns on one session are unsupported by design:

- **A concurrent turn on the same session.** The losing writer's `AppendAsync` (message batch) or `CreateAsync`/`ReplaceAsync` (session document) hits a Cosmos DB 409 or 412. The repository translates that into `SessionConflictException`; the Service layer wraps it as `ConversationBusyException`, which the API maps to **HTTP 409, problem type `conversation-busy`**. The losing turn's messages are never written behind the winner's — the loser simply doesn't retry.
- **A crash between the message batch and the session save.** If the process dies after `StoreChatHistoryAsync` commits but before `SaveSessionAsync` runs, the session document's `messageCount` is now stale — lower than what's actually in the `messages` container. The next `GetSessionAsync` for that session re-derives the count from `MAX(c.sequence)` (one cheap scalar query per turn) rather than trusting the stale field, so the next turn continues numbering correctly instead of overwriting or skipping sequence numbers.

### Session id validation

A continuation id becomes a Cosmos DB document id and partition value, and — now that it's also `[Core].[Session].SessionId`, a `uniqueidentifier` — it has to parse as a GUID. `SessionIdValidator` accepts only the hyphenated (`D`, 36 characters) or plain (`N`, 32 characters) form and rejects everything else — padded, braced, or any other shape — before any I/O happens. Both hosts issue an `N` id when the client sends none, and a continuation reuses whatever form the client already has.

A rejection is `InvalidSessionIdException`, and how it reaches the caller depends on the protocol:

- **AG-UI and `api/conversations`** get the usual **HTTP 400, problem type `validation-error`**.
- **A2A** needs a translation step first: the A2A server turns any exception it doesn't recognize into a 500 (HTTP+JSON) or an internal JSON-RPC error, which would hide what's actually a caller mistake. `AgentsConfiguration` wraps the session store in a private `A2AErrorTranslatingSessionStore` that catches `InvalidSessionIdException` and rethrows it as `A2AException(A2AErrorCode.InvalidParams)`. The A2A server then answers **HTTP 400** on HTTP+JSON or **JSON-RPC error `-32602`** on JSON-RPC, and `GlobalExceptionHandler` maps that same `A2AException` shape to the ordinary `validation-error` problem — so a caller on either protocol sees the same kind of rejection.

## Session usage reporting

Independently of the conversation itself, `[Core].[Session]` (entity `SessionSummary`) keeps one SQL Server row per session — cumulative message and token counts and a running USD cost — for reporting that a document store doesn't answer well: aggregating across sessions, grouping by agent or model, filtering by period. See [Architecture overview](../architecture/overview.md#the-agent-catalog-and-session-usage-summaries) for the schema and [ADR-0003](../adr/0003-database-owned-agent-catalog-and-session-usage.md) for why this is a background projection rather than a write on the turn.

**Usage collection.** `UsageRecordingAgent` keeps two keys in the session's state bag: `andes.usage` (`SessionUsage` — input, output, total) and `andes.usage.details` (`SessionUsageDetails` — cached input tokens and reasoning tokens). The details live under their own key so a build that only knows `andes.usage` still round-trips `andes.usage.details` unchanged during a rollback, instead of dropping it. Cached tokens are already counted inside input tokens, and reasoning tokens inside output tokens, the way `Microsoft.Extensions.AI` 10.9.0 reports them.

**Enqueue on save.** Once `PersistedAgentSessionStore.SaveSessionAsync` has created or replaced the Cosmos DB session document — never before that write succeeds — it hands the session's state, usage, and details to `ISessionSummaryChannel.EnqueueTurn`. Enqueueing never blocks and never throws.

**Enqueue on delete.** `SessionService.DeleteAsync` (the `api/conversations` resource) calls `EnqueueDeletion` with the deleted document's own counts, after the Cosmos DB deletes succeed. `PersistedAgentSessionStore.DeleteSessionAsync` does the same, reading the document first — no host calls it today, but the store's contract requires it regardless.

**The channel.** Bounded at 10,000 items; a full channel, or a user or session id that doesn't parse as a GUID, drops the item with a warning that names no values.

**The writer.** `SessionSummaryProcessor`, a `BackgroundService`, drains the channel in order. For each item it reads the agent catalog cache (see [Architecture overview](../architecture/overview.md#the-agent-catalog-and-session-usage-summaries)) to find the item's agent's active model, then calls `ISessionSummaryRepository.UpsertAsync`.

**Merging a write (`SqlSessionSummaryRepository`).** Each attempt reads the row by `(UserId, SessionId, DateCreated)`:

- **Stale write.** Any count lower than what's already stored belongs to an older write and changes nothing.
- **Growth.** The delta is priced with `TokenPrices.CostOf` — `((input − cached) × input price + cached × cached-input price + output × output price) ÷ 1,000,000`, cached clamped to `[0, input]`, rounded to 9 decimals — and added to `EstimatedCost`. A turn written late is priced at the model's price when it's written, not when the turn ran.
- **Deletion.** Merges the document's own counts (its cached and reasoning tokens are left as already stored, since a deletion doesn't carry them) and stamps `DateDeleted` if it's still null; the stamp is never cleared, and a deletion that arrives before any turn was ever written inserts a stamped row.
- **Concurrency.** A rowversion conflict or a duplicate-key error (2601/2627) re-reads the row and tries again, up to 3 attempts in all, so two writers racing on the same row never double-count.

**Failure handling.** A catalog read failure retries with exponential backoff from 5 seconds up to 5 minutes. A missing hosted agent invalidates the catalog cache and retries — startup already proved every hosted agent has a model, so a miss here means a mapping swap landed mid-read. An agent that isn't in `AgentNames.All` at all — a deleted document naming an agent this host no longer runs — is dropped with a warning. `SessionStoreUnavailableException` (EF's retries exhausted, or a `SqlException` of severity 17 — the server out of log space, disk, memory, or locks — or severity 20 and above, a lost connection, or a −2/258 timeout) gets the same backoff. Anything else is dropped and logged by **exception type names only** — a `SqlException` message can quote the row's key, which holds the caller's `oid`.

**Shutdown.** `SessionSummaryProcessor.StopAsync` completes the channel and waits for the running loop to finish within `HostOptions.ShutdownTimeout`; if the store is down, it stops retrying and returns promptly instead of holding up shutdown.

**Residual loss (documented, not a bug).** A crash, a channel that was already full, or a non-transient failure can lose a queued write. The session's next save or deletion re-sends its complete counts, so the gap closes on the next turn — except a lost deletion stamp, which stays lost until an operator notices.

**AG-UI caveat.** The AG-UI host does not call `SaveSessionAsync` for a stream that failed, or that the client abandoned before it finished — a known limitation of the hosting package, not of this pipeline — so that turn's usage never reaches Cosmos DB or the SQL summary either.

## The `api/conversations` REST resource

`SessionService` and `SessionEndpoints` serve a caller's own conversations independently of the agent pipeline — reading the same two Cosmos DB containers directly. Every operation is scoped to `ICallerContext.UserId`; a session belonging to another caller, or that doesn't exist, is a 404, never a 403 — the caller can't distinguish "not yours" from "doesn't exist."

| Method | Route | Behavior |
|---|---|---|
| `GET` | `api/conversations` | Lists the caller's sessions, newest first (`dateCreated DESC`), with `skip`/`take` paging (`take` ≤ 100). |
| `GET` | `api/conversations/{sessionId}` | Returns one session. |
| `GET` | `api/conversations/{sessionId}/messages` | Lists the session's messages in sequence order, with `skip`/`take` paging (`take` ≤ 200). `TotalCount` is the session document's `messageCount` — no extra `COUNT` query. |
| `DELETE` | `api/conversations/{sessionId}` | Deletes every message, then the session document. `204 No Content`; a repeat call on the same id returns `404` — the service confirms the session still exists (and still belongs to the caller) before deleting, so the *second* delete finds nothing to authorize against. |

Every route requires the `AgentAccess` authorization policy (see [Configuration](../operations/configuration.md)) and returns `PaginatedResponseDto<T>` (`items`, `skip`, `take`, `totalCount`) for the two list endpoints.
