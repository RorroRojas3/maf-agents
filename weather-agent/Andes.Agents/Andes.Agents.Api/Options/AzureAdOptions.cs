using System.ComponentModel.DataAnnotations;

namespace Andes.Agents.Api.Options;

/// <summary>The Microsoft Entra ID registration this API validates tokens against.</summary>
/// <remarks>
/// Microsoft.Identity.Web binds the same section for the bearer scheme; these options carry only what the
/// application itself decides with — the authorization policy and the startup guard below it.
/// </remarks>
public sealed class AzureAdOptions
{
    /// <summary>Configuration section these options bind from.</summary>
    public const string SectionName = "AzureAd";

    /// <summary>Gets or sets the cloud the tenant lives in.</summary>
    public string Instance { get; set; } = "https://login.microsoftonline.com/";

    /// <summary>Gets or sets the tenant that issues accepted tokens.</summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>Gets or sets the full authority URL, which Microsoft.Identity.Web accepts in place of an instance and tenant.</summary>
    public string? Authority { get; set; }

    /// <summary>Gets or sets the application id of this API's registration.</summary>
    [Required]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Gets or sets the accepted audience; blank falls back to the client id.</summary>
    public string? Audience { get; set; }

    /// <summary>Gets or sets the space-separated scopes a delegated caller must hold.</summary>
    public string? Scopes { get; set; }

    /// <summary>Gets or sets the space-separated app roles a daemon caller must hold.</summary>
    public string? AppPermissions { get; set; }

    /// <summary>Gets or sets whether any token issued for this audience is accepted, with no scope or role check.</summary>
    public bool AllowAnyAuthenticatedCaller { get; set; }

    /// <summary>Whether the token authority is resolvable, by either form Microsoft.Identity.Web supports.</summary>
    public bool HasAuthority() =>
        !string.IsNullOrWhiteSpace(Authority)
        || (!string.IsNullOrWhiteSpace(Instance) && !string.IsNullOrWhiteSpace(TenantId));

    /// <summary>The scopes a delegated caller must hold.</summary>
    public string[] GetScopes() => Split(Scopes);

    /// <summary>The app roles a daemon caller must hold.</summary>
    public string[] GetAppPermissions() => Split(AppPermissions);

    /// <summary>
    /// Whether the configuration states what a caller must hold. Any application in the tenant can obtain a
    /// token for this audience, so a scope or role is what proves one was granted access.
    /// </summary>
    public bool HasCallerRequirement() =>
        AllowAnyAuthenticatedCaller || GetScopes().Length > 0 || GetAppPermissions().Length > 0;

    private static string[] Split(string? value) =>
        value?.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
}
