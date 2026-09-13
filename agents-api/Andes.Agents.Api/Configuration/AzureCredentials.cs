using Andes.Agents.Api.Options;
using Azure.Core;
using Azure.Identity;

namespace Andes.Agents.Api.Configuration;

internal static class AzureCredentials
{
    // Built from raw configuration because Key Vault needs it before the service provider exists.
    public static TokenCredential Create(IConfiguration configuration)
    {
        string? clientId = configuration[$"{AzureOptions.SectionName}:{nameof(AzureOptions.ManagedIdentityClientId)}"];

        return new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            ManagedIdentityClientId = string.IsNullOrWhiteSpace(clientId) ? null : clientId,
        });
    }
}
