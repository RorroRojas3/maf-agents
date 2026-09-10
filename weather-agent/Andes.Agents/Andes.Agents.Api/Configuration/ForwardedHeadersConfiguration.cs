using Microsoft.AspNetCore.HttpOverrides;

namespace Andes.Agents.Api.Configuration;

internal static class ForwardedHeadersConfiguration
{
    // Platform ingress terminates TLS; without the forwarded scheme every request looks like plain HTTP and the HTTPS redirect loops.
    public static IServiceCollection AddAndesForwardedHeaders(this IServiceCollection services)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            // The ingress address is not knowable in advance, and nothing but the ingress can reach the container.
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

        return services;
    }
}
