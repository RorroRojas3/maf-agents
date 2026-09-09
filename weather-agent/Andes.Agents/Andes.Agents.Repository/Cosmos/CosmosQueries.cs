using Microsoft.Azure.Cosmos;

namespace Andes.Agents.Repository.Cosmos;

internal static class CosmosQueries
{
    public static async Task<IReadOnlyList<T>> ReadAllAsync<T>(
        Container container,
        QueryDefinition query,
        PartitionKey partitionKey,
        CancellationToken cancellationToken)
    {
        List<T> items = [];

        using FeedIterator<T> iterator = container.GetItemQueryIterator<T>(
            query,
            requestOptions: new QueryRequestOptions { PartitionKey = partitionKey });

        while (iterator.HasMoreResults)
        {
            FeedResponse<T> page = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            items.AddRange(page);
        }

        return items;
    }

    public static async Task<T?> ReadScalarAsync<T>(
        Container container,
        QueryDefinition query,
        PartitionKey partitionKey,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<T> rows = await ReadAllAsync<T>(container, query, partitionKey, cancellationToken).ConfigureAwait(false);

        return rows.Count > 0 ? rows[0] : default;
    }
}
