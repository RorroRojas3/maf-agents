using System.Text.Json;
using Andes.Agents.Common.Constants;
using Andes.Agents.Entity.Sessions;
using Andes.Agents.Repository.Sessions;
using Andes.Agents.Repository.Sessions.Interfaces;
using Andes.Agents.Service.Security;
using Andes.Agents.Service.Serialization;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;

namespace Andes.Agents.Service.Sessions;

/// <summary>Persists hosted agent sessions, one document per session under the caller's partition.</summary>
/// <remarks>
/// The caller is read only when a session is looked up or deleted; a save relies on the owner bound into
/// the session, so it does not depend on an ambient HTTP context.
/// </remarks>
public sealed class PersistedAgentSessionStore(
    ISessionRepository sessions,
    ISessionMessageRepository messages,
    PersistedChatHistoryProvider history,
    ICallerContext caller,
    TimeProvider timeProvider) : AgentSessionStore
{
    private readonly ISessionRepository _sessions = sessions;
    private readonly ISessionMessageRepository _messages = messages;
    private readonly PersistedChatHistoryProvider _history = history;
    private readonly ICallerContext _caller = caller;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc />
    public override async ValueTask<AgentSession> GetSessionAsync(AIAgent agent, string sessionStoreId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agent);
        SessionIdValidator.EnsureValid(sessionStoreId);

        string userId = _caller.UserId;
        SessionRead? existing = await _sessions.GetAsync(userId, sessionStoreId, cancellationToken).ConfigureAwait(false);

        // A turn that stored its messages but never saved the session leaves the document, or its absence, behind the container.
        long stored = await _messages.MaxSequenceAsync(userId, sessionStoreId, cancellationToken).ConfigureAwait(false);

        if (existing is null)
        {
            AgentSession created = await agent.CreateSessionAsync(cancellationToken).ConfigureAwait(false);

            _history.SetState(created, new SessionHistoryState(
                userId,
                sessionStoreId,
                _timeProvider.GetUtcNow(),
                checked((int)stored),
                Title: null,
                LastMessageAt: null,
                Etag: null));

            return created;
        }

        SessionDocument document = existing.Document;
        AgentSession session = await agent
            .DeserializeSessionAsync(document.State, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        // The document, not the serialized bag, is the source of truth for what was last saved.
        _history.SetState(session, new SessionHistoryState(
            document.UserId,
            document.SessionId,
            document.DateCreated,
            checked((int)Math.Max(document.MessageCount, stored)),
            document.Title,
            document.LastMessageAt,
            existing.Etag));

        return session;
    }

    /// <inheritdoc />
    public override async ValueTask SaveSessionAsync(AIAgent agent, string sessionStoreId, AgentSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(session);

        SessionHistoryState state = _history.GetState(session);

        if (!state.IsBound || state.SessionId != sessionStoreId)
        {
            throw new InvalidOperationException("The session was not obtained from this store.");
        }

        SessionUsage usage = session.StateBag.GetValue<SessionUsage>(SessionStateKeys.Usage, SessionStateJson.Options) ?? SessionUsage.Empty;
        JsonElement serialized = await agent.SerializeSessionAsync(session, cancellationToken: cancellationToken).ConfigureAwait(false);

        SessionDocument document = new(
            Id: state.SessionId,
            UserId: state.UserId,
            SessionId: state.SessionId,
            AgentId: agent.Id,
            Title: state.Title,
            MessageCount: state.MessageCount,
            Usage: usage,
            DateCreated: state.DateCreated,
            DateUpdated: _timeProvider.GetUtcNow(),
            LastMessageAt: state.LastMessageAt,
            State: serialized);

        try
        {
            string etag = state.Etag is null
                ? await _sessions.CreateAsync(document, cancellationToken).ConfigureAwait(false)
                : await _sessions.ReplaceAsync(document, state.Etag, cancellationToken).ConfigureAwait(false);

            _history.SetState(session, state with { Etag = etag });
        }
        catch (SessionConflictException ex)
        {
            throw new ConversationBusyException("Another turn of this conversation completed first; retry with the latest state.", ex);
        }
    }

    /// <inheritdoc />
    public override async ValueTask DeleteSessionAsync(AIAgent agent, string sessionStoreId, CancellationToken cancellationToken = default)
    {
        SessionIdValidator.EnsureValid(sessionStoreId);

        string userId = _caller.UserId;

        await _messages.DeleteAllAsync(userId, sessionStoreId, cancellationToken).ConfigureAwait(false);
        await _sessions.DeleteAsync(userId, sessionStoreId, cancellationToken).ConfigureAwait(false);
    }
}
