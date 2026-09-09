using Microsoft.AspNetCore.Mvc;

namespace Andes.Agents.Api.Problems;

internal static class ProblemResponseWriter
{
    // Writes nothing once the response has started or the caller is gone: a faulting SSE stream is aborted
    // rather than corrupted, and a throw here would hide the failure that was being reported.
    public static async ValueTask WriteAsync(
        HttpContext httpContext,
        int status,
        string title,
        string? type,
        string? detail,
        CancellationToken cancellationToken,
        Exception? exception = null)
    {
        if (httpContext.Response.HasStarted || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        httpContext.Response.StatusCode = status;

        IProblemDetailsService problemDetailsService = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();

        await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Type = type,
                Detail = detail,
            },
        });
    }
}
