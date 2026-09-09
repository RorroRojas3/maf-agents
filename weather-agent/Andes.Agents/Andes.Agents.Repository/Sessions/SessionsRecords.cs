using Andes.Agents.Entity.Sessions;

namespace Andes.Agents.Repository.Sessions;

/// <summary>A session document together with the ETag it was read at.</summary>
/// <param name="Document">The document.</param>
/// <param name="Etag">The ETag a later replace must present.</param>
public sealed record SessionRead(SessionDocument Document, string Etag);
