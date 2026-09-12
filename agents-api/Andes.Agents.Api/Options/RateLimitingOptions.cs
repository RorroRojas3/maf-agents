using FluentValidation;

namespace Andes.Agents.Api.Options;

/// <summary>Per-caller budget for agent turns.</summary>
public sealed class RateLimitingOptions
{
    /// <summary>Configuration section these options bind from.</summary>
    public const string SectionName = "RateLimiting";

    /// <summary>Gets or sets how many agent turns one caller may start per minute.</summary>
    public int PermitPerMinute { get; set; } = 30;
}

internal sealed class RateLimitingOptionsValidator : AbstractValidator<RateLimitingOptions>
{
    public RateLimitingOptionsValidator() =>
        RuleFor(options => options.PermitPerMinute).InclusiveBetween(1, 10_000);
}
