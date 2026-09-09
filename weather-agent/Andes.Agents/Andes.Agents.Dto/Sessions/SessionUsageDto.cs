namespace Andes.Agents.Dto.Sessions;

/// <summary>Token usage accumulated over a session, as the provider reported it.</summary>
public sealed record SessionUsageDto(long InputTokens, long OutputTokens, long TotalTokens);
