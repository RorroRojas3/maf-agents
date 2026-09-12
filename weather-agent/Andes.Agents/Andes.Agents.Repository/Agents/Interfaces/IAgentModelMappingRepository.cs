namespace Andes.Agents.Repository.Agents.Interfaces;

/// <summary>Reads which model each agent runs on.</summary>
public interface IAgentModelMappingRepository
{
    /// <summary>Lists every active mapping with its agent's name and its model's deployment and prices.</summary>
    Task<IReadOnlyList<ActiveAgentModel>> ListActiveAsync(CancellationToken cancellationToken);
}
