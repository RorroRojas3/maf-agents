using Andes.Agents.Common.Validation;
using Andes.Agents.Dto.Actions.Sessions;
using Andes.Agents.Service.Options;
using Andes.Agents.Service.Sessions;
using FluentValidation;

namespace Andes.Agents.Api.Configuration;

internal static class SessionsConfiguration
{
    public static IServiceCollection AddSessions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IValidator<SessionsOptions>, SessionsOptionsValidator>();
        services.AddSingleton<IValidator<ListSessionsActionDto>, ListSessionsActionDtoValidator>();
        services.AddSingleton<IValidator<ListSessionMessagesActionDto>, ListSessionMessagesActionDtoValidator>();

        services
            .AddOptions<SessionsOptions>()
            .Bind(configuration.GetSection(SessionsOptions.SectionName))
            .ValidateWithFluentValidation()
            .ValidateOnStart();

        services.AddSingleton<PersistedChatHistoryProvider>();
        services.AddSingleton<PersistedAgentSessionStore>();
        services.AddSingleton<ISessionService, SessionService>();

        return services;
    }
}
