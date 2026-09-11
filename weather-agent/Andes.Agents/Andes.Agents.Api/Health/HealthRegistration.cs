using Andes.Agents.Repository.Cosmos;
using Andes.Agents.Repository.Sql;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace Andes.Agents.Api.Health;

internal static class HealthRegistration
{
    private const string _readyTag = "ready";

    // Inside a platform probe's own timeout, so readiness reports SQL Server down instead of timing out itself.
    private static readonly TimeSpan _sqlTimeout = TimeSpan.FromSeconds(5);

    public static IServiceCollection AddAndesHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddAndesCosmosHealthCheck("cosmos", _readyTag)
            .AddAndesSqlHealthCheck("sql", _sqlTimeout, _readyTag);

        return services;
    }

    public static IEndpointRouteBuilder MapAndesHealthChecks(this IEndpointRouteBuilder app)
    {
        // Liveness answers as long as the process serves requests; readiness also needs the stores.
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false })
            .AllowAnonymous()
            .ExcludeFromDescription();

        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains(_readyTag) })
            .AllowAnonymous()
            .ExcludeFromDescription();

        return app;
    }
}
