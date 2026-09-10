using System.Globalization;
using System.Threading.RateLimiting;
using Andes.Agents.Api.Options;
using Andes.Agents.Api.Problems;
using Andes.Agents.Common.Constants;
using Andes.Agents.Common.Validation;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Andes.Agents.Api.Configuration;

internal static class RateLimitingConfiguration
{
    public static IServiceCollection AddAndesRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IValidator<RateLimitingOptions>, RateLimitingOptionsValidator>();

        services
            .AddOptions<RateLimitingOptions>()
            .Bind(configuration.GetSection(RateLimitingOptions.SectionName))
            .ValidateWithFluentValidation()
            .ValidateOnStart();

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }

                await ProblemResponseWriter.WriteAsync(
                    context.HttpContext,
                    StatusCodes.Status429TooManyRequests,
                    "Too many requests",
                    ProblemTypes.TooManyRequests,
                    "The caller has started too many agent turns in the last minute.",
                    cancellationToken);
            };

            limiter.AddPolicy(RateLimitPolicies.AgentTurns, context =>
            {
                int permitPerMinute = context.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value.PermitPerMinute;

                // Partition by caller identity, falling back to the client address for the anonymous edge case.
                string partition = context.User.FindFirst(ClaimTypeNames.Oid)?.Value
                    ?? context.User.FindFirst(ClaimTypeNames.ObjectIdentifier)?.Value
                    ?? context.Connection.RemoteIpAddress?.ToString()
                    ?? "anonymous";

                return RateLimitPartition.GetSlidingWindowLimiter(partition, _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = permitPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 6,
                    QueueLimit = 0,
                });
            });
        });

        return services;
    }
}
