using System.ComponentModel.DataAnnotations;

namespace Andes.Agents.Dto.Actions.Sessions;

/// <summary>Paging parameters for listing a caller's sessions.</summary>
/// <param name="Skip">Number of sessions to skip.</param>
/// <param name="Take">Page size, at most 100.</param>
public sealed record ListSessionsActionDto(
    [property: Range(0, int.MaxValue)] int Skip = 0,
    [property: Range(1, 100)] int Take = 20);

/// <summary>Paging parameters for listing the messages of a session.</summary>
/// <param name="Skip">Number of messages to skip.</param>
/// <param name="Take">Page size, at most 200.</param>
public sealed record ListSessionMessagesActionDto(
    [property: Range(0, int.MaxValue)] int Skip = 0,
    [property: Range(1, 200)] int Take = 50);
