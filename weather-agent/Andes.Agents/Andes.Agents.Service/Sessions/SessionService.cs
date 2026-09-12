using Andes.Agents.Dto.Pagination;
using Andes.Agents.Dto.Sessions;
using Andes.Agents.Entity.Sessions;
using Andes.Agents.Repository.Sessions;
using Andes.Agents.Repository.Sessions.Interfaces;
using Andes.Agents.Service.Exceptions;
using Andes.Agents.Service.Security;

namespace Andes.Agents.Service.Sessions;

/// <summary>The caller's conversations: every operation is scoped to the authenticated user.</summary>
public interface ISessionService
{
    /// <summary>Lists the caller's sessions, newest first.</summary>
    Task<PaginatedResponseDto<SessionDto>> ListAsync(int skip, int take, CancellationToken cancellationToken);

    /// <summary>Returns one of the caller's sessions.</summary>
    /// <exception cref="NotFoundException">The session does not exist or belongs to another caller.</exception>
    Task<SessionDto> GetAsync(string sessionId, CancellationToken cancellationToken);

    /// <summary>Lists the messages of one of the caller's sessions in sequence order.</summary>
    /// <exception cref="NotFoundException">The session does not exist or belongs to another caller.</exception>
    Task<PaginatedResponseDto<SessionMessageDto>> ListMessagesAsync(string sessionId, int skip, int take, CancellationToken cancellationToken);

    /// <summary>Deletes one of the caller's sessions and every message in it.</summary>
    /// <exception cref="NotFoundException">The session does not exist or belongs to another caller.</exception>
    Task DeleteAsync(string sessionId, CancellationToken cancellationToken);
}

/// <inheritdoc />
public sealed class SessionService(
    ISessionRepository sessions,
    ISessionMessageRepository messages,
    ISessionSummaryChannel summaries,
    ICallerContext caller) : ISessionService
{
    private readonly ISessionRepository _sessions = sessions;
    private readonly ISessionMessageRepository _messages = messages;
    private readonly ISessionSummaryChannel _summaries = summaries;
    private readonly ICallerContext _caller = caller;

    /// <inheritdoc />
    public async Task<PaginatedResponseDto<SessionDto>> ListAsync(int skip, int take, CancellationToken cancellationToken)
    {
        string userId = _caller.UserId;

        IReadOnlyList<SessionDocument> documents = await _sessions.ListAsync(userId, skip, take, cancellationToken).ConfigureAwait(false);
        int total = await _sessions.CountAsync(userId, cancellationToken).ConfigureAwait(false);

        return new PaginatedResponseDto<SessionDto>([.. documents.Select(SessionMapper.MapToSessionDto)], skip, take, total);
    }

    /// <inheritdoc />
    public async Task<SessionDto> GetAsync(string sessionId, CancellationToken cancellationToken)
    {
        SessionRead read = await RequireAsync(sessionId, cancellationToken).ConfigureAwait(false);

        return SessionMapper.MapToSessionDto(read.Document);
    }

    /// <inheritdoc />
    public async Task<PaginatedResponseDto<SessionMessageDto>> ListMessagesAsync(string sessionId, int skip, int take, CancellationToken cancellationToken)
    {
        SessionRead read = await RequireAsync(sessionId, cancellationToken).ConfigureAwait(false);
        SessionDocument document = read.Document;

        IReadOnlyList<SessionMessageDocument> documents = await _messages
            .ListAsync(document.UserId, document.SessionId, skip, take, cancellationToken)
            .ConfigureAwait(false);

        return new PaginatedResponseDto<SessionMessageDto>(
            [.. documents.Select(SessionMapper.MapToSessionMessageDto)],
            skip,
            take,
            document.MessageCount);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string sessionId, CancellationToken cancellationToken)
    {
        SessionRead read = await RequireAsync(sessionId, cancellationToken).ConfigureAwait(false);
        SessionDocument document = read.Document;

        await _messages.DeleteAllAsync(document.UserId, document.SessionId, cancellationToken).ConfigureAwait(false);
        await _sessions.DeleteAsync(document.UserId, document.SessionId, cancellationToken).ConfigureAwait(false);

        _summaries.EnqueueDeletion(document);
    }

    private async Task<SessionRead> RequireAsync(string sessionId, CancellationToken cancellationToken)
    {
        SessionIdValidator.EnsureValid(sessionId);

        return await _sessions.GetAsync(_caller.UserId, sessionId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Conversation '{sessionId}' was not found.");
    }
}
