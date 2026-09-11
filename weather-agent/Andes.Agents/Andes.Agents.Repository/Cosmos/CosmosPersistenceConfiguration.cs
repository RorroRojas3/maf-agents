using Andes.Agents.Common.Validation;
using Andes.Agents.Repository.Cosmos.HealthChecks;
using Andes.Agents.Repository.Cosmos.Options;
using Andes.Agents.Repository.Cosmos.Provisioning;
using Andes.Agents.Repository.Cosmos.Serialization;
using Andes.Agents.Repository.Cosmos.Sessions;
using Andes.Agents.Repository.Sessions.Interfaces;
using Azure.Core;
using FluentValidation;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Andes.Agents.Repository.Cosmos;

/// <summary>Registers the Cosmos DB store and the repositories built on it.</summary>
public static class CosmosPersistenceConfiguration
{
    private const string _applicationName = "Andes.Agents";

    /// <summary>Binds the Cosmos section and registers the client, containers and session repositories.</summary>
    /// <remarks>A <see cref="TokenCredential"/> must already be registered; it is used whenever the account key is blank.</remarks>
    public static IServiceCollection AddAndesCosmosPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IValidator<CosmosDbOptions>, CosmosDbOptionsValidator>();

        services
            .AddOptions<CosmosDbOptions>()
            .Bind(configuration.GetSection(CosmosDbOptions.SectionName))
            .ValidateWithFluentValidation()
            .ValidateOnStart();

        services.AddSingleton(provider =>
        {
            CosmosDbOptions options = provider.GetRequiredService<IOptions<CosmosDbOptions>>().Value;

            CosmosClientOptions clientOptions = new()
            {
                ApplicationName = _applicationName,
                ConnectionMode = options.UseGatewayMode ? ConnectionMode.Gateway : ConnectionMode.Direct,
                UseSystemTextJsonSerializerWithOptions = CosmosJsonOptions.Default,
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
        services.AddSingleton<ISessionRepository, CosmosSessionRepository>();
        services.AddSingleton<ISessionMessageRepository, CosmosSessionMessageRepository>();
        services.AddSingleton<CosmosResourceProvisioner>();

        return services;
    }

    /// <summary>Adds a readiness probe that reads the sessions container.</summary>
    public static IHealthChecksBuilder AddAndesCosmosHealthCheck(this IHealthChecksBuilder builder, string name, params string[] tags)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddCheck<CosmosHealthCheck>(name, tags: tags);
    }
}
