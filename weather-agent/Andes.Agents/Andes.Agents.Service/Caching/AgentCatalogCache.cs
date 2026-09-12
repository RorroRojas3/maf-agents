using Andes.Agents.Repository.Agents;
using Andes.Agents.Repository.Agents.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Andes.Agents.Service.Caching;

/// <summary>The agent catalog, cached for a day unless invalidated.</summary>
public interface IAgentCatalogCache
{
    /// <summary>Returns the catalog, loading it when nothing is cached.</summary>
    ValueTask<AgentCatalog> GetAsync(CancellationToken cancellationToken);

    /// <summary>Discards the cached catalog so the next read loads it again; call it after changing the catalog.</summary>
    void Invalidate();
}

/// <inheritdoc />
public sealed class AgentCatalogCache(IMemoryCache cache, IServiceScopeFactory scopeFactory) : IAgentCatalogCache
{
    private const string _cacheKey = "andes.agent-catalog";

    private static readonly TimeSpan _lifetime = TimeSpan.FromHours(24);

    private readonly IMemoryCache _cache = cache;
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;

    /// <inheritdoc />
    public async ValueTask<AgentCatalog> GetAsync(CancellationToken cancellationToken)
    {
        // A load that throws leaves nothing behind, so the next read tries the store again.
        AgentCatalog? catalog = await _cache.GetOrCreateAsync(_cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = _lifetime;

            return await LoadAsync(cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);

        return catalog ?? throw new InvalidOperationException("The agent catalog cache returned no value.");
    }

    /// <inheritdoc />
    public void Invalidate() => _cache.Remove(_cacheKey);

    #region Private Methods

    private async Task<AgentCatalog> LoadAsync(CancellationToken cancellationToken)
    {
        AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();

        await using (scope.ConfigureAwait(false))
        {
            IReadOnlyList<ActiveAgentModel> activeModels = await scope.ServiceProvider
                .GetRequiredService<IAgentModelMappingRepository>()
                .ListActiveAsync(cancellationToken)
                .ConfigureAwait(false);

            return new AgentCatalog(activeModels);
        }
    }

    #endregion
}
