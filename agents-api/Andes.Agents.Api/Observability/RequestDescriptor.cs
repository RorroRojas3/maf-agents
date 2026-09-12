using Microsoft.AspNetCore.Diagnostics;

namespace Andes.Agents.Api.Observability;

/// <summary>Names the current request for a log line, preferring its route pattern over the path the caller sent.</summary>
internal static class RequestDescriptor
{
    private const int _maxPathLength = 200;

    /// <summary>
    /// The matched route pattern, so an identifier in the path never reaches a log sink; an unmatched
    /// request falls back to its own path, sanitized.
    /// </summary>
    public static string Describe(HttpContext httpContext)
    {
        // The exception-handler middleware clears the endpoint before it runs a handler and never restores it,
        // so on a failure the pattern survives only on the feature it leaves behind.
        Endpoint? endpoint = httpContext.GetEndpoint()
            ?? httpContext.Features.Get<IExceptionHandlerFeature>()?.Endpoint;

        if (endpoint is RouteEndpoint routeEndpoint && routeEndpoint.RoutePattern.RawText is { Length: > 0 } pattern)
        {
            return pattern.StartsWith('/') ? pattern : $"/{pattern}";
        }

        return Sanitize(httpContext.Request.Path.Value);
    }

    private static string Sanitize(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return "/";
        }

        string trimmed = path.Length > _maxPathLength ? path[.._maxPathLength] : path;

        // A newline in a caller-supplied path would let it forge extra log lines.
        return trimmed.ReplaceLineEndings(string.Empty);
    }
}
