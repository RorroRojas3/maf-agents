namespace Andes.Agents.Service.Exceptions;

/// <summary>The requested resource does not exist, or belongs to another caller.</summary>
public sealed class NotFoundException : Exception
{
    /// <summary>Creates the exception with the default message.</summary>
    public NotFoundException()
        : base("The requested resource was not found.")
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    public NotFoundException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and the failure that caused it.</summary>
    public NotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
