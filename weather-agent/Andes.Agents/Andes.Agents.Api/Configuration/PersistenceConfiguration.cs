using Andes.Agents.Api.Options;
using Andes.Agents.Repository.Cosmos;
using Andes.Agents.Repository.Serialization;
using Andes.Agents.Repository.Sessions;
using Azure.Core;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;

namespace Andes.Agents.Api.Configuration;

internal static class PersistenceConfiguration
{
    private const string _applicationName = "Andes.Agents";

    public static IServiceCollection AddAndesPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<CosmosDbOptions>()
            .Bind(configuration.GetSection(CosmosDbOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton(provider =>
        {
            CosmosDbOptions options = provider.GetRequiredService<IOptions<CosmosDbOptions>>().Value;

            CosmosClientOptions clientOptions = new()
            {
                ApplicationName = _applicationName,
                ConnectionMode = options.UseGatewayMode ? ConnectionMode.Gateway : ConnectionMode.Direct,
                UseSystemTextJsonSerializerWithOptions = RepositoryJsonOptions.Default,
            };

            return string.IsNullOrWhiteSpace(options.Key)
                ? new CosmosClient(options.AccountEndpoint, provider.GetRequiredService<TokenCredential>(), clientOptions)
                : new CosmosClient(options.AccountEndpoint, options.Key, clientOptions);
        });

        services.AddSingleton(provider =>
        {
            CosmosDbOptions options = provider.GetRequiredService<IOptions<CosmosDbOptions>>().Value;

            return new CosmosContainerNames(options.DatabaseId, options.SessionsContainerId, options.MessagesContainerId);
        });

        services.AddSingleton<ICosmosContainers, CosmosContainers>();
        services.AddSingleton<ISessionRepository, SessionRepository>();
        services.AddSingleton<ISessionMessageRepository, SessionMessageRepository>();
        services.AddSingleton<CosmosResourceProvisioner>();

        return services;
    }
}
