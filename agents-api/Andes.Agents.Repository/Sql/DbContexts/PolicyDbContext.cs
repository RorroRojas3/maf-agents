using Andes.Agents.Entity.Agents;
using Andes.Agents.Entity.Policies;
using Andes.Agents.Entity.Sessions;
using Andes.Agents.Repository.Sql.Configurations.Agents;
using Andes.Agents.Repository.Sql.Configurations.Policies;
using Andes.Agents.Repository.Sql.Configurations.Sessions;
using Microsoft.EntityFrameworkCore;

namespace Andes.Agents.Repository.Sql.DbContexts;

/// <summary>The application's SQL Server database: policies, the agent catalog and session usage summaries.</summary>
/// <remarks>Connection resiliency is on: an explicit transaction must run inside <c>Database.CreateExecutionStrategy()</c>, or it throws.</remarks>
public sealed class PolicyDbContext(DbContextOptions<PolicyDbContext> options) : DbContext(options)
{
    // Schema of the migrations history; the [Table] attributes of Policy and SessionSummary repeat it, so one grant covers both.
    internal const string Schema = "Core";

    /// <summary>Gets the stored policies.</summary>
    public DbSet<Policy> Policies => Set<Policy>();

    /// <summary>Gets the hosted agents.</summary>
    public DbSet<Agent> Agents => Set<Agent>();

    /// <summary>Gets the model deployments and their prices.</summary>
    public DbSet<Model> Models => Set<Model>();

    /// <summary>Gets the periods during which each agent runs on a model.</summary>
    public DbSet<AgentModelMapping> AgentModelMappings => Set<AgentModelMapping>();

    /// <summary>Gets the per-session usage summaries.</summary>
    public DbSet<SessionSummary> SessionSummaries => Set<SessionSummary>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Named, not swept from the assembly, so a later context's configurations never leak into this model.
        modelBuilder.ApplyConfiguration(new PolicyConfiguration());
        modelBuilder.ApplyConfiguration(new AgentConfiguration());
        modelBuilder.ApplyConfiguration(new ModelConfiguration());
        modelBuilder.ApplyConfiguration(new AgentModelMappingConfiguration());
        modelBuilder.ApplyConfiguration(new SessionSummaryConfiguration());
    }
}
