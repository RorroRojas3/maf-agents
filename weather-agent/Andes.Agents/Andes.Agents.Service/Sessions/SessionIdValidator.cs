using Andes.Agents.Common.Constants;

namespace Andes.Agents.Service.Sessions;

/// <summary>Rejects continuation ids that Cosmos DB could not store, before any I/O happens.</summary>
public static class SessionIdValidator
{
    /// <summary>Throws when the id is empty, too long, or contains a character Cosmos DB forbids.</summary>
    /// <exception cref="InvalidSessionIdException">The id cannot be used.</exception>
    public static void EnsureValid(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new InvalidSessionIdException("The session id is empty.");
        }

        if (sessionId.Length > SessionIdRules.MaxLength)
        {
            throw new InvalidSessionIdException($"The session id is longer than {SessionIdRules.MaxLength} characters.");
        }

        if (sessionId.AsSpan().IndexOfAny(SessionIdRules.InvalidCharacters) >= 0)
        {
            throw new InvalidSessionIdException("The session id contains one of '/', '\\', '?' or '#'.");
        }
    }
}
