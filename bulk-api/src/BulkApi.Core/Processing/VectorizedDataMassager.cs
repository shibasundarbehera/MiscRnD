using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BulkApi.Core.Configuration;
using Microsoft.Extensions.Options;

namespace BulkApi.Core.Processing;

/// <summary>
/// Vectorized-style batch massage: cleans strings, coerces types, stamps metadata,
/// and generates a deterministic _id for idempotent upserts.
/// </summary>
public sealed class VectorizedDataMassager : IDataMassager
{
    private readonly string[] _idempotencyKeyFields;

    public VectorizedDataMassager(IOptions<WorkerOptions> options)
    {
        _idempotencyKeyFields = options.Value.IdempotencyKeyFields;
    }

    public Dictionary<string, object?> Massage(Dictionary<string, object?> row, string sourceFile)
    {
        var output = new Dictionary<string, object?>(row.Count + 3, StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in row)
        {
            output[key] = CleanValue(value);
        }

        output["_ingested_at"] = DateTime.UtcNow;
        output["_source_file"] = sourceFile;
        output["_id"] = GenerateDeterministicId(row, sourceFile);

        return output;
    }

    private static object? CleanValue(object? value) => value switch
    {
        null => null,
        string s => CleanString(s),
        double d when double.IsNaN(d) || double.IsInfinity(d) => null,
        float f when float.IsNaN(f) || float.IsInfinity(f) => null,
        DateTime dt => DateTime.SpecifyKind(dt, DateTimeKind.Utc),
        _ => value
    };

    private static string CleanString(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value.Trim();
    }

    private string GenerateDeterministicId(Dictionary<string, object?> row, string sourceFile)
    {
        var sb = new StringBuilder(256);
        sb.Append(sourceFile).Append('|');

        foreach (var field in _idempotencyKeyFields)
        {
            if (row.TryGetValue(field, out var val) && val is not null)
            {
                sb.Append(field).Append('=').Append(FormatForHash(val)).Append('|');
            }
        }

        if (sb.Length == sourceFile.Length + 1)
        {
            foreach (var (key, val) in row.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                if (val is not null)
                    sb.Append(key).Append('=').Append(FormatForHash(val)).Append('|');
            }
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string FormatForHash(object value) => value switch
    {
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
        _ => value.ToString() ?? string.Empty
    };
}
