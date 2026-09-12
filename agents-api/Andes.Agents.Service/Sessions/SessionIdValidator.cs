namespace Andes.Agents.Service.Sessions;

/// <summary>Rejects continuation ids that are not GUIDs, before any I/O happens.</summary>
public static class SessionIdValidator
{
    /// <summary>Throws unless the id is exactly a GUID in hyphenated (<c>D</c>) or plain (<c>N</c>) form.</summary>
    /// <exception cref="InvalidSessionIdException">The id cannot be used.</exception>
    public static void EnsureValid(string sessionId)
    {
        // The hosts issue N when a client sends no id and browsers generate D. The length comes first because TryParseExact
        // trims whitespace, and a padded id is a different Cosmos DB id for the same GUID.
        bool isGuid = sessionId switch
        {
            { Length: 36 } => Guid.TryParseExact(sessionId, "D", out _),
            { Length: 32 } => Guid.TryParseExact(sessionId, "N", out _),
            _ => false,
        };

        if (!isGuid)
        {
            throw new InvalidSessionIdException("The session id must be a GUID.");
        }
    }
}
