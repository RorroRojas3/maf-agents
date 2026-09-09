using System.Text.Json;
using System.Text.Json.Serialization;

namespace Andes.Agents.Entity.Sessions;

/// <summary>A conversation session as stored in the <c>sessions</c> container.</summary>
/// <param name="Id">Document id; equals <paramref name="SessionId"/>.</param>
/// <param name="UserId">Entra object id of the owner; first level of the partition key.</param>
/// <param name="SessionId">Protocol continuation id (A2A <c>contextId</c>, AG-UI <c>threadId</c>); second level of the partition key.</param>
/// <param name="AgentId">Id of the agent the session belongs to.</param>
/// <param name="Title">First user message, truncated; null until the first turn is stored.</param>
/// <param name="MessageCount">Number of messages stored for the session.</param>
/// <param name="Usage">Cumulative token usage across every turn.</param>
/// <param name="DateCreated">UTC time the session was created.</param>
/// <param name="DateUpdated">UTC time of the last save.</param>
/// <param name="LastMessageAt">UTC time of the last stored message.</param>
/// <param name="State">The serialized agent session; opaque to this application.</param>
public sealed record SessionDocument(
    string Id,
    string UserId,
    string SessionId,
    string AgentId,
    string? Title,
    int MessageCount,
    SessionUsage Usage,
    DateTimeOffset DateCreated,
    DateTimeOffset DateUpdated,
    DateTimeOffset? LastMessageAt,
    JsonElement State);

/// <summary>One chat message as stored in the <c>messages</c> container.</summary>
/// <param name="Id">Document id, <c>{sessionId}:{sequence:D8}</c>, so a duplicate write is a conflict rather than a duplicate.</param>
/// <param name="UserId">Entra object id of the owner; first level of the partition key.</param>
/// <param name="SessionId">Session the message belongs to; second level of the partition key.</param>
/// <param name="AgentId">Id of the agent that took part in the turn.</param>
/// <param name="Sequence">One-based position within the session; the only ordering key.</param>
/// <param name="Role">Chat role as the model client names it.</param>
/// <param name="Text">Plain-text projection for listing; null for tool calls and results.</param>
/// <param name="MessageId">Provider-assigned message id, when one was given.</param>
/// <param name="DateCreated">UTC time the message was stored.</param>
/// <param name="Message">The full chat message serialized with the AI abstractions default options.</param>
public sealed record SessionMessageDocument(
    string Id,
    string UserId,
    string SessionId,
    string AgentId,
    long Sequence,
    string Role,
    string? Text,
    string? MessageId,
    DateTimeOffset DateCreated,
    JsonElement Message);

/// <summary>Token usage accumulated over a session, as the provider reported it.</summary>
public sealed record SessionUsage(long InputTokens, long OutputTokens, long TotalTokens)
{
    /// <summary>Usage of a session that has not completed a turn.</summary>
    public static SessionUsage Empty { get; } = new(0, 0, 0);

    /// <summary>Returns this usage plus one turn's counts.</summary>
    public SessionUsage Add(long inputTokens, long outputTokens, long totalTokens) =>
        new(InputTokens + inputTokens, OutputTokens + outputTokens, TotalTokens + totalTokens);
}

/// <summary>
/// Identity and running summary of a session's persisted history, kept in the agent session's state bag
/// so the history provider and the session store agree without a query.
/// </summary>
/// <param name="UserId">Entra object id of the owner; empty while unbound.</param>
/// <param name="SessionId">Protocol continuation id the messages are stored under.</param>
/// <param name="DateCreated">UTC time the session was created.</param>
/// <param name="MessageCount">Number of messages stored so far; the next message takes <c>MessageCount + 1</c>.</param>
/// <param name="Title">First user message, truncated; null until the first turn is stored.</param>
/// <param name="LastMessageAt">UTC time of the last stored message.</param>
/// <param name="Etag">The session document's ETag as last read; null before the first save.</param>
public sealed record SessionHistoryState(
    string UserId,
    string SessionId,
    DateTimeOffset DateCreated,
    int MessageCount,
    string? Title,
    DateTimeOffset? LastMessageAt,
    string? Etag)
{
    /// <summary>The state of a session the store has not bound to a caller yet.</summary>
    public static SessionHistoryState Unbound { get; } = new(string.Empty, string.Empty, default, 0, null, null, null);

    /// <summary>Whether the store has bound this session to a caller and continuation id.</summary>
    [JsonIgnore]
    public bool IsBound => UserId.Length > 0;
}
