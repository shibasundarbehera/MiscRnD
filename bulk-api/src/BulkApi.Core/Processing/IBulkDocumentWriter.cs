using MongoDB.Driver;

namespace BulkApi.Core.Processing;

public interface IBulkDocumentWriter
{
    Task WriteBatchAsync(IReadOnlyList<Dictionary<string, object?>> documents, CancellationToken ct = default);
}
