using BulkApi.Core.Models;
using BulkApi.Core.Repositories;

namespace BulkApi.Api.Endpoints;

public static class IngestEndpoints
{
    public static void MapIngestEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/ingest");

        group.MapPost("/s3-event", HandleS3Event)
            .WithName("S3ObjectCreatedWebhook")
            .WithSummary("Receives S3/MinIO ObjectCreated notifications")
            .Produces<AcceptedResponse>(StatusCodes.Status202Accepted);

        group.MapPost("/file", HandleManualEnqueue)
            .WithName("EnqueueFile")
            .WithSummary("Manually enqueue a file for bulk processing")
            .Produces<AcceptedResponse>(StatusCodes.Status202Accepted);

        group.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
            .WithName("HealthCheck");
    }

    private static async Task<IResult> HandleS3Event(
        S3EventNotification notification,
        ITaskRepository repository,
        CancellationToken ct)
    {
        var enqueued = new List<AcceptedTask>();

        foreach (var record in notification.Records)
        {
            if (record.S3?.Bucket?.Name is null || record.S3.Object?.Key is null)
                continue;

            if (record.EventName is not null &&
                !record.EventName.StartsWith("ObjectCreated", StringComparison.OrdinalIgnoreCase))
                continue;

            var key = Uri.UnescapeDataString(record.S3.Object.Key);
            var task = CreateTask(record.S3.Bucket.Name, key);
            var taskId = await repository.EnqueueAsync(task, ct);

            enqueued.Add(new AcceptedTask(taskId, task.S3Uri, task.Status));
        }

        return Results.Accepted("/api/v1/ingest", new AcceptedResponse(enqueued));
    }

    private static async Task<IResult> HandleManualEnqueue(
        EnqueueFileRequest request,
        ITaskRepository repository,
        CancellationToken ct)
    {
        var task = CreateTask(request.Bucket, request.ObjectKey);
        var taskId = await repository.EnqueueAsync(task, ct);

        return Results.Accepted("/api/v1/ingest", new AcceptedResponse([
            new AcceptedTask(taskId, task.S3Uri, task.Status)
        ]));
    }

    private static IngestTask CreateTask(string bucket, string objectKey)
    {
        var format = DetectFormat(objectKey);
        return new IngestTask
        {
            Bucket = bucket,
            ObjectKey = objectKey,
            FileFormat = format
        };
    }

    private static string DetectFormat(string objectKey)
    {
        if (objectKey.EndsWith(".parquet", StringComparison.OrdinalIgnoreCase))
            return "parquet";

        if (objectKey.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
            objectKey.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase) ||
            objectKey.EndsWith(".ndjson", StringComparison.OrdinalIgnoreCase))
            return "json";

        return "unknown";
    }
}

public sealed record EnqueueFileRequest(string Bucket, string ObjectKey);

public sealed record AcceptedTask(string TaskId, string S3Uri, string Status);

public sealed record AcceptedResponse(IReadOnlyList<AcceptedTask> Tasks);
