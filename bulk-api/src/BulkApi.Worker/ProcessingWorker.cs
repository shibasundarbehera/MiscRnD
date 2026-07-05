using BulkApi.Core.Configuration;
using BulkApi.Core.Processing;
using BulkApi.Core.Repositories;
using Microsoft.Extensions.Options;

namespace BulkApi.Worker;

public sealed class ProcessingWorker : BackgroundService
{
    private readonly ITaskRepository _repository;
    private readonly IFileProcessor _processor;
    private readonly WorkerOptions _options;
    private readonly ILogger<ProcessingWorker> _logger;
    private readonly string _workerId = $"{Environment.MachineName}-{Guid.NewGuid():N}"[..24];

    public ProcessingWorker(
        ITaskRepository repository,
        IFileProcessor processor,
        IOptions<WorkerOptions> options,
        ILogger<ProcessingWorker> logger)
    {
        _repository = repository;
        _processor = processor;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker {WorkerId} started (max concurrent: {MaxConcurrent})",
            _workerId, _options.MaxConcurrentTasks);

        using var semaphore = new SemaphoreSlim(_options.MaxConcurrentTasks);

        while (!stoppingToken.IsCancellationRequested)
        {
            await semaphore.WaitAsync(stoppingToken);

            var task = await _repository.LeaseNextAsync(
                _workerId,
                _options.LeaseDurationSeconds,
                stoppingToken);

            if (task is null)
            {
                semaphore.Release();
                await Task.Delay(_options.PollIntervalMs, stoppingToken);
                continue;
            }

            _ = ProcessTaskAsync(task, semaphore, stoppingToken);
        }
    }

    private async Task ProcessTaskAsync(
        Core.Models.IngestTask task,
        SemaphoreSlim semaphore,
        CancellationToken ct)
    {
        try
        {
            _logger.LogInformation(
                "Worker {WorkerId} processing task {TaskId}: {S3Uri}",
                _workerId, task.Id, task.S3Uri);

            var rowsProcessed = await _processor.ProcessAsync(task, ct);
            await _repository.MarkCompletedAsync(task.Id!, rowsProcessed, ct);

            _logger.LogInformation(
                "Task {TaskId} completed: {RowCount} rows",
                task.Id, rowsProcessed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Task {TaskId} failed", task.Id);
            await _repository.MarkFailedAsync(task.Id!, ex.Message, ct);
        }
        finally
        {
            semaphore.Release();
        }
    }
}
