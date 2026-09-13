using Andes.Agents.Repository.Cosmos.Options;
using Andes.Agents.Repository.Cosmos.Provisioning;
using Microsoft.Extensions.Options;

namespace Andes.Agents.Api.Startup;

internal static class CosmosBootstrapper
{
    public static async Task EnsureCosmosResourcesAsync(this WebApplication app, CancellationToken cancellationToken = default)
    {
        CosmosDbOptions options = app.Services.GetRequiredService<IOptions<CosmosDbOptions>>().Value;

        if (!options.CreateResourcesOnStartup)
        {
            return;
        }

        await app.Services.GetRequiredService<CosmosResourceProvisioner>().EnsureCreatedAsync(cancellationToken);
    }
}
