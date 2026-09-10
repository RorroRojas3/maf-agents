using FluentValidation;

namespace Andes.Agents.Service.Options;

/// <summary>Settings of the persisted conversation history.</summary>
public sealed class SessionsOptions
{
    /// <summary>Configuration section these options bind from.</summary>
    public const string SectionName = "Sessions";

    /// <summary>Gets or sets how many of the most recent messages are replayed to the model on each turn.</summary>
    public int MaxHistoryMessages { get; set; } = 50;
}

/// <summary>Rules for <see cref="SessionsOptions"/>.</summary>
// Public because the composition root registers it; every other options validator here is internal.
public sealed class SessionsOptionsValidator : AbstractValidator<SessionsOptions>
{
    /// <summary>Creates the validator.</summary>
    public SessionsOptionsValidator() =>
        RuleFor(options => options.MaxHistoryMessages).InclusiveBetween(1, 1000);
}
