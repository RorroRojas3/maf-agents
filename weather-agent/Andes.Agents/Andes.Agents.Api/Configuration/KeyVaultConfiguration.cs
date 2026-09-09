using Andes.Agents.Api.Options;
using Azure.Extensions.AspNetCore.Configuration.Secrets;

namespace Andes.Agents.Api.Configuration;

internal static class KeyVaultConfiguration
{
    // Secret names use "--" where configuration uses ":", so Foundry--ApiKey lands on Foundry:ApiKey.
    public static WebApplicationBuilder AddAndesKeyVault(this WebApplicationBuilder builder)
    {
        KeyVaultOptions options = builder.Configuration.GetSection(KeyVaultOptions.SectionName).Get<KeyVaultOptions>() ?? new();

        if (!options.Enabled)
        {
            return builder;
        }

        if (options.VaultUri is null)
        {
            throw new InvalidOperationException($"{KeyVaultOptions.SectionName}:{nameof(KeyVaultOptions.VaultUri)} is required when Key Vault is enabled.");
        }

        builder.Configuration.AddAzureKeyVault(
            options.VaultUri,
            AzureCredentials.Create(builder.Configuration),
            new AzureKeyVaultConfigurationOptions
            {
                ReloadInterval = options.ReloadIntervalMinutes > 0 ? TimeSpan.FromMinutes(options.ReloadIntervalMinutes) : null,
            });

        return builder;
    }
}
