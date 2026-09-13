using System.Collections.Frozen;
using Andes.Agents.Repository.Agents;

namespace Andes.Agents.Service.Caching;

/// <summary>The active model of every agent in the catalog, looked up by agent name.</summary>
public sealed class AgentCatalog(IEnumerable<ActiveAgentModel> activeModels)
{
    // Names compare the way the database's case-insensitive collation keeps them unique.
    private readonly FrozenDictionary<string, ActiveAgentModel> _byAgentName =
        activeModels.ToFrozenDictionary(model => model.AgentName, StringComparer.OrdinalIgnoreCase);

    /// <summary>Returns the agent's active model, or null when the catalog has none for it.</summary>
    public ActiveAgentModel? Find(string agentName)
    {
        ArgumentNullException.ThrowIfNull(agentName);

        return _byAgentName.GetValueOrDefault(agentName);
    }
}
