using Andes.Agents.Api.ExceptionHandlers;

namespace Andes.Agents.Api.Configuration;

internal static class ExceptionHandlingConfiguration
{
    public static IServiceCollection AddAndesExceptionHandling(this IServiceCollection services)
    {
        services.AddExceptionHandler<GlobalExceptionHandler>();

        return services;
    }
}
