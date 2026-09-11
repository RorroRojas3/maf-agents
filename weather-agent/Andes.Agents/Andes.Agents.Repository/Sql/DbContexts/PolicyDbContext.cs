using Andes.Agents.Entity.Policies;
using Andes.Agents.Repository.Sql.Configurations.Policies;
using Microsoft.EntityFrameworkCore;

namespace Andes.Agents.Repository.Sql.DbContexts;

/// <summary>The policy database.</summary>
/// <remarks>Connection resiliency is on: an explicit transaction must run inside <c>Database.CreateExecutionStrategy()</c>, or it throws.</remarks>
public sealed class PolicyDbContext(DbContextOptions<PolicyDbContext> options) : DbContext(options)
{
    internal const string Schema = "Core";

    /// <summary>Gets the stored policies.</summary>
    public DbSet<Policy> Policies => Set<Policy>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Named, not swept from the assembly, so a later context's configurations never leak into this model.
        modelBuilder.ApplyConfiguration(new PolicyConfiguration());
    }
}
