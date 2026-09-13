namespace Andes.Agents.Api.Options;

/// <summary>How the application authenticates to Azure services that accept Entra ID.</summary>
public sealed class AzureOptions
{
    /// <summary>Configuration section these options bind from.</summary>
    public const string SectionName = "Azure";

    /// <summary>Gets or sets the client id of a user-assigned managed identity; blank selects the system-assigned one or local developer credentials.</summary>
    public string? ManagedIdentityClientId { get; set; }
}
