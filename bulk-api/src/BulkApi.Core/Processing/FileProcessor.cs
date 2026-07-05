using BulkApi.Core.Configuration;
using BulkApi.Core.Models;
using BulkApi.Core.Reading;
using BulkApi.Core.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BulkApi.Core.Processing;

public sealed class FileProcessor : IFileProcessor
{
    private readonly IObjectStorage _storage;
    private readonly IEnumerable<IFileReader> _readers;
    private readonly IDataMassager _massager;
    private readonly IBulkDocumentWriter _writer;
    private readonly int _batchSize;
    private readonly ILogger<FileProcessor> _logger;

    public FileProcessor(
        IObjectStorage storage,
        IEnumerable<IFileReader> readers,
        IDataMassager massager,
        IBulkDocumentWriter writer,
        IOptions<WorkerOptions> options,
        ILogger<FileProcessor> logger)
    {
        _storage = storage;
        _readers = readers;
        _massager = massager;
        _writer = writer;
        _batchSize = options.Value.BulkWriteBatchSize;
        _logger = logger;
    }

    public async Task<long> ProcessAsync(IngestTask task, CancellationToken ct = default)
    {
        var reader = ResolveReader(task.ObjectKey);
        await using var stream = await _storage.OpenReadStreamAsync(task.Bucket, task.ObjectKey, ct);

        var batch = new List<Dictionary<string, object?>>(_batchSize);
        long totalRows = 0;

        await foreach (var row in reader.ReadRowsAsync(stream, ct))
        {
            batch.Add(_massager.Massage(row, task.S3Uri));

            if (batch.Count >= _batchSize)
            {
                await _writer.WriteBatchAsync(batch, ct);
                totalRows += batch.Count;
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            await _writer.WriteBatchAsync(batch, ct);
            totalRows += batch.Count;
        }

        _logger.LogInformation(
            "Processed {RowCount} rows from s3://{Bucket}/{Key}",
            totalRows, task.Bucket, task.ObjectKey);

        return totalRows;
    }

    private IFileReader ResolveReader(string objectKey)
    {
        var reader = _readers.FirstOrDefault(r => r.CanRead(objectKey));
        if (reader is null)
        {
            throw new NotSupportedException(
                $"No reader registered for object key: {objectKey}. Supported: .parquet, .json, .jsonl, .ndjson");
        }

        return reader;
    }
}
