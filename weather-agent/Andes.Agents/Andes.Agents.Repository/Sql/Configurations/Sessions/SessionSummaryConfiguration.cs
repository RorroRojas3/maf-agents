using Andes.Agents.Entity.Agents;
using Andes.Agents.Entity.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Andes.Agents.Repository.Sql.Configurations.Sessions;

internal sealed class SessionSummaryConfiguration : IEntityTypeConfiguration<SessionSummary>
{
    public void Configure(EntityTypeBuilder<SessionSummary> builder)
    {
        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_Session_Counts",
                "[MessageCount] >= 0 AND [InputTokens] >= 0 AND [CachedInputTokens] >= 0 AND [OutputTokens] >= 0 AND [ReasoningTokens] >= 0 AND [TotalTokens] >= 0");
            table.HasCheckConstraint("CK_Session_EstimatedCost", "[EstimatedCost] >= 0");
        });

        builder.HasOne<Agent>().WithMany().HasForeignKey(summary => summary.AgentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Model>().WithMany().HasForeignKey(summary => summary.ModelId).OnDelete(DeleteBehavior.Restrict);

        // One row per session incarnation: an id reused after its conversation was deleted starts a new row.
        builder.HasIndex(summary => new { summary.UserId, summary.SessionId, summary.DateCreated }).IsUnique();

        // Reporting periods.
        builder.HasIndex(summary => summary.DateCreated);
    }
}
