using Andes.Agents.Entity.Agents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Andes.Agents.Repository.Sql.Configurations.Agents;

internal sealed class ModelConfiguration : IEntityTypeConfiguration<Model>
{
    public void Configure(EntityTypeBuilder<Model> builder)
    {
        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_Model_Prices",
                "[InputPricePerMillionTokens] >= 0 AND [CachedInputPricePerMillionTokens] >= 0 AND [OutputPricePerMillionTokens] >= 0");
        });

        builder.HasIndex(model => model.DeploymentName).IsUnique();
    }
}
