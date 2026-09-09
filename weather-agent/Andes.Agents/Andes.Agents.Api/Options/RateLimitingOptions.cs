using System.ComponentModel.DataAnnotations;

namespace Andes.Agents.Api.Options;

/// <summary>Per-caller budget for agent turns.</summary>
public sealed class RateLimitingOptions
{
    /// <summary>Configuration section these options bind from.</summary>
    public const string SectionName = "RateLimiting";

    /// <summary>Gets or sets how many agent turns one caller may start per minute.</summary>
    [Range(1, 10_000)]
    public int PermitPerMinute { get; set; } = 30;
}
