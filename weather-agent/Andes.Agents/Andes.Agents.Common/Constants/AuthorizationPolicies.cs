namespace Andes.Agents.Common.Constants;

/// <summary>Names of the authorization policies the endpoints require.</summary>
public static class AuthorizationPolicies
{
    /// <summary>May invoke agents and read their own conversations.</summary>
    public const string AgentAccess = "AgentAccess";
}
