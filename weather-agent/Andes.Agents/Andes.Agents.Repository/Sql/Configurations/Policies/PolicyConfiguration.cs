using Andes.Agents.Common.Constants;
using Andes.Agents.Common.Enums;
using Andes.Agents.Entity.Policies;
using Andes.Agents.Repository.Sql.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Andes.Agents.Repository.Sql.Configurations.Policies;

internal sealed class PolicyConfiguration : IEntityTypeConfiguration<Policy>
{
    private const string _tableName = "Policy";
    private const int _statusMaxLength = 16;

    // Codes are compared byte for byte: under the default case-insensitive collation 'active' and 'usd' would pass.
    private const string _binaryCollation = "Latin1_General_100_BIN2";

    public void Configure(EntityTypeBuilder<Policy> builder)
    {
        string statusNames = string.Join(", ", Enum.GetNames<PolicyStatuses>().Select(name => $"'{name}'"));

        builder.ToTable(_tableName, PolicyDbContext.Schema, table =>
        {
            table.HasCheckConstraint("CK_Policy_Status", $"[Status] COLLATE {_binaryCollation} IN ({statusNames})");
            table.HasCheckConstraint("CK_Policy_CoverageWindow", "[ExpirationDate] > [EffectiveDate]");
            table.HasCheckConstraint("CK_Policy_PremiumAmount", "[PremiumAmount] >= 0");
            table.HasCheckConstraint("CK_Policy_CoverageAmount", "[CoverageAmount] > 0");
            table.HasCheckConstraint("CK_Policy_CurrencyCode", $"[CurrencyCode] COLLATE {_binaryCollation} LIKE '[A-Z][A-Z][A-Z]'");
        });

        // EF's sequential GUIDs, not Guid.CreateVersion7(): SQL Server orders uniqueidentifier by its last six bytes
        // first, so version 7 values would scatter inserts across this clustered key.
        builder.HasKey(policy => policy.Id);

        builder.Property(policy => policy.PolicyNumber).HasMaxLength(PolicyLimits.PolicyNumberMaxLength).IsUnicode(false);
        builder.Property(policy => policy.ProductCode).HasMaxLength(PolicyLimits.ProductCodeMaxLength).IsUnicode(false);
        builder.Property(policy => policy.Status).HasConversion<string>().HasMaxLength(_statusMaxLength).IsUnicode(false);
        builder.Property(policy => policy.HolderReference).HasMaxLength(PolicyLimits.HolderReferenceMaxLength).IsUnicode(false);
        builder.Property(policy => policy.HolderName).HasMaxLength(PolicyLimits.HolderNameMaxLength);
        builder.Property(policy => policy.PremiumAmount).HasPrecision(PolicyLimits.MoneyPrecision, PolicyLimits.MoneyScale);
        builder.Property(policy => policy.CoverageAmount).HasPrecision(PolicyLimits.MoneyPrecision, PolicyLimits.MoneyScale);
        builder.Property(policy => policy.CurrencyCode).HasMaxLength(PolicyLimits.CurrencyCodeLength).IsFixedLength().IsUnicode(false);
        builder.Property(policy => policy.RowVersion).IsRowVersion();

        builder.HasIndex(policy => policy.PolicyNumber).IsUnique();

        // A holder's policy list, answered from the index without key lookups.
        builder.HasIndex(policy => policy.HolderReference)
            .IncludeProperties(policy => new { policy.PolicyNumber, policy.Status, policy.EffectiveDate, policy.ExpirationDate });

        // Renewal and expiry sweeps. Not filtered on one status: a filtered index is skipped when the status is a parameter.
        builder.HasIndex(policy => new { policy.Status, policy.ExpirationDate });
    }
}
