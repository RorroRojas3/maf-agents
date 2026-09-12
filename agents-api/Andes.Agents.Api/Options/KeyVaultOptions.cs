namespace Andes.Agents.Api.Options;

/// <summary>Whether and where configuration is layered from Azure Key Vault.</summary>
public sealed class KeyVaultOptions
{
    /// <summary>Configuration section these options bind from.</summary>
    public const string SectionName = "KeyVault";

    /// <summary>Gets or sets whether Key Vault secrets are loaded into configuration.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets the vault URI; required when enabled.</summary>
    public Uri? VaultUri { get; set; }

    /// <summary>Gets or sets how often secrets are re-read; zero disables reloading.</summary>
    public int ReloadIntervalMinutes { get; set; } = 30;
}
