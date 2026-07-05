namespace BulkApi.Core.Reading;

public interface IFileReader
{
    string Format { get; }

    bool CanRead(string objectKey);

    IAsyncEnumerable<Dictionary<string, object?>> ReadRowsAsync(Stream stream, CancellationToken ct = default);
}
