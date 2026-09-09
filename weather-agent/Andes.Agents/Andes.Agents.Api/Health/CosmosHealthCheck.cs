using Andes.Agents.Repository.Cosmos;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Andes.Agents.Api.Health;

internal sealed class CosmosHealthCheck(ICosmosContainers containers) : IHealthCheck
{
    private readonly ICosmosContainers _containers = containers;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await _containers.Sessions.ReadContainerAsync(cancellationToken: cancellationToken);

            return HealthCheckResult.Healthy();
        }
        catch (CosmosException exception)
        {
            return HealthCheckResult.Unhealthy("Cosmos DB is unreachable or the sessions container is missing.", exception);
        }
    }
}
