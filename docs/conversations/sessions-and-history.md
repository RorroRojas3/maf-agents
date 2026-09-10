# Sessions and history

## Overview

Every turn of the weather agent needs somewhere durable to keep the conversation, because the model client is configured with `store:false` (see [Hosting and protocols](../agent/hosting-and-protocols.md)) — Cosmos DB is the *only* place a session's state and message history live. Two Cosmos DB containers hold it: `sessions` (one document per conversation) and `messages` (one document per chat message). A caller resumes a conversation by sending back the same continuation id — the A2A `contextId` or the AG-UI `threadId` — which becomes the Cosmos DB `id` and partition value.

This page covers the document shapes, how the Microsoft Agent Framework session store and history provider read and write them, what happens when two turns race, and the `api/conversations` REST resource that lets a caller browse and delete their own conversations.

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
| `dateUpdated` | DateTimeOffset | UTC time of the last save. |
| `lastMessageAt` | DateTimeOffset? | UTC time of the last stored message. |
| `state` | JSON | The Agent Framework's own serialized `AgentSession`; opaque to this application. Excluded from indexing (see below). |

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

A continuation id becomes a Cosmos DB document id and partition value, so `SessionIdValidator` rejects, before any I/O:

- an id longer than 255 characters (`SessionIdRules.MaxLength`), and
- an id containing any of `/ \ ? #` (`SessionIdRules.InvalidCharacters`) — characters Cosmos DB forbids in an `id`.

A rejection is `InvalidSessionIdException` → **HTTP 400, problem type `validation-error`**.

## The `api/conversations` REST resource

`SessionService` and `SessionEndpoints` serve a caller's own conversations independently of the agent pipeline — reading the same two Cosmos DB containers directly. Every operation is scoped to `ICallerContext.UserId`; a session belonging to another caller, or that doesn't exist, is a 404, never a 403 — the caller can't distinguish "not yours" from "doesn't exist."

| Method | Route | Behavior |
|---|---|---|
| `GET` | `api/conversations` | Lists the caller's sessions, newest first (`dateCreated DESC`), with `skip`/`take` paging (`take` ≤ 100). |
| `GET` | `api/conversations/{sessionId}` | Returns one session. |
| `GET` | `api/conversations/{sessionId}/messages` | Lists the session's messages in sequence order, with `skip`/`take` paging (`take` ≤ 200). `TotalCount` is the session document's `messageCount` — no extra `COUNT` query. |
| `DELETE` | `api/conversations/{sessionId}` | Deletes every message, then the session document. `204 No Content`; a repeat call on the same id returns `404` — the service confirms the session still exists (and still belongs to the caller) before deleting, so the *second* delete finds nothing to authorize against. |

Every route requires the `AgentAccess` authorization policy (see [Configuration](../operations/configuration.md)) and returns `PaginatedResponseDto<T>` (`items`, `skip`, `take`, `totalCount`) for the two list endpoints.
