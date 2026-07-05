using BulkApi.Core.Extensions;
using BulkApi.Core.Repositories;
using BulkApi.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddBulkApiCore(builder.Configuration);
builder.Services.AddHostedService<ProcessingWorker>();

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var repository = scope.ServiceProvider.GetRequiredService<ITaskRepository>();
    await repository.EnsureIndexesAsync();
}

host.Run();
