using Microsoft.Azure.Cosmos;

namespace Andes.Agents.Repository.Cosmos;

/// <summary>Builds the hierarchical partition keys of the session containers, <c>[/userId, /sessionId]</c>.</summary>
public static class PartitionKeys
{
    /// <summary>The full key of one session's documents.</summary>
    public static PartitionKey ForSession(string userId, string sessionId) =>
        new PartitionKeyBuilder().Add(userId).Add(sessionId).Build();

    /// <summary>The prefix key spanning every session of one user.</summary>
    public static PartitionKey ForUser(string userId) =>
        new PartitionKeyBuilder().Add(userId).Build();
}
