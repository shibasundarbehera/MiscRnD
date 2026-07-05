using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace BulkApi.Core.Models;

public sealed class IngestTask
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public required string Bucket { get; set; }

    public required string ObjectKey { get; set; }

    public required string FileFormat { get; set; }

    public string Status { get; set; } = IngestTaskStatus.Pending;

    public string? WorkerId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LeasedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public long? RowsProcessed { get; set; }

    public string? ErrorMessage { get; set; }

    public int AttemptCount { get; set; }

    [BsonIgnore]
    public string S3Uri => $"s3://{Bucket}/{ObjectKey}";
}
