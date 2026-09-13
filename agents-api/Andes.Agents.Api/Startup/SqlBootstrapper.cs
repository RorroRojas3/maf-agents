using Andes.Agents.Repository.Sql.Options;
using Andes.Agents.Repository.Sql.Provisioning;
using Microsoft.Extensions.Options;

namespace Andes.Agents.Api.Startup;

internal static class SqlBootstrapper
{
    public static async Task MigrateSqlDatabaseAsync(this WebApplication app, CancellationToken cancellationToken = default)
    {
        SqlDbOptions options = app.Services.GetRequiredService<IOptions<SqlDbOptions>>().Value;

        if (!options.ApplyMigrationsOnStartup)
        {
            return;
        }

        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<SqlSchemaMigrator>().MigrateAsync(cancellationToken);
    }
}
