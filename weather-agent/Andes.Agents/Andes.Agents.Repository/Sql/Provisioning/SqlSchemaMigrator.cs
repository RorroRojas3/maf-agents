using Andes.Agents.Repository.Sql.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace Andes.Agents.Repository.Sql.Provisioning;

/// <summary>Brings the policy database up to the latest migration.</summary>
/// <remarks>
/// Development only: the production login holds no DDL rights, so production applies
/// <c>dotnet ef migrations script --idempotent</c> or a migrations bundle from its pipeline.
/// </remarks>
public sealed class SqlSchemaMigrator(PolicyDbContext dbContext)
{
    private readonly PolicyDbContext _dbContext = dbContext;

    /// <summary>Creates the database when it is missing and applies every pending migration.</summary>
    public Task MigrateAsync(CancellationToken cancellationToken) => _dbContext.Database.MigrateAsync(cancellationToken);
}
