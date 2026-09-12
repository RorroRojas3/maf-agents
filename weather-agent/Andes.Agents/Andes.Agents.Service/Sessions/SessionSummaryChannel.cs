using System.Threading.Channels;
using Andes.Agents.Entity.Sessions;
using Microsoft.Extensions.Logging;

namespace Andes.Agents.Service.Sessions;

/// <summary>Hands session counts from the request path to the writer of the reporting summaries.</summary>
/// <remarks>Enqueueing neither blocks nor fails: the session it describes is already saved, and the summary is reporting data.</remarks>
public interface ISessionSummaryChannel
{
    /// <summary>Queues the counts a saved turn left on its session.</summary>
    void EnqueueTurn(SessionHistoryState state, SessionUsage usage, SessionUsageDetails details, string agentName);

    /// <summary>Queues a deleted session with the counts its last saved document carried.</summary>
    void EnqueueDeletion(SessionDocument document);

    /// <summary>Reads queued work in order until the channel is completed and drained.</summary>
    IAsyncEnumerable<SessionSummaryWork> ReadAllAsync(CancellationToken cancellationToken);

    /// <summary>Stops accepting work; a reader finishes once the backlog is drained.</summary>
    void Complete();
}

/// <inheritdoc />
public sealed partial class SessionSummaryChannel(TimeProvider timeProvider, ILogger<SessionSummaryChannel> logger) : ISessionSummaryChannel
{
    // Bounds memory while the store is down; a dropped summary is rewritten by its session's next save.
    private const int _capacity = 10_000;

    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly ILogger<SessionSummaryChannel> _logger = logger;
    private readonly Channel<SessionSummaryWork> _channel = Channel.CreateBounded<SessionSummaryWork>(new BoundedChannelOptions(_capacity)
    {
        SingleReader = true,
        FullMode = BoundedChannelFullMode.Wait,
    });

    /// <inheritdoc />
    public void EnqueueTurn(SessionHistoryState state, SessionUsage usage, SessionUsageDetails details, string agentName)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(usage);
        ArgumentNullException.ThrowIfNull(details);
        ArgumentNullException.ThrowIfNull(agentName);

        if (ParseIds(state.UserId, state.SessionId) is not { } ids)
        {
            return;
        }

        Write(new SessionSummaryWork(
            ids.UserId,
            ids.SessionId,
            state.DateCreated,
            agentName,
            state.LastMessageAt ?? _timeProvider.GetUtcNow(),
            state.MessageCount,
            usage,
            details,
            DateDeleted: null));
    }

    /// <inheritdoc />
    public void EnqueueDeletion(SessionDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (ParseIds(document.UserId, document.SessionId) is not { } ids)
        {
            return;
        }

        Write(new SessionSummaryWork(
            ids.UserId,
            ids.SessionId,
            document.DateCreated,
            document.AgentId,
            document.LastMessageAt ?? document.DateModified,
            document.MessageCount,
            document.Usage,
            Details: null,
            DateDeleted: _timeProvider.GetUtcNow()));
    }

    /// <inheritdoc />
    public IAsyncEnumerable<SessionSummaryWork> ReadAllAsync(CancellationToken cancellationToken) => _channel.Reader.ReadAllAsync(cancellationToken);

    /// <inheritdoc />
    public void Complete() => _channel.Writer.TryComplete();

    private void Write(SessionSummaryWork work)
    {
        if (!_channel.Writer.TryWrite(work))
        {
            LogDropped();
        }
    }

    private (Guid UserId, Guid SessionId)? ParseIds(string userId, string sessionId)
    {
        if (Guid.TryParse(userId, out Guid parsedUserId) && Guid.TryParse(sessionId, out Guid parsedSessionId))
        {
            return (parsedUserId, parsedSessionId);
        }

        LogIdsNotGuids();

        return null;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "A session summary was dropped: the summary channel is full or no longer accepts work.")]
    private partial void LogDropped();

    [LoggerMessage(Level = LogLevel.Warning, Message = "A session summary was skipped: its user or session id is not a GUID.")]
    private partial void LogIdsNotGuids();
}
