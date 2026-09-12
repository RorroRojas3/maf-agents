using Andes.Agents.Common.Constants;
using Andes.Agents.Entity.Agents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Andes.Agents.Repository.Sql.Configurations.Agents;

internal sealed class AgentConfiguration : IEntityTypeConfiguration<Agent>
{
    // Fixed, because a seed value that changes is written into the next migration as UpdateData.
    private static readonly DateTimeOffset _seededAt = new(2026, 9, 11, 0, 0, 0, TimeSpan.Zero);

    public void Configure(EntityTypeBuilder<Agent> builder)
    {
        builder.HasIndex(agent => agent.Name).IsUnique();

        builder.HasData(new Agent
        {
            Id = AgentIds.Weather,
            Name = AgentNames.Weather,
            DateCreated = _seededAt,
            DateModified = _seededAt,
        });
    }
}
