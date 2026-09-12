namespace Andes.Agents.Repository.Sessions;

/// <summary>A write lost a race with another writer of the same session: a duplicate id, a stale ETag or a vanished document.</summary>
public sealed class SessionConflictException : Exception
{
    /// <summary>Creates the exception with the default message.</summary>
    public SessionConflictException()
        : base("The session was modified by another request.")
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    public SessionConflictException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and the failure that caused it.</summary>
    public SessionConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>The store is unreachable, timed out or out of resources; the same write can be tried again later.</summary>
public sealed class SessionStoreUnavailableException : Exception
{
    /// <summary>Creates the exception with the default message.</summary>
    public SessionStoreUnavailableException()
        : base("The session store is unavailable.")
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    public SessionStoreUnavailableException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and the failure that caused it.</summary>
    public SessionStoreUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
