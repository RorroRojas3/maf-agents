using FluentValidation;

namespace Andes.Agents.Api.Options;

/// <summary>Whether the OpenAPI document and its Scalar reference UI are served, and how Scalar signs a caller in.</summary>
public sealed class ApiDocsOptions
{
    /// <summary>Configuration section these options bind from.</summary>
    public const string SectionName = "ApiDocs";

    /// <summary>Gets or sets whether the document and UI are mapped; Development maps them regardless.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets the public client Scalar signs in with; blank leaves only a pasted bearer token.</summary>
    /// <remarks>Each host's <c>/scalar/</c> URL must be registered on that client as a Single-page application redirect URI.</remarks>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Gets or sets the space-separated, fully qualified scopes Scalar requests, such as <c>api://{client-id}/access_as_user</c>.</summary>
    public string? Scopes { get; set; }

    /// <summary>Whether Scalar signs callers in through Microsoft Entra ID.</summary>
    public bool HasSignIn() => !string.IsNullOrWhiteSpace(ClientId);

    /// <summary>The scopes Scalar requests.</summary>
    public string[] GetScopes() =>
        [.. (Scopes?.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? []).Distinct()];
}

internal sealed class ApiDocsOptionsValidator : AbstractValidator<ApiDocsOptions>
{
    public ApiDocsOptionsValidator()
    {
        RuleFor(options => options.Scopes)
            .NotEmpty()
            .When(options => options.HasSignIn())
            .WithMessage(
                $"'{ApiDocsOptions.SectionName}:{nameof(ApiDocsOptions.Scopes)}' must name the scopes Scalar requests "
                + $"when '{ApiDocsOptions.SectionName}:{nameof(ApiDocsOptions.ClientId)}' is set.");
    }
}
