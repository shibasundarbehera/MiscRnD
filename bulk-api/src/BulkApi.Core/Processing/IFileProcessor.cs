using BulkApi.Core.Models;

namespace BulkApi.Core.Processing;

public interface IFileProcessor
{
    Task<long> ProcessAsync(IngestTask task, CancellationToken ct = default);
}
