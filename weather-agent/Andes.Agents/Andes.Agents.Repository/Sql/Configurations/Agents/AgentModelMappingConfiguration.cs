using Andes.Agents.Entity.Agents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Andes.Agents.Repository.Sql.Configurations.Agents;

internal sealed class AgentModelMappingConfiguration : IEntityTypeConfiguration<AgentModelMapping>
{
    public void Configure(EntityTypeBuilder<AgentModelMapping> builder)
    {
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_AgentModelMapping_ActivationWindow", "[DateDeactivated] IS NULL OR [DateDeactivated] >= [DateCreated]");
        });

        builder.HasOne<Agent>().WithMany().HasForeignKey(mapping => mapping.AgentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Model>().WithMany().HasForeignKey(mapping => mapping.ModelId).OnDelete(DeleteBehavior.Restrict);

        // One active model per agent.
        builder.HasIndex(mapping => mapping.AgentId).IsUnique().HasFilter("[DateDeactivated] IS NULL");

        // An agent's model history; the filtered index above covers only the active row.
        builder.HasIndex(mapping => new { mapping.AgentId, mapping.DateCreated });
    }
}
