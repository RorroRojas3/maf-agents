using System.Text.Json;
using Andes.Agents.Common.Constants;
using Andes.Agents.Entity.Sessions;
using Andes.Agents.Repository.Sessions;
using Andes.Agents.Repository.Sessions.Interfaces;
using Andes.Agents.Service.Options;
using Andes.Agents.Service.Serialization;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace Andes.Agents.Service.Sessions;

/// <summary>Stores one document per message and replays the most recent ones before each turn.</summary>
/// <remarks>
/// A session must be bound by <see cref="PersistedAgentSessionStore"/> before its first turn; an unbound
/// session fails rather than writing under an id no protocol continuation could ever find again.
/// </remarks>
public sealed class PersistedChatHistoryProvider(
    ISessionMessageRepository messages,
    IOptions<SessionsOptions> options,
    TimeProvider timeProvider) : ChatHistoryProvider
{
    private const int _titleLength = 80;

    private readonly ISessionMessageRepository _messages = messages;
    private readonly SessionsOptions _options = options.Value;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly ProviderSessionState<SessionHistoryState> _state = new(
        _ => SessionHistoryState.Unbound,
        SessionStateKeys.History,
        SessionStateJson.Options);

    /// <inheritdoc />
    public override IReadOnlyList<string> StateKeys { get; } = [SessionStateKeys.History];

    /// <inheritdoc />
    protected override async ValueTask<IEnumerable<ChatMessage>> ProvideChatHistoryAsync(InvokingContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        SessionHistoryState state = RequireBound(context.Session);

        IReadOnlyList<SessionMessageDocument> documents = await _messages
            .ListLatestAsync(state.UserId, state.SessionId, _options.MaxHistoryMessages, cancellationToken)
            .ConfigureAwait(false);

        List<ChatMessage> history = [];

        foreach (SessionMessageDocument document in documents)
        {
            // A message that cannot be read would leave a tool call without its result; better no history than a broken one.
            ChatMessage message = document.Message.Deserialize<ChatMessage>(AIJsonUtilities.DefaultOptions)
                ?? throw new InvalidOperationException($"Message '{document.Id}' could not be deserialized.");

            history.Add(message);
        }

        TrimLeadingToolResults(history);

        return history;
    }

    /// <inheritdoc />
    protected override async ValueTask StoreChatHistoryAsync(InvokedContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        SessionHistoryState state = RequireBound(context.Session);
        List<ChatMessage> turn = [.. context.RequestMessages, .. context.ResponseMessages ?? []];

        if (turn.Count == 0)
        {
            return;
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        string agentId = context.Agent.Id;

        try
        {
            await _messages.AppendAsync(Describe(turn, state, agentId, now), cancellationToken).ConfigureAwait(false);
        }
        catch (SessionConflictException ex)
        {
            // A concurrent turn got there first. Retrying would commit this turn's messages behind it, so the loser writes nothing.
            throw new ConversationBusyException("Another turn of this conversation is writing to it.", ex);
        }

        _state.SaveState(context.Session, state with
        {
            MessageCount = state.MessageCount + turn.Count,
            Title = state.Title ?? TitleOf(turn),
            LastMessageAt = now,
        });
    }

    /// <summary>Reads the history state of a session; unbound when the store has not seen it.</summary>
    public SessionHistoryState GetState(AgentSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        return _state.GetOrInitializeState(session);
    }

    /// <summary>Binds a session to its owner and continuation id, or updates its running summary.</summary>
    public void SetState(AgentSession session, SessionHistoryState state)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(state);

        _state.SaveState(session, state);
    }

    #region Private Methods

    private SessionHistoryState RequireBound(AgentSession? session)
    {
        if (session is null)
        {
            throw new InvalidOperationException("The agent ran without a session, so there is no history to keep.");
        }

        SessionHistoryState state = _state.GetOrInitializeState(session);

        return state.IsBound
            ? state
            : throw new InvalidOperationException("The session was not bound by the session store.");
    }

    private static List<SessionMessageDocument> Describe(List<ChatMessage> turn, SessionHistoryState state, string agentId, DateTimeOffset now)
    {
        List<SessionMessageDocument> documents = new(turn.Count);

        for (int index = 0; index < turn.Count; index++)
        {
            ChatMessage message = turn[index];
            long sequence = state.MessageCount + index + 1;

            documents.Add(new SessionMessageDocument(
                Id: $"{state.SessionId}:{sequence:D8}",
                UserId: state.UserId,
                SessionId: state.SessionId,
                AgentId: agentId,
                Sequence: sequence,
                Role: message.Role.Value,
                Text: string.IsNullOrWhiteSpace(message.Text) ? null : message.Text,
                MessageId: message.MessageId,
                DateCreated: now,
                Message: JsonSerializer.SerializeToElement(message, AIJsonUtilities.DefaultOptions)));
        }

        return documents;
    }

    private static string? TitleOf(List<ChatMessage> turn)
    {
        string? text = turn
            .Where(message => message.Role == ChatRole.User)
            .Select(message => message.Text.Trim())
            .FirstOrDefault(text => text.Length > 0);

        return text is null
            ? null
            : text.Length <= _titleLength ? text : text[.._titleLength];
    }

    // A window that opens on a tool result has lost the call it answers, which the model rejects.
    private static void TrimLeadingToolResults(List<ChatMessage> history)
    {
        while (history.Count > 0 && history[0].Contents.Any(content => content is FunctionResultContent))
        {
            history.RemoveAt(0);
        }
    }

    #endregion
}
