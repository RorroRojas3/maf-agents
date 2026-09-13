using Andes.Agents.Common.Enums;
using Andes.Agents.Entity.Policies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Andes.Agents.Repository.Sql.Configurations.Policies;

internal sealed class PolicyConfiguration : IEntityTypeConfiguration<Policy>
{
    // Codes are compared by code point: under the default case-insensitive collation 'active' and 'usd' would pass.
    private const string _binaryCollation = "Latin1_General_100_BIN2";

    public void Configure(EntityTypeBuilder<Policy> builder)
    {
        string statusNames = string.Join(", ", Enum.GetNames<PolicyStatuses>().Select(name => $"N'{name}'"));

        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_Policy_Status", $"[Status] COLLATE {_binaryCollation} IN ({statusNames})");
            table.HasCheckConstraint("CK_Policy_CoverageWindow", "[ExpirationDate] > [EffectiveDate]");
            table.HasCheckConstraint("CK_Policy_PremiumAmount", "[PremiumAmount] >= 0");
            table.HasCheckConstraint("CK_Policy_CoverageAmount", "[CoverageAmount] > 0");
            table.HasCheckConstraint("CK_Policy_CurrencyCode", $"[CurrencyCode] COLLATE {_binaryCollation} LIKE N'[A-Z][A-Z][A-Z]'");
        });

        // The length describes the converted string column, and [StringLength] on an enum throws under DataAnnotations validation.
        builder.Property(policy => policy.Status).HasConversion<string>().HasMaxLength(16);

        builder.HasIndex(policy => policy.PolicyNumber).IsUnique();

        // A holder's policy list, answered from the index without key lookups.
        builder.HasIndex(policy => policy.HolderReference)
            .IncludeProperties(policy => new { policy.PolicyNumber, policy.Status, policy.EffectiveDate, policy.ExpirationDate });

        // Renewal and expiry sweeps. Not filtered on one status: a filtered index is skipped when the status is a parameter.
        builder.HasIndex(policy => new { policy.Status, policy.ExpirationDate });
    }
}
