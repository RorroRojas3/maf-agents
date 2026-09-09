namespace Andes.Agents.Service.Sessions;

/// <summary>Another turn of the same conversation ran concurrently; the caller should retry with the latest state.</summary>
public sealed class ConversationBusyException : Exception
{
    /// <summary>Creates the exception with the default message.</summary>
    public ConversationBusyException()
        : base("Another turn of this conversation is in progress.")
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    public ConversationBusyException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and the failure that caused it.</summary>
    public ConversationBusyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>A continuation id from the wire cannot be used as a Cosmos DB id.</summary>
public sealed class InvalidSessionIdException : Exception
{
    /// <summary>Creates the exception with the default message.</summary>
    public InvalidSessionIdException()
        : base("The session id is not valid.")
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    public InvalidSessionIdException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and the failure that caused it.</summary>
    public InvalidSessionIdException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
