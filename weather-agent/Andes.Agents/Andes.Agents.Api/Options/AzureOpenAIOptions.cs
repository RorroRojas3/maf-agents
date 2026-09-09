namespace Andes.Agents.Api.Options;

/// <summary>The deployment the agent runs on, reached through the Responses API on the resource's v1 route.</summary>
public sealed class AzureOpenAIOptions : OpenAIEndpointOptions
{
    /// <summary>Configuration section these options bind from.</summary>
    public const string SectionName = "AzureOpenAI";
}
