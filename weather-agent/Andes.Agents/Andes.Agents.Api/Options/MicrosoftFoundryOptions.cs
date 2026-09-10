namespace Andes.Agents.Api.Options;

/// <summary>The deployment used for plain chat completions, on the same resource and v1 route as the agent's.</summary>
/// <remarks>Optional: a blank endpoint registers no completions client rather than failing startup.</remarks>
public sealed class MicrosoftFoundryOptions : OpenAIEndpointOptions
{
    /// <summary>Configuration section these options bind from.</summary>
    public const string SectionName = "MicrosoftFoundry";
}

internal sealed class MicrosoftFoundryOptionsValidator : OpenAIEndpointOptionsValidator<MicrosoftFoundryOptions>
{
    public MicrosoftFoundryOptionsValidator()
        : base(MicrosoftFoundryOptions.SectionName)
    {
    }
}
