using Andes.Agents.Api.Observability;
using Andes.Agents.Api.Problems;
using Andes.Agents.Service.Exceptions;
using Andes.Agents.Service.Sessions;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;

namespace Andes.Agents.Api.ExceptionHandlers;

/// <remarks>
/// A 5xx body carries no detail, only the trace id; the exception behind it goes to the log instead, where a
/// provider message can still quote the request that failed.
/// </remarks>
internal sealed partial class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    // Client-closed-request as nginx numbers it; there is no one left to read a body.
    private const int _clientClosedRequest = 499;

    private readonly ILogger<GlobalExceptionHandler> _logger = logger;

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        string route = RequestDescriptor.Describe(httpContext);
        string method = httpContext.Request.Method;

        // Only the caller going away is a 499; a timeout inside a tool or a store call is a genuine failure.
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            LogAbandoned(method, route);

            if (!httpContext.Response.HasStarted)
            {
                httpContext.Response.StatusCode = _clientClosedRequest;
            }

            return true;
        }

        ProblemDescription problem = Describe(exception);

        if (problem.Status >= StatusCodes.Status500InternalServerError)
        {
            LogUnexpected(exception, method, route);
        }
        else
        {
            LogExpected(method, route, problem.Status, exception.GetType().Name);
        }

        await ProblemResponseWriter.WriteAsync(httpContext, problem.Status, problem.Title, problem.Type, problem.Detail, cancellationToken, exception, problem.Errors);

        return true;
    }

    private static ProblemDescription Describe(Exception exception) => exception switch
    {
        // The failures go in the body, never in the log line: they quote what the caller sent.
        ValidationException validation =>
            new(StatusCodes.Status400BadRequest, "The request is not valid", ProblemTypes.ValidationError, "One or more parameters are not valid.", ToErrors(validation)),
        InvalidSessionIdException =>
            new(StatusCodes.Status400BadRequest, "The request is not valid", ProblemTypes.ValidationError, exception.Message),
        ForbiddenException =>
            new(StatusCodes.Status403Forbidden, "Forbidden", ProblemTypes.Forbidden, exception.Message),
        NotFoundException =>
            new(StatusCodes.Status404NotFound, "Resource not found", ProblemTypes.ResourceNotFound, exception.Message),
        ConversationBusyException =>
            new(StatusCodes.Status409Conflict, "Conversation busy", ProblemTypes.ConversationBusy, exception.Message),

        // No detail and no type: an unexpected message can name hosts or internals, and a model failure can
        // carry the prompt that caused it. The trace id on the response is how a caller reports it instead.
        _ => new(StatusCodes.Status500InternalServerError, "An unexpected error occurred", Type: null, Detail: null),
    };

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception for {Method} {Route}.")]
    private partial void LogUnexpected(Exception exception, string method, string route);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Method} {Route} failed with {StatusCode} ({ExceptionType}).")]
    private partial void LogExpected(string method, string route, int statusCode, string exceptionType);

    [LoggerMessage(Level = LogLevel.Debug, Message = "{Method} {Route} was abandoned by the caller.")]
    private partial void LogAbandoned(string method, string route);

    private static Dictionary<string, string[]> ToErrors(ValidationException exception) =>
        exception.Errors
            .GroupBy(failure => failure.PropertyName, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(failure => failure.ErrorMessage).ToArray(), StringComparer.Ordinal);

    private sealed record ProblemDescription(
        int Status,
        string Title,
        string? Type,
        string? Detail,
        IReadOnlyDictionary<string, string[]>? Errors = null);
}
