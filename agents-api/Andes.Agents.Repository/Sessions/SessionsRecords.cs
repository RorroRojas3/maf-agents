using Andes.Agents.Entity.Sessions;
using Andes.Agents.Repository.Agents;

namespace Andes.Agents.Repository.Sessions;

/// <summary>A session document together with the ETag it was read at.</summary>
/// <param name="Document">The document.</param>
/// <param name="Etag">The ETag a later replace must present.</param>
public sealed record SessionRead(SessionDocument Document, string Etag);

/// <summary>A session's counts as its latest save or its deletion left them, to merge into its reporting summary.</summary>
/// <param name="UserId">Entra object id of the owner.</param>
/// <param name="SessionId">Protocol continuation id.</param>
/// <param name="DateCreated">UTC time the session was created; with the two ids it identifies one incarnation.</param>
/// <param name="AgentId">Key of the agent.</param>
/// <param name="ModelId">Key of the agent's active model.</param>
/// <param name="Prices">That model's prices, applied to the tokens this write adds.</param>
/// <param name="DateModified">UTC time of the session's last message.</param>
/// <param name="MessageCount">Number of messages stored for the session.</param>
/// <param name="Usage">Cumulative token usage.</param>
/// <param name="Details">Cumulative cached input and reasoning tokens; null when the writer does not know them, as for a deletion.</param>
/// <param name="DateDeleted">UTC time the conversation was deleted; null for a save.</param>
public sealed record SessionSummaryWrite(
    Guid UserId,
    Guid SessionId,
    DateTimeOffset DateCreated,
    Guid AgentId,
    Guid ModelId,
    TokenPrices Prices,
    DateTimeOffset DateModified,
    int MessageCount,
    SessionUsage Usage,
    SessionUsageDetails? Details,
    DateTimeOffset? DateDeleted);
