using FluentValidation;
using Microsoft.Data.SqlClient;

namespace Andes.Agents.Repository.Sql.Options;

/// <summary>Connection and resiliency settings for the SQL Server database that stores policies.</summary>
public sealed class SqlDbOptions
{
    /// <summary>Configuration section these options bind from.</summary>
    public const string SectionName = "SqlDb";

    /// <summary>Gets or sets the connection string; its <c>Authentication</c> keyword chooses SQL or Microsoft Entra ID sign-in.</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Gets or sets how long a command may run before it is abandoned, in seconds.</summary>
    public int CommandTimeoutSeconds { get; set; } = 30;

    /// <summary>Gets or sets how many times an operation that failed with a transient error is retried.</summary>
    public int MaxRetryCount { get; set; } = 6;

    /// <summary>Gets or sets the longest delay between two retries, in seconds.</summary>
    public int MaxRetryDelaySeconds { get; set; } = 30;

    /// <summary>Gets or sets whether pending migrations are applied at startup; development only.</summary>
    public bool ApplyMigrationsOnStartup { get; set; }
}

internal sealed class SqlDbOptionsValidator : AbstractValidator<SqlDbOptions>
{
    public SqlDbOptionsValidator()
    {
        // No message quotes the value: a connection string can carry a password.
        RuleFor(options => options.ConnectionString)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(NamesDatabase)
            .WithMessage($"'{SqlDbOptions.SectionName}:{nameof(SqlDbOptions.ConnectionString)}' must be a valid SQL Server connection string that names a database.");

        RuleFor(options => options.CommandTimeoutSeconds).InclusiveBetween(1, 600);
        RuleFor(options => options.MaxRetryCount).InclusiveBetween(0, 10);
        RuleFor(options => options.MaxRetryDelaySeconds).InclusiveBetween(1, 300);
    }

    private static bool NamesDatabase(string value)
    {
        try
        {
            return !string.IsNullOrWhiteSpace(new SqlConnectionStringBuilder(value).InitialCatalog);
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or OverflowException or NotSupportedException)
        {
            return false;
        }
    }
}
