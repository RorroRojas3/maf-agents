using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Andes.Agents.Entity.Base;
using Microsoft.EntityFrameworkCore;

namespace Andes.Agents.Entity.Agents;

/// <summary>A model deployment and its list prices.</summary>
[Table("Model", Schema = "Core.Ref")]
public sealed class Model : BaseEntity
{
    /// <summary>Gets or sets the display name, such as GPT 5.6 Luna.</summary>
    [StringLength(100)]
    public required string Name { get; set; }

    /// <summary>Gets or sets the name of the deployment the chat client calls; unique.</summary>
    [StringLength(64)]
    public required string DeploymentName { get; set; }

    /// <summary>Gets or sets the model's name at the provider.</summary>
    [StringLength(64)]
    public required string ProviderName { get; set; }

    /// <summary>Gets or sets the US dollar price of one million uncached input tokens.</summary>
    [Precision(19, 9)]
    public decimal InputPricePerMillionTokens { get; set; }

    /// <summary>Gets or sets the US dollar price of one million input tokens read from the provider's cache.</summary>
    [Precision(19, 9)]
    public decimal CachedInputPricePerMillionTokens { get; set; }

    /// <summary>Gets or sets the US dollar price of one million output tokens, reasoning tokens included.</summary>
    [Precision(19, 9)]
    public decimal OutputPricePerMillionTokens { get; set; }
}
