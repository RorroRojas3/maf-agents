using Andes.Agents.Api.Filters;
using Andes.Agents.Common.Constants;
using Andes.Agents.Dto.Actions.Sessions;
using Andes.Agents.Dto.Pagination;
using Andes.Agents.Dto.Sessions;
using Andes.Agents.Service.Sessions;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Andes.Agents.Api.Endpoints;

internal static class SessionEndpoints
{
    // The route is the wire contract; the domain word behind it is "session".
    public static IEndpointRouteBuilder MapSessionEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("api/conversations")
            .WithTags("Conversations")
            .RequireAuthorization(AuthorizationPolicies.AgentAccess)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("", ListAsync)
            .WithName("ListConversations")
            .WithSummary("Lists the caller's conversations, newest first.")
            .ProducesValidationProblem()
            .AddEndpointFilter(ValidationEndpointFilter.Require<ListSessionsActionDto>());

        group.MapGet("{sessionId}", GetAsync)
            .WithName("GetConversation")
            .WithSummary("Returns one of the caller's conversations.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("{sessionId}/messages", ListMessagesAsync)
            .WithName("ListConversationMessages")
            .WithSummary("Lists the messages of one of the caller's conversations in order.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem()
            .AddEndpointFilter(ValidationEndpointFilter.Require<ListSessionMessagesActionDto>());

        group.MapDelete("{sessionId}", DeleteAsync)
            .WithName("DeleteConversation")
            .WithSummary("Deletes one of the caller's conversations and every message in it.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }

    internal static async Task<Ok<PaginatedResponseDto<SessionDto>>> ListAsync(
        [AsParameters] ListSessionsActionDto query,
        ISessionService sessions,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await sessions.ListAsync(query.Skip, query.Take, cancellationToken));

    internal static async Task<Ok<SessionDto>> GetAsync(
        string sessionId,
        ISessionService sessions,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await sessions.GetAsync(sessionId, cancellationToken));

    internal static async Task<Ok<PaginatedResponseDto<SessionMessageDto>>> ListMessagesAsync(
        string sessionId,
        [AsParameters] ListSessionMessagesActionDto query,
        ISessionService sessions,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await sessions.ListMessagesAsync(sessionId, query.Skip, query.Take, cancellationToken));

    internal static async Task<NoContent> DeleteAsync(
        string sessionId,
        ISessionService sessions,
        CancellationToken cancellationToken)
    {
        await sessions.DeleteAsync(sessionId, cancellationToken);

        return TypedResults.NoContent();
    }
}
