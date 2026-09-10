using Andes.Agents.Api.Options;
using Andes.Agents.Common.Validation;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace Andes.Agents.Api.Configuration;

internal static class OpenApiConfiguration
{
    private const string _bearerSchemeName = "Bearer";
    private const string _entraIdSchemeName = "EntraId";
    private const string _scalarPath = "/scalar";

    public static IServiceCollection AddAndesOpenApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IValidator<ApiDocsOptions>, ApiDocsOptionsValidator>();

        services
            .AddOptions<ApiDocsOptions>()
            .Bind(configuration.GetSection(ApiDocsOptions.SectionName))
            .ValidateWithFluentValidation()
            .ValidateOnStart();

        services.AddOpenApi(options => options.AddDocumentTransformer<SecuritySchemeTransformer>());

        return services;
    }

    public static WebApplication MapAndesOpenApi(this WebApplication app)
    {
        IOptions<ApiDocsOptions> apiDocsOptions = app.Services.GetRequiredService<IOptions<ApiDocsOptions>>();

        if (!app.Environment.IsDevelopment() && !apiDocsOptions.Value.Enabled)
        {
            return app;
        }

        app.MapOpenApi();
        app.MapScalarApiReference(_scalarPath, (options, httpContext) =>
        {
            ApiDocsOptions apiDocs = apiDocsOptions.Value;

            options.WithTitle("Andes Agents API");

            if (!apiDocs.HasSignIn())
            {
                options.AddPreferredSecuritySchemes(_bearerSchemeName);
                return;
            }

            // Scalar also serves /scalar/v1 and defaults the redirect to the loaded path; Entra ID accepts only the
            // registered URI, so every entry path is pinned to /scalar/ on the requesting host.
            HttpRequest request = httpContext.Request;
            string redirectUri = UriHelper.BuildAbsolute(request.Scheme, request.Host, request.PathBase, $"{_scalarPath}/");

            // The code comes back in the fragment, which never reaches the server or its request telemetry.
            options
                .AddPreferredSecuritySchemes(_entraIdSchemeName)
                .AddAuthorizationCodeFlow(_entraIdSchemeName, flow => flow
                    .WithClientId(apiDocs.ClientId)
                    .WithSelectedScopes(apiDocs.GetScopes())
                    .WithPkce(Pkce.Sha256)
                    .WithRedirectUri(redirectUri)
                    .AddQueryParameter("response_mode", "fragment"));
        });

        return app;
    }

    private sealed class SecuritySchemeTransformer(
        IAuthenticationSchemeProvider authenticationSchemeProvider,
        IOptions<ApiDocsOptions> apiDocsOptions,
        IOptions<AzureAdOptions> azureAdOptions) : IOpenApiDocumentTransformer
    {
        private readonly IAuthenticationSchemeProvider _authenticationSchemeProvider = authenticationSchemeProvider;
        private readonly ApiDocsOptions _apiDocs = apiDocsOptions.Value;
        private readonly AzureAdOptions _azureAd = azureAdOptions.Value;

        public async Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
        {
            IEnumerable<AuthenticationScheme> schemes = await _authenticationSchemeProvider.GetAllSchemesAsync();

            if (!schemes.Any(scheme => scheme.Name == JwtBearerDefaults.AuthenticationScheme))
            {
                return;
            }

            string[] scopes = _apiDocs.GetScopes();

            Dictionary<string, IOpenApiSecurityScheme> securitySchemes = new()
            {
                [_bearerSchemeName] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    In = ParameterLocation.Header,
                    BearerFormat = "Json Web Token",
                },
            };

            if (_apiDocs.HasSignIn())
            {
                string tenantUrl = _azureAd.GetTenantUrl();

                securitySchemes[_entraIdSchemeName] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.OAuth2,
                    Flows = new OpenApiOAuthFlows
                    {
                        AuthorizationCode = new OpenApiOAuthFlow
                        {
                            AuthorizationUrl = new Uri($"{tenantUrl}/oauth2/v2.0/authorize"),
                            TokenUrl = new Uri($"{tenantUrl}/oauth2/v2.0/token"),
                            Scopes = scopes.ToDictionary(scope => scope, _ => string.Empty),
                        },
                    },
                };
            }

            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes = securitySchemes;

            foreach (IOpenApiPathItem path in document.Paths.Values)
            {
                if (path.Operations is null)
                {
                    continue;
                }

                foreach (OpenApiOperation operation in path.Operations.Values)
                {
                    operation.Security ??= [];

                    // Separate requirements, so either scheme alone satisfies the operation.
                    foreach (string schemeName in securitySchemes.Keys)
                    {
                        operation.Security.Add(new OpenApiSecurityRequirement
                        {
                            [new OpenApiSecuritySchemeReference(schemeName, document)] = schemeName == _entraIdSchemeName ? [.. scopes] : [],
                        });
                    }
                }
            }
        }
    }
}
