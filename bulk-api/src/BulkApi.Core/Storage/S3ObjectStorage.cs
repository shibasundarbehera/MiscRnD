using Amazon.S3;
using Amazon.S3.Model;
using BulkApi.Core.Configuration;
using Microsoft.Extensions.Options;

namespace BulkApi.Core.Storage;

public sealed class S3ObjectStorage : IObjectStorage
{
    private readonly IAmazonS3 _client;

    public S3ObjectStorage(IAmazonS3 client) => _client = client;

    public async Task<Stream> OpenReadStreamAsync(string bucket, string key, CancellationToken ct = default)
    {
        var response = await _client.GetObjectAsync(new GetObjectRequest
        {
            BucketName = bucket,
            Key = key
        }, ct);

        return response.ResponseStream;
    }
}

public static class S3ClientFactory
{
    public static IAmazonS3 Create(S3Options options)
    {
        var config = new AmazonS3Config
        {
            ServiceURL = options.ServiceUrl,
            ForcePathStyle = options.ForcePathStyle,
            AuthenticationRegion = options.Region
        };

        return new AmazonS3Client(options.AccessKey, options.SecretKey, config);
    }
}
