using Andes.Agents.Api.Options;
using Microsoft.Extensions.Options;

namespace Andes.Agents.Api.Configuration;

internal static class CorsConfiguration
{
    public static IServiceCollection AddAndesCors(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<CorsOptions>().Bind(configuration.GetSection(CorsOptions.SectionName));

        services.AddCors();
        services
            .AddOptions<Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions>()
            .Configure<IOptions<CorsOptions>>((cors, allowed) =>
                cors.AddDefaultPolicy(policy =>
                {
                    string[] origins = [.. allowed.Value.AllowedOrigins];

                    if (origins.Length > 0)
                    {
                        policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
                    }
                }));

        return services;
    }
}
