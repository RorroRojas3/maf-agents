namespace Andes.Agents.Service.Exceptions;

/// <summary>The caller is authenticated but may not perform the operation.</summary>
public sealed class ForbiddenException : Exception
{
    /// <summary>Creates the exception with the default message.</summary>
    public ForbiddenException()
        : base("The caller may not perform this operation.")
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    public ForbiddenException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and the failure that caused it.</summary>
    public ForbiddenException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
