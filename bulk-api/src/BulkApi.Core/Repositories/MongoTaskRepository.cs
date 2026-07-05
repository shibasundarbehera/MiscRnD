using BulkApi.Core.Configuration;
using BulkApi.Core.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace BulkApi.Core.Repositories;

public sealed class MongoTaskRepository : ITaskRepository
{
    private readonly IMongoCollection<IngestTask> _collection;

    public MongoTaskRepository(IMongoClient client, IOptions<MongoOptions> options)
    {
        var mongoOptions = options.Value;
        var database = client.GetDatabase(mongoOptions.DatabaseName);
        _collection = database.GetCollection<IngestTask>(mongoOptions.TasksCollection);
    }

    public async Task EnsureIndexesAsync(CancellationToken ct = default)
    {
        var statusIndex = new CreateIndexModel<IngestTask>(
            Builders<IngestTask>.IndexKeys.Ascending(t => t.Status).Ascending(t => t.CreatedAt));

        var dedupeIndex = new CreateIndexModel<IngestTask>(
            Builders<IngestTask>.IndexKeys
                .Ascending(t => t.Bucket)
                .Ascending(t => t.ObjectKey),
            new CreateIndexOptions { Unique = true });

        await _collection.Indexes.CreateManyAsync([statusIndex, dedupeIndex], ct);
    }

    public async Task<string> EnqueueAsync(IngestTask task, CancellationToken ct = default)
    {
        try
        {
            await _collection.InsertOneAsync(task, cancellationToken: ct);
            return task.Id!;
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            var existing = await _collection
                .Find(t => t.Bucket == task.Bucket && t.ObjectKey == task.ObjectKey)
                .FirstOrDefaultAsync(ct);

            return existing?.Id
                ?? throw new InvalidOperationException("Duplicate key conflict but task not found.");
        }
    }

    public async Task<IngestTask?> LeaseNextAsync(
        string workerId,
        int leaseDurationSeconds,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var leaseExpiry = now.AddSeconds(-leaseDurationSeconds);

        var filter = Builders<IngestTask>.Filter.Or(
            Builders<IngestTask>.Filter.Eq(t => t.Status, IngestTaskStatus.Pending),
            Builders<IngestTask>.Filter.And(
                Builders<IngestTask>.Filter.Eq(t => t.Status, IngestTaskStatus.Processing),
                Builders<IngestTask>.Filter.Lt(t => t.LeasedAt, leaseExpiry)));

        var update = Builders<IngestTask>.Update
            .Set(t => t.Status, IngestTaskStatus.Processing)
            .Set(t => t.WorkerId, workerId)
            .Set(t => t.LeasedAt, now)
            .Inc(t => t.AttemptCount, 1);

        var options = new FindOneAndUpdateOptions<IngestTask>
        {
            ReturnDocument = ReturnDocument.After,
            Sort = Builders<IngestTask>.Sort.Ascending(t => t.CreatedAt)
        };

        return await _collection.FindOneAndUpdateAsync(filter, update, options, ct);
    }

    public Task MarkCompletedAsync(string taskId, long rowsProcessed, CancellationToken ct = default)
    {
        var update = Builders<IngestTask>.Update
            .Set(t => t.Status, IngestTaskStatus.Completed)
            .Set(t => t.CompletedAt, DateTime.UtcNow)
            .Set(t => t.RowsProcessed, rowsProcessed);

        return _collection.UpdateOneAsync(t => t.Id == taskId, update, cancellationToken: ct);
    }

    public Task MarkFailedAsync(string taskId, string errorMessage, CancellationToken ct = default)
    {
        var update = Builders<IngestTask>.Update
            .Set(t => t.Status, IngestTaskStatus.Failed)
            .Set(t => t.ErrorMessage, errorMessage)
            .Set(t => t.CompletedAt, DateTime.UtcNow);

        return _collection.UpdateOneAsync(t => t.Id == taskId, update, cancellationToken: ct);
    }
}
