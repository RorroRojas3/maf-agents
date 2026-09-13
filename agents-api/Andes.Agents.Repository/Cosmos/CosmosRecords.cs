namespace Andes.Agents.Repository.Cosmos;

/// <summary>Names of the Cosmos DB database and the two containers, one document per session and one per message.</summary>
public sealed record CosmosContainerNames(string DatabaseId, string SessionsContainerId, string MessagesContainerId);
