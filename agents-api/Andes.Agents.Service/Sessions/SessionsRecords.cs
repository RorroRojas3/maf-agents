using Andes.Agents.Entity.Sessions;

namespace Andes.Agents.Service.Sessions;

/// <summary>A session's counts, queued for its reporting summary once the session store has saved or deleted the session.</summary>
/// <param name="UserId">Entra object id of the owner.</param>
/// <param name="SessionId">Protocol continuation id.</param>
/// <param name="DateCreated">UTC time the session was created; with the two ids it identifies one incarnation.</param>
/// <param name="AgentName">Registration name of the agent the session belongs to.</param>
/// <param name="DateModified">UTC time of the session's last message.</param>
/// <param name="MessageCount">Number of messages stored for the session.</param>
/// <param name="Usage">Cumulative token usage.</param>
/// <param name="Details">Cumulative cached input and reasoning tokens; null for a deletion, whose document does not carry them.</param>
/// <param name="DateDeleted">UTC time the conversation was deleted; null for a turn.</param>
public sealed record SessionSummaryWork(
    Guid UserId,
    Guid SessionId,
    DateTimeOffset DateCreated,
    string AgentName,
    DateTimeOffset DateModified,
    int MessageCount,
    SessionUsage Usage,
    SessionUsageDetails? Details,
    DateTimeOffset? DateDeleted);
