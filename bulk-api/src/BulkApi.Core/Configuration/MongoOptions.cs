namespace BulkApi.Core.Configuration;

public sealed class MongoOptions
{
    public const string SectionName = "MongoDB";

    public string ConnectionString { get; set; } = "mongodb://localhost:27017";

    public string DatabaseName { get; set; } = "bulk_ingest";

    public string TasksCollection { get; set; } = "ingest_tasks";

    public string DataCollection { get; set; } = "records";
}
