using Andes.Agents.Api.Options;
using Andes.Agents.Common.Validation;
using FluentValidation;

namespace Andes.Agents.Api.Middleware;

internal static class RequestLoggingRegistration
{
    public static IServiceCollection AddAndesRequestLogging(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IValidator<RequestLoggingOptions>, RequestLoggingOptionsValidator>();

        services
            .AddOptions<RequestLoggingOptions>()
            .Bind(configuration.GetSection(RequestLoggingOptions.SectionName))
            .ValidateWithFluentValidation()
            .ValidateOnStart();

        return services;
    }

    public static IApplicationBuilder UseAndesRequestLogging(this IApplicationBuilder app) =>
        app.UseMiddleware<RequestLoggingMiddleware>();
}
