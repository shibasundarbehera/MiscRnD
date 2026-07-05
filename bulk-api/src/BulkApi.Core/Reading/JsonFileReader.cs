using System.Runtime.CompilerServices;
using System.Text.Json;

namespace BulkApi.Core.Reading;

public sealed class JsonFileReader : IFileReader
{
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };

    public string Format => "json";

    public bool CanRead(string objectKey) =>
        objectKey.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
        objectKey.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase) ||
        objectKey.EndsWith(".ndjson", StringComparison.OrdinalIgnoreCase);

    public async IAsyncEnumerable<Dictionary<string, object?>> ReadRowsAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (IsJsonLines(stream))
        {
            await foreach (var row in ReadJsonLinesAsync(stream, ct))
                yield return row;
            yield break;
        }

        using var document = await JsonDocument.ParseAsync(stream, DocumentOptions, ct);
        if (document.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in document.RootElement.EnumerateArray())
            {
                ct.ThrowIfCancellationRequested();
                yield return JsonElementToDictionary(element);
            }
        }
        else if (document.RootElement.ValueKind == JsonValueKind.Object)
        {
            yield return JsonElementToDictionary(document.RootElement);
        }
    }

    private static async IAsyncEnumerable<Dictionary<string, object?>> ReadJsonLinesAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken ct)
    {
        using var reader = new StreamReader(stream, leaveOpen: true);
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            using var doc = JsonDocument.Parse(line, DocumentOptions);
            yield return JsonElementToDictionary(doc.RootElement);
        }
    }

    private static bool IsJsonLines(Stream stream)
    {
        if (!stream.CanSeek)
            return true;

        var position = stream.Position;
        var buffer = new byte[1];
        var read = stream.Read(buffer, 0, 1);
        stream.Position = position;

        if (read == 0)
            return false;

        return buffer[0] != (byte)'[' && buffer[0] != (byte)'{';
    }

    internal static Dictionary<string, object?> JsonElementToDictionary(JsonElement element)
    {
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in element.EnumerateObject())
        {
            dict[property.Name] = ConvertJsonValue(property.Value);
        }

        return dict;
    }

    private static object? ConvertJsonValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Null => null,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Number when element.TryGetInt64(out var l) => l,
        JsonValueKind.Number => element.GetDouble(),
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonValue).ToList(),
        JsonValueKind.Object => JsonElementToDictionary(element),
        _ => element.GetRawText()
    };
}
