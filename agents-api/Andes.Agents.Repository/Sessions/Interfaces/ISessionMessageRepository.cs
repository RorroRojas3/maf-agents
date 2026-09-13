using Andes.Agents.Entity.Sessions;

namespace Andes.Agents.Repository.Sessions.Interfaces;

/// <summary>Reads and writes the messages of a session, always within that session's partition.</summary>
public interface ISessionMessageRepository
{
    /// <summary>Appends messages of one session in batches of up to 100; each batch is atomic.</summary>
    /// <exception cref="SessionConflictException">A message with one of the ids already exists.</exception>
    Task AppendAsync(IReadOnlyList<SessionMessageDocument> documents, CancellationToken cancellationToken);

    /// <summary>Lists messages in sequence order.</summary>
    Task<IReadOnlyList<SessionMessageDocument>> ListAsync(string userId, string sessionId, int skip, int take, CancellationToken cancellationToken);

    /// <summary>Returns the last <paramref name="count"/> messages, in sequence order.</summary>
    Task<IReadOnlyList<SessionMessageDocument>> ListLatestAsync(string userId, string sessionId, int count, CancellationToken cancellationToken);

    /// <summary>Returns the highest sequence stored for the session, or zero when it has none.</summary>
    Task<long> MaxSequenceAsync(string userId, string sessionId, CancellationToken cancellationToken);

    /// <summary>Deletes every message of the session.</summary>
    Task DeleteAllAsync(string userId, string sessionId, CancellationToken cancellationToken);
}
