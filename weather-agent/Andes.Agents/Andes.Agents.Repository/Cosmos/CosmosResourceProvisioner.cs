using Microsoft.Azure.Cosmos;

namespace Andes.Agents.Repository.Cosmos;

/// <summary>Creates the database and containers when they do not exist.</summary>
/// <remarks>
/// Development only: data-plane RBAC cannot create containers, so production provisions the same
/// definition through infrastructure as code.
/// </remarks>
public sealed class CosmosResourceProvisioner(CosmosClient client, CosmosContainerNames names)
{
    private static readonly string[] _partitionKeyPaths = ["/userId", "/sessionId"];

    private readonly CosmosClient _client = client;
    private readonly CosmosContainerNames _names = names;

    /// <summary>Ensures the database and both containers exist with the partition key, TTL and indexing policy the repositories expect.</summary>
    public async Task EnsureCreatedAsync(CancellationToken cancellationToken)
    {
        Database database = await _client
            .CreateDatabaseIfNotExistsAsync(_names.DatabaseId, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        await database
            .CreateContainerIfNotExistsAsync(Describe(_names.SessionsContainerId, "/state/*"), cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        await database
            .CreateContainerIfNotExistsAsync(Describe(_names.MessagesContainerId, "/message/*"), cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private static ContainerProperties Describe(string containerId, string excludedPath)
    {
        ContainerProperties properties = new(containerId, _partitionKeyPaths)
        {
            // TTL on with no default, so retention can later be set per document without recreating the container.
            DefaultTimeToLive = -1,
        };

        // The excluded path is the one opaque blob each document carries; indexing it costs RUs on every write for nothing.
        properties.IndexingPolicy.ExcludedPaths.Add(new ExcludedPath { Path = excludedPath });

        return properties;
    }
}
