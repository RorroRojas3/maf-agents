using Andes.Agents.Api.Options;
using Andes.Agents.Common.Constants;
using Andes.Agents.Common.Validation;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;

namespace Andes.Agents.Api.Configuration;

internal static class AuthenticationConfiguration
{
    public static IServiceCollection AddAndesAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        IConfigurationSection section = configuration.GetSection(AzureAdOptions.SectionName);

        services.AddSingleton<IValidator<AzureAdOptions>, AzureAdOptionsValidator>();

        services
            .AddOptions<AzureAdOptions>()
            .Bind(section)
            .ValidateWithFluentValidation()
            .ValidateOnStart();

        services.AddHttpContextAccessor();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddMicrosoftIdentityWebApi(section);

        // The policy is fixed at startup, so it reads configuration directly rather than through the options monitor.
        AzureAdOptions azureAd = section.Get<AzureAdOptions>() ?? new AzureAdOptions();
        string[] scopes = azureAd.GetScopes();
        string[] appPermissions = azureAd.GetAppPermissions();

        services.AddAuthorizationBuilder().AddPolicy(AuthorizationPolicies.AgentAccess, policy =>
        {
            policy.RequireAuthenticatedUser();

            // Delegated callers carry scp, daemon A2A callers carry roles.
            if (scopes.Length > 0 || appPermissions.Length > 0)
            {
                policy.RequireScopeOrAppPermission(scopes, appPermissions);
            }
        });

        return services;
    }
}
