using Andes.Agents.Repository.Sql.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Andes.Agents.Repository.Sql.DbContexts;

// Lets `dotnet ef` build the context without booting the Api host, which would demand every provider's configuration.
// `migrations add` and `script` never open the connection; `database update` and a bundle take `--connection`.
internal sealed class PolicyDbContextDesignTimeFactory : IDesignTimeDbContextFactory<PolicyDbContext>
{
    private const string _connectionStringVariable = $"ConnectionStrings__{SqlDbOptions.ConnectionStringName}";
    private const string _placeholderConnectionString = "Server=localhost,1433;Database=AndesAgents;Integrated Security=true;TrustServerCertificate=true";

    public PolicyDbContext CreateDbContext(string[] args)
    {
        string? configured = Environment.GetEnvironmentVariable(_connectionStringVariable);

        SqlDbOptions options = new()
        {
            ConnectionString = string.IsNullOrWhiteSpace(configured) ? _placeholderConnectionString : configured,
        };

        DbContextOptionsBuilder<PolicyDbContext> builder = new();
        SqlPersistenceConfiguration.ConfigureSqlServer(builder, options);

        return new PolicyDbContext(builder.Options);
    }
}
