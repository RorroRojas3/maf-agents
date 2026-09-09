using Andes.Agents.Api.Options;
using Andes.Agents.Common.Constants;
using Andes.Agents.Service.Agents;
using Andes.Agents.Service.Prompts;
using Andes.Agents.Service.Sessions;
using Andes.Agents.Service.Weather.Tools;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace Andes.Agents.Api.Configuration;

internal static class AgentsConfiguration
{
    public static IServiceCollection AddAgents(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<AgentCardOptions>()
            .Bind(configuration.GetSection(AgentCardOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(
                options => options.PublicBaseUrl?.IsAbsoluteUri == true,
                $"{AgentCardOptions.SectionName}:{nameof(AgentCardOptions.PublicBaseUrl)} must be an absolute URL.")
            .ValidateOnStart();

        services.AddAGUIServer();

        services
            .AddAIAgent(AgentNames.Weather, CreateWeatherAgent)
            // The store scopes every lookup by the caller itself, so the framework's isolation wrapper is not layered on top.
            .WithSessionStore((provider, _) => provider.GetRequiredService<CosmosAgentSessionStore>(), withIsolation: false)
            .AddA2AServer();

        return services;
    }

    private static AIAgent CreateWeatherAgent(IServiceProvider provider, string name)
    {
        ILoggerFactory loggerFactory = provider.GetRequiredService<ILoggerFactory>();
        TelemetryOptions telemetry = provider.GetRequiredService<IOptions<TelemetryOptions>>().Value;

        ChatClientAgent agent = new(
            provider.GetRequiredKeyedService<IChatClient>(ChatClientKeys.AzureOpenAI),
            new ChatClientAgentOptions
            {
                Id = name,
                Name = name,
                Description = "Answers questions about current weather and short-range forecasts anywhere in the world.",
                ChatOptions = new ChatOptions
                {
                    Instructions = provider.GetRequiredService<IPromptTemplateLoader>().Load(PromptNames.WeatherAgent),
                    Tools = provider.GetRequiredService<WeatherToolProvider>().CreateTools(),
                },
                ChatHistoryProvider = provider.GetRequiredService<CosmosChatHistoryProvider>(),
                // The keyed client already carries function invocation and telemetry.
                UseProvidedChatClientAsIs = true,
            },
            loggerFactory,
            provider);

        return agent
            .AsBuilder()
            .UseOpenTelemetry(TelemetryNames.Source, otel => otel.EnableSensitiveData = telemetry.EnableSensitiveData)
            .Use(inner => new UsageRecordingAgent(inner, loggerFactory))
            .Build(provider);
    }
}
