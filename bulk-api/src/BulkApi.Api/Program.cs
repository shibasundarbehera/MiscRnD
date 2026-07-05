using BulkApi.Api.Endpoints;
using BulkApi.Core.Extensions;
using BulkApi.Core.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddBulkApiCore(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var repository = scope.ServiceProvider.GetRequiredService<ITaskRepository>();
    await repository.EnsureIndexesAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapIngestEndpoints();

app.Run();
