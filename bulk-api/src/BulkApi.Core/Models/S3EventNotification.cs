using System.Text.Json.Serialization;

namespace BulkApi.Core.Models;

public sealed class S3EventNotification
{
    [JsonPropertyName("Records")]
    public List<S3EventRecord> Records { get; set; } = [];
}

public sealed class S3EventRecord
{
    [JsonPropertyName("eventName")]
    public string? EventName { get; set; }

    [JsonPropertyName("s3")]
    public S3Entity? S3 { get; set; }
}

public sealed class S3Entity
{
    [JsonPropertyName("bucket")]
    public S3Bucket? Bucket { get; set; }

    [JsonPropertyName("object")]
    public S3ObjectRef? Object { get; set; }
}

public sealed class S3Bucket
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

public sealed class S3ObjectRef
{
    [JsonPropertyName("key")]
    public string? Key { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }
}
