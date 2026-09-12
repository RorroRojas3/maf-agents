namespace Andes.Agents.Common.Constants;

/// <summary>Registration names of the hosted agents; each doubles as the agent's stable id.</summary>
public static class AgentNames
{
    /// <summary>The weather agent.</summary>
    public const string Weather = "weather-agent";

    /// <summary>Every hosted agent; startup requires each to have an active model in the agent catalog.</summary>
    public static readonly IReadOnlyList<string> All = [Weather];
}
