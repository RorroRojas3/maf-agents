using Andes.Agents.Repository.Cosmos;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace Andes.Agents.Api.Health;

internal static class HealthRegistration
{
    private const string _readyTag = "ready";

    public static IServiceCollection AddAndesHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks().AddAndesCosmosHealthCheck("cosmos", _readyTag);

        return services;
    }

    public static IEndpointRouteBuilder MapAndesHealthChecks(this IEndpointRouteBuilder app)
    {
        // Liveness answers as long as the process serves requests; readiness also needs the store.
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false })
            .AllowAnonymous()
            .ExcludeFromDescription();

        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains(_readyTag) })
            .AllowAnonymous()
            .ExcludeFromDescription();

        return app;
    }
}
