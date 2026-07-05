namespace BulkApi.Core.Configuration;

public sealed class S3Options
{
    public const string SectionName = "S3";

    public string ServiceUrl { get; set; } = "http://localhost:9000";

    public string AccessKey { get; set; } = "minioadmin";

    public string SecretKey { get; set; } = "minioadmin";

    public string Region { get; set; } = "us-east-1";

    public bool ForcePathStyle { get; set; } = true;
}
