using Andes.Agents.Api.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace Andes.Agents.Api.Configuration;

internal static class OpenApiConfiguration
{
    private const string _schemeName = "Bearer";

    public static IServiceCollection AddAndesOpenApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ApiDocsOptions>().Bind(configuration.GetSection(ApiDocsOptions.SectionName));
        services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

        return services;
    }

    public static WebApplication MapAndesOpenApi(this WebApplication app)
    {
        bool enabled = app.Environment.IsDevelopment()
            || app.Services.GetRequiredService<IOptions<ApiDocsOptions>>().Value.Enabled;

        if (!enabled)
        {
            return app;
        }

        app.MapOpenApi();
        app.MapScalarApiReference(options => options
            .WithTitle("Andes Agents API")
            .AddPreferredSecuritySchemes(_schemeName));

        return app;
    }

    private sealed class BearerSecuritySchemeTransformer(IAuthenticationSchemeProvider authenticationSchemeProvider) : IOpenApiDocumentTransformer
    {
        private readonly IAuthenticationSchemeProvider _authenticationSchemeProvider = authenticationSchemeProvider;

        public async Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
        {
            IEnumerable<AuthenticationScheme> schemes = await _authenticationSchemeProvider.GetAllSchemesAsync();

            if (!schemes.Any(scheme => scheme.Name == JwtBearerDefaults.AuthenticationScheme))
            {
                return;
            }

            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
            {
                [_schemeName] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    In = ParameterLocation.Header,
                    BearerFormat = "Json Web Token",
                },
            };

            foreach (IOpenApiPathItem path in document.Paths.Values)
            {
                if (path.Operations is null)
                {
                    continue;
                }

                foreach (OpenApiOperation operation in path.Operations.Values)
                {
                    operation.Security ??= [];
                    operation.Security.Add(new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference(_schemeName, document)] = [],
                    });
                }
            }
        }
    }
}
