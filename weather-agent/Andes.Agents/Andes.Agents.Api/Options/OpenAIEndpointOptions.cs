using System.ComponentModel.DataAnnotations;

namespace Andes.Agents.Api.Options;

/// <summary>Connection settings shared by every client that reaches a Microsoft Azure AI Foundry resource through its OpenAI-compatible v1 route.</summary>
public abstract class OpenAIEndpointOptions
{
    /// <summary>The path an OpenAI-compatible Foundry endpoint ends with.</summary>
    public const string V1RoutePath = "/openai/v1/";

    /// <summary>Gets or sets the resource endpoint, which must end in <c>/openai/v1/</c>.</summary>
    [Required]
    [Url]
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Gets or sets the API key of the resource.</summary>
    [Required]
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the name of the deployment to call on that resource.</summary>
    [Required]
    public string Model { get; set; } = string.Empty;

    /// <summary>The endpoint as an absolute URI; only callable once validation has passed.</summary>
    /// <remarks>A method, not a property: options validation reads every property, and this throws on the blank default.</remarks>
    public Uri GetEndpointUri() => new(Endpoint, UriKind.Absolute);

    /// <summary>Whether the endpoint names the v1 route; any other path answers 404 to every model request.</summary>
    public bool HasV1Route() =>
        Uri.TryCreate(Endpoint, UriKind.Absolute, out Uri? parsed)
        && parsed.AbsolutePath.EndsWith(V1RoutePath, StringComparison.OrdinalIgnoreCase);
}
