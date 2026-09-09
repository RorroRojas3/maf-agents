using A2A;
using Andes.Agents.Api.Options;

namespace Andes.Agents.Api.Endpoints;

internal static class WeatherAgentCard
{
    public const string A2APath = "weather/a2a";

    private const string _bearerScheme = "entra-bearer";

    public static AgentCard Create(AgentCardOptions options)
    {
        Uri baseUrl = options.PublicBaseUrl ?? throw new InvalidOperationException($"{AgentCardOptions.SectionName}:{nameof(AgentCardOptions.PublicBaseUrl)} is required.");

        // Relative resolution drops the last path segment of a base without a trailing slash, which a gateway prefix would have.
        Uri root = baseUrl.AbsolutePath.EndsWith('/') ? baseUrl : new Uri(baseUrl.AbsoluteUri + "/");
        string a2aUrl = new Uri(root, A2APath).ToString();

        return new AgentCard
        {
            Name = "Andes Weather Agent",
            Description = "Answers questions about current weather and short-range forecasts anywhere in the world.",
            Version = options.Version,
            DefaultInputModes = ["text"],
            DefaultOutputModes = ["text"],
            Capabilities = new AgentCapabilities { Streaming = true },
            SecuritySchemes = new Dictionary<string, SecurityScheme>
            {
                [_bearerScheme] = new SecurityScheme
                {
                    HttpAuthSecurityScheme = new HttpAuthSecurityScheme
                    {
                        Scheme = "bearer",
                        BearerFormat = "JWT",
                        Description = "A Microsoft Entra ID access token issued for this API.",
                    },
                },
            },
            SecurityRequirements =
            [
                new SecurityRequirement
                {
                    Schemes = new Dictionary<string, StringList> { [_bearerScheme] = new StringList { List = [] } },
                },
            ],
            SupportedInterfaces =
            [
                new AgentInterface { Url = a2aUrl, ProtocolBinding = ProtocolBindingNames.HttpJson, ProtocolVersion = "1.0" },
                new AgentInterface { Url = a2aUrl, ProtocolBinding = ProtocolBindingNames.JsonRpc, ProtocolVersion = "1.0" },
            ],
            Skills =
            [
                new AgentSkill
                {
                    Id = "current-weather",
                    Name = "Current weather",
                    Description = "Current conditions at a named place: temperature, wind, humidity and precipitation chance.",
                    Tags = ["weather", "current-conditions"],
                    Examples = ["What's the weather in Seattle right now?"],
                },
                new AgentSkill
                {
                    Id = "daily-forecast",
                    Name = "Daily forecast",
                    Description = "Day-by-day forecast for up to seven days at a named place.",
                    Tags = ["weather", "forecast"],
                    Examples = ["Will it rain in London this weekend?"],
                },
            ],
        };
    }
}
