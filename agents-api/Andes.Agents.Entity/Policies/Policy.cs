using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Andes.Agents.Common.Enums;
using Andes.Agents.Entity.Base;
using Microsoft.EntityFrameworkCore;

namespace Andes.Agents.Entity.Policies;

/// <summary>An insurance policy.</summary>
[Table("Policy", Schema = "Core")]
public sealed class Policy : BaseEntity
{
    /// <summary>Gets or sets the business key printed on policy documents; unique.</summary>
    [StringLength(32)]
    public required string PolicyNumber { get; set; }

    /// <summary>Gets or sets the code of the product the policy was sold under.</summary>
    [StringLength(20)]
    public required string ProductCode { get; set; }

    /// <summary>Gets or sets where the policy is in its lifecycle; stored by name.</summary>
    public PolicyStatuses Status { get; set; }

    /// <summary>Gets or sets the holder's id in the system of record.</summary>
    [StringLength(64)]
    public required string HolderReference { get; set; }

    /// <summary>Gets or sets the holder's name.</summary>
    /// <remarks>Personal data: it may be stored and returned to its owner, never logged.</remarks>
    [StringLength(200)]
    public required string HolderName { get; set; }

    /// <summary>Gets or sets the first day of cover.</summary>
    public DateOnly EffectiveDate { get; set; }

    /// <summary>Gets or sets the day cover ends; always after <see cref="EffectiveDate"/>.</summary>
    public DateOnly ExpirationDate { get; set; }

    /// <summary>Gets or sets the premium for the whole term, in <see cref="CurrencyCode"/>; never negative.</summary>
    [Precision(19, 4)]
    public decimal PremiumAmount { get; set; }

    /// <summary>Gets or sets the sum insured, in <see cref="CurrencyCode"/>; always positive.</summary>
    [Precision(19, 4)]
    public decimal CoverageAmount { get; set; }

    /// <summary>Gets or sets the upper-case ISO 4217 code both amounts are denominated in.</summary>
    [StringLength(3)]
    public required string CurrencyCode { get; set; }
}
