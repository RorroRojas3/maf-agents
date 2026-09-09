namespace Andes.Agents.Api.Configuration;

internal static class AzureCredentialConfiguration
{
    public static IServiceCollection AddAndesAzureCredential(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(_ => AzureCredentials.Create(configuration));

        return services;
    }
}
