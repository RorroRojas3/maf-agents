namespace Andes.Agents.Dto.Sessions;

/// <summary>A conversation session as returned by the conversations resource.</summary>
/// <param name="Id">The session id, which is also the protocol continuation id.</param>
/// <param name="AgentId">Id of the agent the session belongs to.</param>
/// <param name="Title">First user message, truncated; null until the first turn is stored.</param>
/// <param name="MessageCount">Number of messages stored for the session.</param>
/// <param name="Usage">Cumulative token usage.</param>
/// <param name="DateCreated">UTC time the session was created.</param>
/// <param name="DateModified">UTC time of the last save.</param>
/// <param name="LastMessageAt">UTC time of the last stored message.</param>
public sealed record SessionDto(
    string Id,
    string AgentId,
    string? Title,
    int MessageCount,
    SessionUsageDto Usage,
    DateTimeOffset DateCreated,
    DateTimeOffset DateModified,
    DateTimeOffset? LastMessageAt);
