using BulkApi.Core.Models;

namespace BulkApi.Core.Repositories;

public interface ITaskRepository
{
    Task<string> EnqueueAsync(IngestTask task, CancellationToken ct = default);

    Task<IngestTask?> LeaseNextAsync(string workerId, int leaseDurationSeconds, CancellationToken ct = default);

    Task MarkCompletedAsync(string taskId, long rowsProcessed, CancellationToken ct = default);

    Task MarkFailedAsync(string taskId, string errorMessage, CancellationToken ct = default);

    Task EnsureIndexesAsync(CancellationToken ct = default);
}
