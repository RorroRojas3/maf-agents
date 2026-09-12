using Andes.Agents.Entity.Sessions;

namespace Andes.Agents.Repository.Sessions.Interfaces;

/// <summary>Reads and writes session documents, always within one user's partition.</summary>
public interface ISessionRepository
{
    /// <summary>Point-reads a session; null when it does not exist.</summary>
    Task<SessionRead?> GetAsync(string userId, string sessionId, CancellationToken cancellationToken);

    /// <summary>Creates a session document that must not exist yet and returns its ETag.</summary>
    /// <exception cref="SessionConflictException">A document with the same id already exists.</exception>
    Task<string> CreateAsync(SessionDocument document, CancellationToken cancellationToken);

    /// <summary>Replaces a session document whose ETag still matches and returns the new ETag.</summary>
    /// <exception cref="SessionConflictException">The document changed since it was read, or was deleted.</exception>
    Task<string> ReplaceAsync(SessionDocument document, string etag, CancellationToken cancellationToken);

    /// <summary>Lists a user's sessions, newest first.</summary>
    Task<IReadOnlyList<SessionDocument>> ListAsync(string userId, int skip, int take, CancellationToken cancellationToken);

    /// <summary>Counts a user's sessions.</summary>
    Task<int> CountAsync(string userId, CancellationToken cancellationToken);

    /// <summary>Deletes a session document; succeeds when it is already gone.</summary>
    Task DeleteAsync(string userId, string sessionId, CancellationToken cancellationToken);
}
