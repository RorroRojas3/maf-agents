using System.Reflection;
using Microsoft.Extensions.Configuration;

namespace ImageAgent.Configuration;

/// <summary>
/// Builds the sample's configuration and turns it into validated options.
/// </summary>
/// <remarks>
/// Providers are layered lowest-priority first: the shared <c>config/appsettings.json</c> copied
/// next to the binary, then user-secrets, then environment variables. Endpoint and model names
/// belong in the shared file so they are entered once for every sample; the key is better left to
/// user-secrets, which stores outside the repository tree.
/// </remarks>
internal static class ConfigurationLoader
{
    /// <summary>Builds the layered configuration for the sample.</summary>
    /// <returns>The composed configuration.</returns>
    public static IConfiguration Build()
    {
        ConfigurationBuilder builder = new();
        builder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);

        Assembly? entryAssembly = Assembly.GetEntryAssembly();

        if (entryAssembly is not null)
        {
            builder.AddUserSecrets(entryAssembly, optional: true);
        }

        builder.AddEnvironmentVariables();

        return builder.Build();
    }

    /// <summary>Reads and validates the Foundry connection settings.</summary>
    /// <param name="configuration">The configuration to read from.</param>
    /// <returns>Validated Foundry options.</returns>
    /// <exception cref="InvalidOperationException">A required setting is missing or malformed.</exception>
    public static FoundryOptions LoadFoundryOptions(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        FoundryOptions options = new();
        configuration.GetSection(FoundryOptions.SectionName).Bind(options);

        // The flat names are read as a fallback because that is what Azure tooling and CI systems
        // already export. An unpopulated CI secret arrives as "" rather than absent, which is why
        // these are blank checks rather than null checks.
        options.Endpoint = FirstNonBlank(options.Endpoint, configuration, "FOUNDRY_ENDPOINT", "AZURE_OPENAI_ENDPOINT");
        options.ApiKey = FirstNonBlank(options.ApiKey, configuration, "FOUNDRY_API_KEY", "AZURE_OPENAI_API_KEY");
        options.ChatModel = FirstNonBlank(options.ChatModel, configuration, "FOUNDRY_CHAT_MODEL");
        options.ImageModel = FirstNonBlank(options.ImageModel, configuration, "FOUNDRY_IMAGE_MODEL");

        IReadOnlyList<string> problems = options.Validate();

        if (problems.Count > 0)
        {
            throw new InvalidOperationException(BuildSetupMessage(problems));
        }

        return options;
    }

    /// <summary>Reads the sample's behavioural settings, falling back to defaults for anything absent.</summary>
    /// <param name="configuration">The configuration to read from.</param>
    /// <returns>The sample options.</returns>
    public static ImageAgentOptions LoadImageAgentOptions(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        ImageAgentOptions options = new();
        configuration.GetSection(ImageAgentOptions.SectionName).Bind(options);

        return options;
    }

    private static string FirstNonBlank(string bound, IConfiguration configuration, params string[] fallbackKeys)
    {
        if (!string.IsNullOrWhiteSpace(bound))
        {
            return bound;
        }

        foreach (string key in fallbackKeys)
        {
            string? value = configuration[key];

            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }

    private static string BuildSetupMessage(IReadOnlyList<string> problems)
    {
        string detail = string.Join(Environment.NewLine, problems.Select(problem => $"  - {problem}"));

        return $"""
            ImageAgent is not configured:

            {detail}

            Copy the tracked sample file and fill it in — it is git-ignored, so nothing lands in a commit:

              cp config/appsettings.sample.json config/appsettings.json

            The endpoint is the resource-level OpenAI-compatible v1 route, one of:

              https://<resource>.openai.azure.com/openai/v1/
              https://<resource>.services.ai.azure.com/openai/v1/

            Not the project-scoped '/api/projects/<project>/openai/v1/' route — that one serves
            chat but not the image APIs.

            Set the key outside the repository tree instead, if you prefer:

              dotnet user-secrets set "Foundry:ApiKey" "<key>" --project samples/02-agents/ImageAgent

            The environment variables FOUNDRY_ENDPOINT, FOUNDRY_API_KEY, FOUNDRY_CHAT_MODEL and
            FOUNDRY_IMAGE_MODEL are read too, as are the AZURE_OPENAI_ prefixed forms of the first two.
            """;
    }
}
