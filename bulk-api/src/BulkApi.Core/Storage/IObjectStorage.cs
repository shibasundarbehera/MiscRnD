namespace BulkApi.Core.Storage;

public interface IObjectStorage
{
    Task<Stream> OpenReadStreamAsync(string bucket, string key, CancellationToken ct = default);
}
