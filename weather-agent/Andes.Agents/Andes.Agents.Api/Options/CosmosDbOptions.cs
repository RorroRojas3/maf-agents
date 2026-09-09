using System.ComponentModel.DataAnnotations;

namespace Andes.Agents.Api.Options;

/// <summary>Connection settings for the Cosmos DB account that stores sessions and messages.</summary>
public sealed class CosmosDbOptions
{
    /// <summary>Configuration section these options bind from.</summary>
    public const string SectionName = "CosmosDb";

    /// <summary>Gets or sets the account endpoint.</summary>
    [Required]
    [Url]
    public string AccountEndpoint { get; set; } = string.Empty;

    /// <summary>Gets or sets the account key; when blank the shared Azure credential is used instead.</summary>
    public string? Key { get; set; }

    /// <summary>Gets or sets the database id.</summary>
    [Required]
    public string DatabaseId { get; set; } = "andes-agents";

    /// <summary>Gets or sets the id of the container holding one document per session.</summary>
    [Required]
    public string SessionsContainerId { get; set; } = "sessions";

    /// <summary>Gets or sets the id of the container holding one document per message.</summary>
    [Required]
    public string MessagesContainerId { get; set; } = "messages";

    /// <summary>Gets or sets whether to use gateway connectivity; the local emulator supports nothing else.</summary>
    public bool UseGatewayMode { get; set; }

    /// <summary>Gets or sets whether the database and containers are created at startup; development only.</summary>
    public bool CreateResourcesOnStartup { get; set; }
}
