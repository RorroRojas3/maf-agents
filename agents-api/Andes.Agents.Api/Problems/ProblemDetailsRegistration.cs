using System.Diagnostics;

namespace Andes.Agents.Api.Problems;

internal static class ProblemDetailsRegistration
{
    public static IServiceCollection AddAndesProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            HttpContext httpContext = context.HttpContext;
            Activity? activity = Activity.Current;

            context.ProblemDetails.Instance ??= httpContext.Request.Path;

            // The trace id is what a caller quotes when reporting a failure: it is the operation id the
            // request arrives under in Application Insights, and the only handle on a suppressed 500.
            context.ProblemDetails.Extensions["traceId"] = activity?.TraceId.ToString() ?? httpContext.TraceIdentifier;

            // The connection-scoped id, which is what the server's own log lines carry.
            context.ProblemDetails.Extensions["requestId"] = httpContext.TraceIdentifier;
        });

        return services;
    }
}
