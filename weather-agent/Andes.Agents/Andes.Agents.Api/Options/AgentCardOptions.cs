using FluentValidation;

namespace Andes.Agents.Api.Options;

/// <summary>What the published A2A agent card says about this host.</summary>
public sealed class AgentCardOptions
{
    /// <summary>Configuration section these options bind from.</summary>
    public const string SectionName = "AgentCard";

    /// <summary>Gets or sets the address clients reach this host at; the card's interface URLs are built on it.</summary>
    public Uri? PublicBaseUrl { get; set; }

    /// <summary>Gets or sets the version the card advertises.</summary>
    public string Version { get; set; } = "1.0.0";
}

internal sealed class AgentCardOptionsValidator : AbstractValidator<AgentCardOptions>
{
    public AgentCardOptionsValidator()
    {
        RuleFor(options => options.PublicBaseUrl)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .Must(url => url!.IsAbsoluteUri)
            .WithMessage($"'{AgentCardOptions.SectionName}:{{PropertyName}}' must be an absolute URL.");

        RuleFor(options => options.Version).NotEmpty();
    }
}
