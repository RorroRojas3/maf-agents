using Andes.Agents.Common.Enums;
using Andes.Agents.Entity.Base;

namespace Andes.Agents.Entity.Policies;

/// <summary>An insurance policy as stored in <c>[Core].[Policy]</c>.</summary>
public sealed class Policy : BaseEntity
{
    /// <summary>Gets or sets the business key printed on policy documents; unique.</summary>
    public required string PolicyNumber { get; set; }

    /// <summary>Gets or sets the code of the product the policy was sold under.</summary>
    public required string ProductCode { get; set; }

    /// <summary>Gets or sets where the policy is in its lifecycle.</summary>
    public PolicyStatuses Status { get; set; }

    /// <summary>Gets or sets the holder's id in the system of record.</summary>
    public required string HolderReference { get; set; }

    /// <summary>Gets or sets the holder's name.</summary>
    /// <remarks>Personal data: it may be stored and returned to its owner, never logged.</remarks>
    public required string HolderName { get; set; }

    /// <summary>Gets or sets the first day of cover.</summary>
    public DateOnly EffectiveDate { get; set; }

    /// <summary>Gets or sets the day cover ends; always after <see cref="EffectiveDate"/>.</summary>
    public DateOnly ExpirationDate { get; set; }

    /// <summary>Gets or sets the premium for the whole term, in <see cref="CurrencyCode"/>; never negative.</summary>
    public decimal PremiumAmount { get; set; }

    /// <summary>Gets or sets the sum insured, in <see cref="CurrencyCode"/>; always positive.</summary>
    public decimal CoverageAmount { get; set; }

    /// <summary>Gets or sets the upper-case ISO 4217 code both amounts are denominated in.</summary>
    public required string CurrencyCode { get; set; }
}
