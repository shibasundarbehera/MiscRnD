using Amazon.S3;
using BulkApi.Core.Configuration;
using BulkApi.Core.Processing;
using BulkApi.Core.Reading;
using BulkApi.Core.Repositories;
using BulkApi.Core.Storage;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace BulkApi.Core.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBulkApiCore(
        this IServiceCollection services,
        Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        services.Configure<MongoOptions>(configuration.GetSection(MongoOptions.SectionName));
        services.Configure<S3Options>(configuration.GetSection(S3Options.SectionName));
        services.Configure<WorkerOptions>(configuration.GetSection(WorkerOptions.SectionName));

        services.AddSingleton<IMongoClient>(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<MongoOptions>>().Value;
            return new MongoClient(options.ConnectionString);
        });

        services.AddSingleton<IAmazonS3>(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<S3Options>>().Value;
            return S3ClientFactory.Create(options);
        });

        services.AddSingleton<ITaskRepository, MongoTaskRepository>();
        services.AddSingleton<IObjectStorage, S3ObjectStorage>();
        services.AddSingleton<IDataMassager, VectorizedDataMassager>();
        services.AddSingleton<IBulkDocumentWriter, MongoBulkDocumentWriter>();
        services.AddSingleton<IFileProcessor, FileProcessor>();

        services.AddSingleton<IFileReader, ParquetFileReader>();
        services.AddSingleton<IFileReader, JsonFileReader>();

        return services;
    }
}
