using System.Diagnostics;
using Andes.Agents.Api.Observability;
using Andes.Agents.Api.Options;
using Microsoft.Extensions.Options;

namespace Andes.Agents.Api.Middleware;

/// <summary>Logs one line per request: what was called, how it ended, and how long it took.</summary>
/// <remarks>
/// The route pattern stands in for the path, so an identifier in it never reaches the sink; only an unrouted
/// request logs its own path, sanitized. Bodies and query values are never read, and the caller's object id —
/// which identifies a person — stays out of the message.
/// </remarks>
internal sealed partial class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    private readonly RequestDelegate _next = next;
    private readonly ILogger<RequestLoggingMiddleware> _logger = logger;

    public async Task InvokeAsync(HttpContext context, IOptions<RequestLoggingOptions> options)
    {
        RequestLoggingOptions settings = options.Value;

        if (!settings.Enabled || IsExcluded(context.Request.Path, settings.ExcludedPaths))
        {
            await _next(context);

            return;
        }

        long start = Stopwatch.GetTimestamp();

        try
        {
            await _next(context);
        }
        finally
        {
            double elapsed = Math.Round(Stopwatch.GetElapsedTime(start).TotalMilliseconds, 1);
            string route = RequestDescriptor.Describe(context);

            if (elapsed >= settings.SlowRequestThresholdMilliseconds)
            {
                LogSlowRequest(context.Request.Method, route, context.Response.StatusCode, elapsed);
            }
            else
            {
                LogRequest(context.Request.Method, route, context.Response.StatusCode, elapsed);
            }
        }
    }

    private static bool IsExcluded(PathString path, string[] excludedPaths)
    {
        foreach (string excluded in excludedPaths)
        {
            if (path.StartsWithSegments(excluded, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "{Method} {Route} responded {StatusCode} in {ElapsedMilliseconds} ms.")]
    private partial void LogRequest(string method, string route, int statusCode, double elapsedMilliseconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Method} {Route} responded {StatusCode} in {ElapsedMilliseconds} ms, over the slow-request threshold.")]
    private partial void LogSlowRequest(string method, string route, int statusCode, double elapsedMilliseconds);
}
