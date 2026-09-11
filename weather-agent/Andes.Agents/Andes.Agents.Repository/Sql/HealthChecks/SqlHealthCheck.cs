using Andes.Agents.Repository.Sql.DbContexts;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Andes.Agents.Repository.Sql.HealthChecks;

// Not Database.CanConnectAsync: that runs inside the retrying execution strategy and its own one-minute login-retry loop.
// An unpooled connection answers for the server as it is now, not for a session the pool kept alive.
internal sealed class SqlHealthCheck(PolicyDbContext ctx) : IHealthCheck
{
    private const int _commandTimeoutSeconds = 5;

    private readonly PolicyDbContext _ctx = ctx;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        SqlConnectionStringBuilder probe = new(_ctx.Database.GetConnectionString())
        {
            Pooling = false,
        };

        // SqlClient keeps a cancelled open running until Connect Timeout, and a pooled open ignored the token outright under
        // a stopped container (15 s default); the registration's timeout bounds the attempt either way.
        if (context.Registration.Timeout > TimeSpan.Zero)
        {
            probe.ConnectTimeout = Math.Max(1, (int)context.Registration.Timeout.TotalSeconds);
        }

        try
        {
            SqlConnection connection = new(probe.ConnectionString);

            await using (connection.ConfigureAwait(false))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

                using SqlCommand command = new("SELECT 1", connection) { CommandTimeout = _commandTimeoutSeconds };
                await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            }

            return HealthCheckResult.Healthy();
        }
        catch (SqlException exception)
        {
            return HealthCheckResult.Unhealthy("SQL Server is unreachable or the database does not exist.", exception);
        }
    }
}
