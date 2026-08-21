namespace ImageAgent.Configuration;

/// <summary>
/// Connection settings for a Microsoft Foundry resource reached through its OpenAI-compatible
/// <c>/openai/v1/</c> route.
/// </summary>
internal sealed class FoundryOptions
{
    /// <summary>Configuration section these options bind from.</summary>
    public const string SectionName = "Foundry";

    /// <summary>
    /// Gets or sets the Foundry endpoint, which must end in <c>/openai/v1/</c>. The v1 route uses
    /// implicit versioning, so no <c>api-version</c> is sent.
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Gets or sets the Foundry API key.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the deployment name of the chat model that drives the agent and reads images.
    /// It must support tool calling and image input.
    /// </summary>
    public string ChatModel { get; set; } = string.Empty;

    /// <summary>Gets or sets the deployment name of the image model used to draw and edit pixels.</summary>
    public string ImageModel { get; set; } = string.Empty;

    /// <summary>Gets the endpoint as an absolute URI. Only valid once validation has passed.</summary>
    public Uri EndpointUri => new(Endpoint, UriKind.Absolute);

    /// <summary>Collects every configuration problem, so one run reports all of them rather than the first.</summary>
    /// <returns>Human-readable problem descriptions; empty when the options are usable.</returns>
    public IReadOnlyList<string> Validate()
    {
        List<string> problems = [];

        if (string.IsNullOrWhiteSpace(Endpoint))
        {
            problems.Add($"{SectionName}:Endpoint is missing.");
        }
        else if (!Uri.TryCreate(Endpoint, UriKind.Absolute, out Uri? parsed))
        {
            problems.Add($"{SectionName}:Endpoint is not an absolute URI: '{Endpoint}'.");
        }
        else if (!parsed.AbsolutePath.EndsWith("/openai/v1/", StringComparison.OrdinalIgnoreCase))
        {
            // Without the trailing v1 segment the SDK silently builds requests against a route that
            // answers 404, which reads as "model not found" rather than "wrong endpoint".
            problems.Add($"{SectionName}:Endpoint must end with '/openai/v1/' — got '{Endpoint}'.");
        }
        else if (parsed.AbsolutePath.Contains("/api/projects/", StringComparison.OrdinalIgnoreCase))
        {
            // A project-scoped route serves /responses but not /images/*, so the agent would start
            // and chat happily and then 404 the moment it reached for an image tool. Caught here
            // rather than mid-conversation.
            problems.Add(
                $"{SectionName}:Endpoint is a project-scoped route, which does not serve the image APIs. "
                + "Drop the '/api/projects/<project>' segment and use the resource-level endpoint: "
                + $"{parsed.GetLeftPart(UriPartial.Authority)}/openai/v1/");
        }

        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            problems.Add($"{SectionName}:ApiKey is missing.");
        }

        if (string.IsNullOrWhiteSpace(ChatModel))
        {
            problems.Add($"{SectionName}:ChatModel is missing.");
        }

        if (string.IsNullOrWhiteSpace(ImageModel))
        {
            problems.Add($"{SectionName}:ImageModel is missing.");
        }

        return problems;
    }
}
