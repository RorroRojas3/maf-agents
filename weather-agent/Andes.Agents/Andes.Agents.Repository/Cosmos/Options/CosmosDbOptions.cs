using FluentValidation;

namespace Andes.Agents.Repository.Cosmos.Options;

/// <summary>Connection settings for the Cosmos DB account that stores sessions and messages.</summary>
public sealed class CosmosDbOptions
{
    /// <summary>Configuration section these options bind from.</summary>
    public const string SectionName = "CosmosDb";

    /// <summary>Gets or sets the account endpoint.</summary>
    public string AccountEndpoint { get; set; } = string.Empty;

    /// <summary>Gets or sets the account key; when blank the shared Azure credential is used instead.</summary>
    public string? Key { get; set; }

    /// <summary>Gets or sets the database id.</summary>
    public string DatabaseId { get; set; } = "andes-agents";

    /// <summary>Gets or sets the id of the container holding one document per session.</summary>
    public string SessionsContainerId { get; set; } = "sessions";

    /// <summary>Gets or sets the id of the container holding one document per message.</summary>
    public string MessagesContainerId { get; set; } = "messages";

    /// <summary>Gets or sets whether to use gateway connectivity; the local emulator supports nothing else.</summary>
    public bool UseGatewayMode { get; set; }

    /// <summary>Gets or sets whether the database and containers are created at startup; development only.</summary>
    public bool CreateResourcesOnStartup { get; set; }
}

internal sealed class CosmosDbOptionsValidator : AbstractValidator<CosmosDbOptions>
{
    public CosmosDbOptionsValidator()
    {
        RuleFor(options => options.AccountEndpoint)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(IsHttpUrl)
            .WithMessage($"'{CosmosDbOptions.SectionName}:{{PropertyName}}' must be an absolute http or https URL.");

        RuleFor(options => options.DatabaseId).NotEmpty();
        RuleFor(options => options.SessionsContainerId).NotEmpty();
        RuleFor(options => options.MessagesContainerId).NotEmpty();
    }

    // Uri.TryCreate alone accepts file:// and, on Windows, a bare drive path; CosmosClient needs http(s).
    private static bool IsHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out Uri? parsed)
        && (parsed.Scheme == Uri.UriSchemeHttps || parsed.Scheme == Uri.UriSchemeHttp);
}
