using System.Net;
using Andes.Agents.Entity.Sessions;
using Andes.Agents.Repository.Sessions;
using Andes.Agents.Repository.Sessions.Interfaces;
using Microsoft.Azure.Cosmos;

namespace Andes.Agents.Repository.Cosmos.Sessions;

/// <inheritdoc />
public sealed class CosmosSessionMessageRepository(ICosmosContainers containers) : ISessionMessageRepository
{
    // Cosmos DB caps a transactional batch at 100 operations.
    private const int _batchSize = 100;

    private const string _listQuery = "SELECT * FROM c ORDER BY c.sequence ASC OFFSET @skip LIMIT @take";
    private const string _latestQuery = "SELECT TOP @count * FROM c ORDER BY c.sequence DESC";
    private const string _maxSequenceQuery = "SELECT VALUE MAX(c.sequence) FROM c";
    private const string _idsQuery = "SELECT VALUE c.id FROM c";

    private readonly Container _messages = containers.Messages;

    /// <inheritdoc />
    public async Task AppendAsync(IReadOnlyList<SessionMessageDocument> documents, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(documents);

        if (documents.Count == 0)
        {
            return;
        }

        SessionMessageDocument first = documents[0];

        if (documents.Any(document => document.UserId != first.UserId || document.SessionId != first.SessionId))
        {
            throw new ArgumentException("Every message must belong to the same session.", nameof(documents));
        }

        PartitionKey partitionKey = PartitionKeys.ForSession(first.UserId, first.SessionId);

        foreach (SessionMessageDocument[] chunk in documents.Chunk(_batchSize))
        {
            TransactionalBatch batch = _messages.CreateTransactionalBatch(partitionKey);

            foreach (SessionMessageDocument document in chunk)
            {
                batch.CreateItem(document);
            }

            await ExecuteAsync(batch, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<SessionMessageDocument>> ListAsync(string userId, string sessionId, int skip, int take, CancellationToken cancellationToken)
    {
        QueryDefinition query = new QueryDefinition(_listQuery)
            .WithParameter("@skip", skip)
            .WithParameter("@take", take);

        return CosmosQueries.ReadAllAsync<SessionMessageDocument>(_messages, query, PartitionKeys.ForSession(userId, sessionId), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SessionMessageDocument>> ListLatestAsync(string userId, string sessionId, int count, CancellationToken cancellationToken)
    {
        QueryDefinition query = new QueryDefinition(_latestQuery).WithParameter("@count", count);

        IReadOnlyList<SessionMessageDocument> newestFirst = await CosmosQueries
            .ReadAllAsync<SessionMessageDocument>(_messages, query, PartitionKeys.ForSession(userId, sessionId), cancellationToken)
            .ConfigureAwait(false);

        return [.. newestFirst.Reverse()];
    }

    /// <inheritdoc />
    public async Task<long> MaxSequenceAsync(string userId, string sessionId, CancellationToken cancellationToken)
    {
        long? max = await CosmosQueries
            .ReadScalarAsync<long?>(_messages, new QueryDefinition(_maxSequenceQuery), PartitionKeys.ForSession(userId, sessionId), cancellationToken)
            .ConfigureAwait(false);

        return max ?? 0;
    }

    /// <inheritdoc />
    public async Task DeleteAllAsync(string userId, string sessionId, CancellationToken cancellationToken)
    {
        PartitionKey partitionKey = PartitionKeys.ForSession(userId, sessionId);

        IReadOnlyList<string> ids = await CosmosQueries
            .ReadAllAsync<string>(_messages, new QueryDefinition(_idsQuery), partitionKey, cancellationToken)
            .ConfigureAwait(false);

        foreach (string[] chunk in ids.Chunk(_batchSize))
        {
            TransactionalBatch batch = _messages.CreateTransactionalBatch(partitionKey);

            foreach (string id in chunk)
            {
                batch.DeleteItem(id);
            }

            await ExecuteAsync(batch, cancellationToken).ConfigureAwait(false);
        }
    }

    #region Private Methods

    private static async Task ExecuteAsync(TransactionalBatch batch, CancellationToken cancellationToken)
    {
        using TransactionalBatchResponse response = await batch.ExecuteAsync(cancellationToken).ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            return;
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            throw new SessionConflictException("A message with the same sequence was stored by another request.");
        }

        throw new InvalidOperationException($"The batch write failed with status {(int)response.StatusCode}: {response.ErrorMessage}");
    }

    #endregion
}
