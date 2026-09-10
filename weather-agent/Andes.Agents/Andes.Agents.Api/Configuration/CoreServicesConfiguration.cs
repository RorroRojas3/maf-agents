using Andes.Agents.Service.Prompts;
using Andes.Agents.Service.Security;

namespace Andes.Agents.Api.Configuration;

internal static class CoreServicesConfiguration
{
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ICallerContext, HttpCallerContext>();
        services.AddSingleton<IPromptTemplateLoader, PromptTemplateLoader>();

        return services;
    }
}
