using Andes.Agents.Api.Options;

namespace Andes.Agents.Api.Middleware;

internal static class RequestLoggingRegistration
{
    public static IServiceCollection AddAndesRequestLogging(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<RequestLoggingOptions>()
            .Bind(configuration.GetSection(RequestLoggingOptions.SectionName))
            .ValidateDataAnnotations()
            // A prefix without its leading slash makes PathString throw on every request, and a blank one
            // matches everything; data annotations cannot see inside the array.
            .Validate(
                options => Array.TrueForAll(options.ExcludedPaths, path => path.StartsWith('/')),
                $"{RequestLoggingOptions.SectionName}:{nameof(RequestLoggingOptions.ExcludedPaths)} entries must each start with '/'.")
            .ValidateOnStart();

        return services;
    }

    public static IApplicationBuilder UseAndesRequestLogging(this IApplicationBuilder app) =>
        app.UseMiddleware<RequestLoggingMiddleware>();
}
