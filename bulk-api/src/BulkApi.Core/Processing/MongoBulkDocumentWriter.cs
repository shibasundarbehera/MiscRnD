using BulkApi.Core.Configuration;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BulkApi.Core.Processing;

public sealed class MongoBulkDocumentWriter : IBulkDocumentWriter
{
    private readonly IMongoCollection<BsonDocument> _collection;

    public MongoBulkDocumentWriter(IMongoClient client, IOptions<MongoOptions> options)
    {
        var mongoOptions = options.Value;
        _collection = client
            .GetDatabase(mongoOptions.DatabaseName)
            .GetCollection<BsonDocument>(mongoOptions.DataCollection);
    }

    public async Task WriteBatchAsync(
        IReadOnlyList<Dictionary<string, object?>> documents,
        CancellationToken ct = default)
    {
        if (documents.Count == 0)
            return;

        var writes = new List<WriteModel<BsonDocument>>(documents.Count);

        foreach (var doc in documents)
        {
            var bson = new BsonDocument();
            foreach (var (key, value) in doc)
            {
                bson[key] = ToBsonValue(value);
            }

            var filter = Builders<BsonDocument>.Filter.Eq("_id", bson["_id"]);
            writes.Add(new ReplaceOneModel<BsonDocument>(filter, bson) { IsUpsert = true });
        }

        var options = new BulkWriteOptions { IsOrdered = false };
        await _collection.BulkWriteAsync(writes, options, ct);
    }

    private static BsonValue ToBsonValue(object? value) => value switch
    {
        null => BsonNull.Value,
        string s => new BsonString(s),
        bool b => new BsonBoolean(b),
        int i => new BsonInt32(i),
        long l => new BsonInt64(l),
        double d => new BsonDouble(d),
        float f => new BsonDouble(f),
        decimal m => new BsonDecimal128(m),
        DateTime dt => new BsonDateTime(dt),
        DateTimeOffset dto => new BsonDateTime(dto.UtcDateTime),
        Guid g => new BsonBinaryData(g, GuidRepresentation.Standard),
        byte[] bytes => new BsonBinaryData(bytes),
        IEnumerable<object?> list => new BsonArray(list.Select(ToBsonValue)),
        Dictionary<string, object?> dict => new BsonDocument(dict.ToDictionary(
            k => k.Key,
            k => ToBsonValue(k.Value))),
        _ => BsonValue.Create(value)
    };
}
