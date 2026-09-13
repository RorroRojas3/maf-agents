namespace Andes.Agents.Dto.Sessions;

/// <summary>One message of a conversation session.</summary>
/// <param name="Id">The message id.</param>
/// <param name="Sequence">One-based position within the session.</param>
/// <param name="Role">Chat role as the model client names it.</param>
/// <param name="Text">Plain text of the message; null for tool calls and results.</param>
/// <param name="DateCreated">UTC time the message was stored.</param>
public sealed record SessionMessageDto(string Id, long Sequence, string Role, string? Text, DateTimeOffset DateCreated);
