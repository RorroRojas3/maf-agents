using FluentValidation;

namespace Andes.Agents.Api.Options;

/// <summary>Connection settings shared by every client that reaches a Microsoft Azure AI Foundry resource through its OpenAI-compatible v1 route.</summary>
public abstract class OpenAIEndpointOptions
{
    /// <summary>The path an OpenAI-compatible Foundry endpoint ends with.</summary>
    public const string V1RoutePath = "/openai/v1/";

    /// <summary>Gets or sets the resource endpoint, which must end in <c>/openai/v1/</c>.</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Gets or sets the API key of the resource.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the name of the deployment to call on that resource.</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>The endpoint as an absolute URI; only callable once validation has passed.</summary>
    public Uri GetEndpointUri() => new(Endpoint, UriKind.Absolute);

    /// <summary>Whether the endpoint names the v1 route; any other path answers 404 to every model request.</summary>
    public bool HasV1Route() =>
        Uri.TryCreate(Endpoint, UriKind.Absolute, out Uri? parsed)
        && parsed.AbsolutePath.EndsWith(V1RoutePath, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Shared endpoint rules; each provider subclasses this so failures name its own section.</summary>
/// <typeparam name="TOptions">The concrete provider options being validated.</typeparam>
internal abstract class OpenAIEndpointOptionsValidator<TOptions> : AbstractValidator<TOptions>
    where TOptions : OpenAIEndpointOptions
{
    protected OpenAIEndpointOptionsValidator(string sectionName)
    {
        RuleFor(options => options.Endpoint)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(IsHttpUrl)
            .WithMessage($"'{sectionName}:{{PropertyName}}' must be an absolute http or https URL.")
            .Must((options, _) => options.HasV1Route())
            .WithMessage($"'{sectionName}:{{PropertyName}}' must end with '{OpenAIEndpointOptions.V1RoutePath}'.");

        RuleFor(options => options.ApiKey).NotEmpty();
        RuleFor(options => options.Model).NotEmpty();
    }

    // Uri.TryCreate alone accepts file:// and, on Windows, a bare drive path; the client needs http(s).
    private static bool IsHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out Uri? parsed)
        && (parsed.Scheme == Uri.UriSchemeHttps || parsed.Scheme == Uri.UriSchemeHttp);
}
