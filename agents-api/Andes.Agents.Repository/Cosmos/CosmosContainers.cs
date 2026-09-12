using Microsoft.Azure.Cosmos;

namespace Andes.Agents.Repository.Cosmos;

/// <summary>The containers the session repositories operate on.</summary>
public interface ICosmosContainers
{
    /// <summary>Container holding one document per session.</summary>
    Container Sessions { get; }

    /// <summary>Container holding one document per message.</summary>
    Container Messages { get; }
}

/// <inheritdoc />
public sealed class CosmosContainers(CosmosClient client, CosmosContainerNames names) : ICosmosContainers
{
    /// <inheritdoc />
    public Container Sessions { get; } = client.GetContainer(names.DatabaseId, names.SessionsContainerId);

    /// <inheritdoc />
    public Container Messages { get; } = client.GetContainer(names.DatabaseId, names.MessagesContainerId);
}
