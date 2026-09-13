using System.ComponentModel.DataAnnotations.Schema;
using Andes.Agents.Entity.Base;
using Microsoft.EntityFrameworkCore;

namespace Andes.Agents.Entity.Sessions;

/// <summary>Token usage and estimated cost of one session, kept for reporting.</summary>
/// <remarks>
/// <see cref="BaseEntity.DateCreated"/> is when the session was created and <see cref="BaseEntity.DateModified"/> its last
/// message, both copied from the session store. The row holds no message text.
/// </remarks>
[Table("Session", Schema = "Core")]
public sealed class SessionSummary : BaseEntity
{
    /// <summary>Gets or sets the Entra object id of the owner.</summary>
    /// <remarks>Personal data: it may be stored and reported, never logged.</remarks>
    public Guid UserId { get; set; }

    /// <summary>Gets or sets the protocol continuation id (A2A <c>contextId</c>, AG-UI <c>threadId</c>).</summary>
    public Guid SessionId { get; set; }

    /// <summary>Gets or sets the key of the agent the session belongs to.</summary>
    public Guid AgentId { get; set; }

    /// <summary>Gets or sets the key of the agent's active model when the latest counts were recorded.</summary>
    public Guid ModelId { get; set; }

    /// <summary>Gets or sets the number of messages stored for the session.</summary>
    public int MessageCount { get; set; }

    /// <summary>Gets or sets the input tokens of every turn, cached tokens included.</summary>
    public long InputTokens { get; set; }

    /// <summary>Gets or sets the input tokens read from the provider's cache.</summary>
    public long CachedInputTokens { get; set; }

    /// <summary>Gets or sets the output tokens of every turn, reasoning tokens included.</summary>
    public long OutputTokens { get; set; }

    /// <summary>Gets or sets the output tokens the model spent reasoning.</summary>
    public long ReasoningTokens { get; set; }

    /// <summary>Gets or sets the total tokens as the provider reported them.</summary>
    public long TotalTokens { get; set; }

    /// <summary>Gets or sets the US dollar cost, each increment priced when it was recorded.</summary>
    // Nine decimals keep the cost of a single cached token.
    [Precision(19, 9)]
    public decimal EstimatedCost { get; set; }

    /// <summary>Gets or sets the UTC time the conversation was deleted; the row outlives it as usage history.</summary>
    public DateTimeOffset? DateDeleted { get; set; }
}
