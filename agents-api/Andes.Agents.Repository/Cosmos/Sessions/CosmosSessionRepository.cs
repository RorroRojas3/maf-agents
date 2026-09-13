using System.Net;
using Andes.Agents.Entity.Sessions;
using Andes.Agents.Repository.Sessions;
using Andes.Agents.Repository.Sessions.Interfaces;
using Microsoft.Azure.Cosmos;

namespace Andes.Agents.Repository.Cosmos.Sessions;

/// <inheritdoc />
public sealed class CosmosSessionRepository(ICosmosContainers containers) : ISessionRepository
{
    private const string _listQuery = "SELECT * FROM c ORDER BY c.dateCreated DESC OFFSET @skip LIMIT @take";
    private const string _countQuery = "SELECT VALUE COUNT(1) FROM c";

    private readonly Container _sessions = containers.Sessions;

    /// <inheritdoc />
    public async Task<SessionRead?> GetAsync(string userId, string sessionId, CancellationToken cancellationToken)
    {
        try
        {
            ItemResponse<SessionDocument> response = await _sessions
                .ReadItemAsync<SessionDocument>(sessionId, PartitionKeys.ForSession(userId, sessionId), cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return new SessionRead(response.Resource, response.ETag);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<string> CreateAsync(SessionDocument document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        try
        {
            ItemResponse<SessionDocument> response = await _sessions
                .CreateItemAsync(document, PartitionKeys.ForSession(document.UserId, document.SessionId), cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return response.ETag;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            throw new SessionConflictException("A session with this id was created by another request.", ex);
        }
    }

    /// <inheritdoc />
    public async Task<string> ReplaceAsync(SessionDocument document, string etag, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(etag);

        try
        {
            ItemResponse<SessionDocument> response = await _sessions
                .ReplaceItemAsync(
                    document,
                    document.Id,
                    PartitionKeys.ForSession(document.UserId, document.SessionId),
                    new ItemRequestOptions { IfMatchEtag = etag },
                    cancellationToken)
                .ConfigureAwait(false);

            return response.ETag;
        }
        catch (CosmosException ex) when (ex.StatusCode is HttpStatusCode.PreconditionFailed or HttpStatusCode.NotFound)
        {
            throw new SessionConflictException("The session was modified or deleted by another request.", ex);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<SessionDocument>> ListAsync(string userId, int skip, int take, CancellationToken cancellationToken)
    {
        QueryDefinition query = new QueryDefinition(_listQuery)
            .WithParameter("@skip", skip)
            .WithParameter("@take", take);

        return CosmosQueries.ReadAllAsync<SessionDocument>(_sessions, query, PartitionKeys.ForUser(userId), cancellationToken);
    }

    /// <inheritdoc />
    public Task<int> CountAsync(string userId, CancellationToken cancellationToken) =>
        CosmosQueries.ReadScalarAsync<int>(_sessions, new QueryDefinition(_countQuery), PartitionKeys.ForUser(userId), cancellationToken);

    /// <inheritdoc />
    public async Task DeleteAsync(string userId, string sessionId, CancellationToken cancellationToken)
    {
        try
        {
            await _sessions
                .DeleteItemAsync<SessionDocument>(sessionId, PartitionKeys.ForSession(userId, sessionId), cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Already gone; deleting is idempotent.
        }
    }
}
