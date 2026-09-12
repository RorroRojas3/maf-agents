namespace Andes.Agents.Common.Constants;

/// <summary>Service keys of the keyed <c>IChatClient</c> registrations.</summary>
public static class ChatClientKeys
{
    /// <summary>The Responses API client that drives the agent.</summary>
    public const string AzureOpenAI = "azure-openai";

    /// <summary>The Chat Completions client, for callers that need a plain model call rather than an agent turn.</summary>
    public const string MicrosoftFoundry = "microsoft-foundry";
}
