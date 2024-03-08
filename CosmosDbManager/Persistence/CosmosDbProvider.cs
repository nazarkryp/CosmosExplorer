using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Azure.Cosmos;

namespace CosmosDbManager.Persistence;

public class CosmosDatabase
{
    public string Id { get; set; } = null!;
}

public class CosmosContainer
{
    public string Id { get; set; } = null!;
}

public class CosmosResponse<T>
{
    public IReadOnlyCollection<T> Items { get; set; } = [];

    public string? ContinuationToken { get; set; }

    public double RequestCharge { get; set; }
}

public sealed class CosmosDbProvider : IDisposable
{
    private readonly CosmosClient _cosmosClient;

    public CosmosDbProvider(string connectionString)
    {
        var factory = new ClientFactory(connectionString);

        _cosmosClient = factory.GetClient();
    }

    public async Task<IReadOnlyCollection<CosmosDatabase>> FetchDatabasesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var iterator = _cosmosClient.GetDatabaseQueryIterator<CosmosDatabase>();

            var databases = new List<CosmosDatabase>();

            while (iterator.HasMoreResults)
            {
                var items = await iterator.ReadNextAsync();

                foreach (var database in items.Resource)
                {
                    databases.Add(database);
                }
            }

            return databases;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<IReadOnlyCollection<CosmosContainer>> FetchContainersAsync(string database, CancellationToken cancellationToken = default)
    {
        try
        {
            var iterator = _cosmosClient.GetDatabase(database).GetContainerQueryIterator<CosmosContainer>();

            var containers = new List<CosmosContainer>();

            while (iterator.HasMoreResults)
            {
                var items = await iterator.ReadNextAsync();

                foreach (var container in items.Resource)
                {
                    containers.Add(container);
                }
            }

            return containers;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<CosmosResponse<T>> FetchDocumentsAsync<T>(string? query, string databaseId, string containerId, int? limit = 20, string? continuationToken = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var container = _cosmosClient.GetDatabase(databaseId).GetContainer(containerId);

            string innerQuery;

            if (string.IsNullOrEmpty(query))
            {
                innerQuery = "SELECT * FROM c";
            }
            else if (!string.IsNullOrEmpty(query) && query!.StartsWith("SELECT", StringComparison.InvariantCultureIgnoreCase))
            {
                innerQuery = query;
            }
            else
            {
                innerQuery = $"SELECT * FROM c {query}";
            }

            var count = await CountAsync(container, innerQuery, cancellationToken);

            if (count == 0)
            {
                return new CosmosResponse<T>();
            }
            
            var iterator = container.GetItemQueryIterator<T>(innerQuery, requestOptions: new QueryRequestOptions
            {
                MaxItemCount = limit,
            }, continuationToken: continuationToken);

            if (!iterator.HasMoreResults)
            {
                return new CosmosResponse<T>();
            }

            var response = await iterator.ReadNextAsync(cancellationToken: cancellationToken);

            return new CosmosResponse<T>()
            {
                Items = response.Resource.ToArray(),
                ContinuationToken = response.ContinuationToken,
                RequestCharge = response.RequestCharge
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    private async Task<int> CountAsync(Container container, string query, CancellationToken cancellationToken = default)
    {
        try
        {
            var countIterator = container.GetItemQueryIterator<dynamic>(query);

            var count = 0;

            while (countIterator.HasMoreResults)
            {
                var response = await countIterator.ReadNextAsync(cancellationToken);

                count += response.Count;
            }

            return count;
        }
        catch (Exception e)
        {
            throw;
        }
    }

    public void Dispose()
    {
        _cosmosClient.Dispose();
    }
}
