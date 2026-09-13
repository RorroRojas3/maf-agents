using Andes.Agents.Repository.Agents;
using Andes.Agents.Repository.Agents.Interfaces;
using Andes.Agents.Repository.Sql.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace Andes.Agents.Repository.Sql.Agents;

/// <inheritdoc />
public sealed class SqlAgentModelMappingRepository(PolicyDbContext ctx) : IAgentModelMappingRepository
{
    private readonly PolicyDbContext _ctx = ctx;

    /// <inheritdoc />
    public async Task<IReadOnlyList<ActiveAgentModel>> ListActiveAsync(CancellationToken cancellationToken) =>
        await _ctx.AgentModelMappings
            .AsNoTracking()
            .Where(mapping => !mapping.DateDeactivated.HasValue)
            .Join(
                _ctx.Agents,
                mapping => mapping.AgentId,
                agent => agent.Id,
                (mapping, agent) => new { mapping.ModelId, AgentId = agent.Id, AgentName = agent.Name })
            .Join(
                _ctx.Models,
                active => active.ModelId,
                model => model.Id,
                (active, model) => new ActiveAgentModel(
                    active.AgentId,
                    active.AgentName,
                    model.Id,
                    model.DeploymentName,
                    new TokenPrices(model.InputPricePerMillionTokens, model.CachedInputPricePerMillionTokens, model.OutputPricePerMillionTokens)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}
