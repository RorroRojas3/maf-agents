using A2A.AspNetCore;
using Andes.Agents.Api.Options;
using Andes.Agents.Common.Constants;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Microsoft.Extensions.Options;

namespace Andes.Agents.Api.Endpoints;

internal static class AgentEndpoints
{
    private const string _a2aPattern = "/" + WeatherAgentCard.A2APath;
    private const string _uiPattern = "/weather/ui";

    public static IEndpointRouteBuilder MapAgentEndpoints(this IEndpointRouteBuilder app)
    {
        AgentCardOptions cardOptions = app.ServiceProvider.GetRequiredService<IOptions<AgentCardOptions>>().Value;

        // Both A2A bindings share the path: HTTP+JSON adds method suffixes, JSON-RPC posts to the root.
        app.MapA2AHttpJson(AgentNames.Weather, _a2aPattern)
            .RequireAuthorization(AuthorizationPolicies.AgentAccess)
            .RequireRateLimiting(RateLimitPolicies.AgentTurns);

        app.MapA2AJsonRpc(AgentNames.Weather, _a2aPattern)
            .RequireAuthorization(AuthorizationPolicies.AgentAccess)
            .RequireRateLimiting(RateLimitPolicies.AgentTurns);

        app.MapAGUIServer(AgentNames.Weather, _uiPattern)
            .RequireAuthorization(AuthorizationPolicies.AgentAccess)
            .RequireRateLimiting(RateLimitPolicies.AgentTurns);

        // Discovery must work before a client has a token.
        app.MapWellKnownAgentCard(WeatherAgentCard.Create(cardOptions))
            .AllowAnonymous();

        return app;
    }
}
