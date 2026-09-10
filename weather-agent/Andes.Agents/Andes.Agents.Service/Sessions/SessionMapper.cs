using Andes.Agents.Dto.Sessions;
using Andes.Agents.Entity.Sessions;

namespace Andes.Agents.Service.Sessions;

/// <summary>Maps session documents to their response DTOs.</summary>
public static class SessionMapper
{
    /// <summary>Maps a session document.</summary>
    public static SessionDto MapToSessionDto(SessionDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return new SessionDto(
            document.SessionId,
            document.AgentId,
            document.Title,
            document.MessageCount,
            MapToSessionUsageDto(document.Usage),
            document.DateCreated,
            document.DateUpdated,
            document.LastMessageAt);
    }

    /// <summary>Maps a message document.</summary>
    public static SessionMessageDto MapToSessionMessageDto(SessionMessageDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return new SessionMessageDto(document.Id, document.Sequence, document.Role, document.Text, document.DateCreated);
    }

    /// <summary>Maps cumulative usage.</summary>
    public static SessionUsageDto MapToSessionUsageDto(SessionUsage usage)
    {
        ArgumentNullException.ThrowIfNull(usage);

        return new SessionUsageDto(usage.InputTokens, usage.OutputTokens, usage.TotalTokens);
    }
}
