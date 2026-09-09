using Andes.Agents.Service.Options;
using Andes.Agents.Service.Sessions;

namespace Andes.Agents.Api.Configuration;

internal static class SessionsConfiguration
{
    public static IServiceCollection AddSessions(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<SessionsOptions>()
            .Bind(configuration.GetSection(SessionsOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<CosmosChatHistoryProvider>();
        services.AddSingleton<CosmosAgentSessionStore>();
        services.AddSingleton<ISessionService, SessionService>();

        return services;
    }
}
