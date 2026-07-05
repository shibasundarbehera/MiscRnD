namespace BulkApi.Core.Configuration;

public sealed class WorkerOptions
{
    public const string SectionName = "Worker";

    public int PollIntervalMs { get; set; } = 500;

    public int LeaseDurationSeconds { get; set; } = 300;

    public int BulkWriteBatchSize { get; set; } = 5000;

    public int MaxConcurrentTasks { get; set; } = 4;

    public string[] IdempotencyKeyFields { get; set; } = ["id", "source_id"];
}
