namespace Andes.Agents.Repository.Sessions.Interfaces;

/// <summary>Writes the reporting summary of each session incarnation.</summary>
public interface ISessionSummaryRepository
{
    /// <summary>Merges a session's latest counts into its summary, pricing the tokens they add, and stamps the deletion the write carries.</summary>
    /// <remarks>Counts lower than the stored ones belong to an older write and change nothing; a deletion stamp is never cleared.</remarks>
    /// <exception cref="SessionConflictException">Concurrent writers kept changing the summary.</exception>
    /// <exception cref="SessionStoreUnavailableException">The store is unreachable, timed out or out of resources.</exception>
    Task UpsertAsync(SessionSummaryWrite write, CancellationToken cancellationToken);
}
