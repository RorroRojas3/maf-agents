using Andes.Agents.Common.Validation;
using Andes.Agents.Repository.Sql.DbContexts;
using Andes.Agents.Repository.Sql.HealthChecks;
using Andes.Agents.Repository.Sql.Interceptors;
using Andes.Agents.Repository.Sql.Options;
using Andes.Agents.Repository.Sql.Provisioning;
using FluentValidation;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Andes.Agents.Repository.Sql;

/// <summary>Registers the SQL Server store and the policy database built on it.</summary>
public static class SqlPersistenceConfiguration
{
    private const string _applicationName = "Andes.Agents";

    // SQL Server 2025 and Azure SQL. Without it EF assumes 150 (SQL Server 2019) and avoids newer T-SQL.
    private const int _compatibilityLevel = 170;

    /// <summary>Binds the SQL Server section and default connection string, and registers the pooled policy context and its schema migrator.</summary>
    public static IServiceCollection AddAndesSqlPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IValidator<SqlDbOptions>, SqlDbOptionsValidator>();

        services
            .AddOptions<SqlDbOptions>()
            .Bind(configuration.GetSection(SqlDbOptions.SectionName))
            // App Service injects its connection strings (SQLAZURECONNSTR_<name>) under ConnectionStrings, never inside a section.
            .Configure(options => options.ConnectionString = configuration.GetConnectionString(SqlDbOptions.ConnectionStringName) ?? string.Empty)
            .ValidateWithFluentValidation()
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<AuditTimestampInterceptor>();

        services.AddDbContextPool<PolicyDbContext>((provider, builder) =>
        {
            SqlDbOptions options = provider.GetRequiredService<IOptions<SqlDbOptions>>().Value;

            ConfigureSqlServer(builder, options).AddInterceptors(provider.GetRequiredService<AuditTimestampInterceptor>());
        });

        services.AddScoped<SqlSchemaMigrator>();

        return services;
    }

    /// <summary>Adds a readiness probe that runs a trivial query against the policy database.</summary>
    public static IHealthChecksBuilder AddAndesSqlHealthCheck(this IHealthChecksBuilder builder, string name, TimeSpan timeout, params string[] tags)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddCheck<SqlHealthCheck>(name, tags: tags, timeout: timeout);
    }

    // Shared with the design-time factory, so migrations are generated against the provider settings the app runs with.
    internal static DbContextOptionsBuilder ConfigureSqlServer(DbContextOptionsBuilder builder, SqlDbOptions options) =>
        builder
            .UseSqlServer(WithApplicationName(options.ConnectionString), sql => sql
                .UseCompatibilityLevel(_compatibilityLevel)
                .MigrationsHistoryTable(HistoryRepository.DefaultTableName, PolicyDbContext.Schema)
                .CommandTimeout(options.CommandTimeoutSeconds)
                .EnableRetryOnFailure(options.MaxRetryCount, TimeSpan.FromSeconds(options.MaxRetryDelaySeconds), errorNumbersToAdd: null))
            // EF logs these at Error with the SqlException, whose text quotes a duplicate key (2601, 2627) or a truncated
            // value (2628). The exception still reaches the caller, which decides what a sink may see.
            .ConfigureWarnings(warnings => warnings.Log(
                (CoreEventId.SaveChangesFailed, LogLevel.Debug),
                (CoreEventId.QueryIterationFailed, LogLevel.Debug),
                (RelationalEventId.CommandError, LogLevel.Debug)));

    private static string WithApplicationName(string connectionString)
    {
        SqlConnectionStringBuilder builder = new(connectionString);

        // SqlClient's default name means none was given; a name the operator chose is kept.
        if (builder.ApplicationName == new SqlConnectionStringBuilder().ApplicationName)
        {
            builder.ApplicationName = _applicationName;
        }

        return builder.ConnectionString;
    }
}
