using System.ComponentModel.DataAnnotations;

namespace Andes.Agents.Api.Options;

/// <summary>What the published A2A agent card says about this host.</summary>
public sealed class AgentCardOptions
{
    /// <summary>Configuration section these options bind from.</summary>
    public const string SectionName = "AgentCard";

    /// <summary>Gets or sets the address clients reach this host at; the card's interface URLs are built on it.</summary>
    [Required]
    public Uri? PublicBaseUrl { get; set; }

    /// <summary>Gets or sets the version the card advertises.</summary>
    [Required]
    public string Version { get; set; } = "1.0.0";
}
