using System.Runtime.CompilerServices;
using Parquet;
using Parquet.Data;
using Parquet.Schema;

namespace BulkApi.Core.Reading;

public sealed class ParquetFileReader : IFileReader
{
    public string Format => "parquet";

    public bool CanRead(string objectKey) =>
        objectKey.EndsWith(".parquet", StringComparison.OrdinalIgnoreCase);

    public async IAsyncEnumerable<Dictionary<string, object?>> ReadRowsAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await using var readable = await EnsureSeekableAsync(stream, ct);
        await using var reader = await ParquetReader.CreateAsync(readable, cancellationToken: ct);

        var fields = reader.Schema.GetDataFields();

        for (var rg = 0; rg < reader.RowGroupCount; rg++)
        {
            ct.ThrowIfCancellationRequested();

            using var rowGroup = reader.OpenRowGroupReader(rg);
            var rowCount = (int)rowGroup.RowCount;
            var columns = new object?[fields.Length][];

            for (var col = 0; col < fields.Length; col++)
            {
                columns[col] = await ReadColumnValuesAsync(rowGroup, fields[col], rowCount, ct);
            }

            for (var row = 0; row < rowCount; row++)
            {
                var dict = new Dictionary<string, object?>(fields.Length, StringComparer.OrdinalIgnoreCase);
                for (var col = 0; col < fields.Length; col++)
                {
                    dict[fields[col].Name] = columns[col][row];
                }

                yield return dict;
            }
        }
    }

    private static async Task<object?[]> ReadColumnValuesAsync(
        ParquetRowGroupReader rowGroup,
        DataField field,
        int rowCount,
        CancellationToken ct)
    {
        var raw = await rowGroup.ReadRawColumnDataBaseAsync(field, ct);
        using (raw)
        {
            return ExtractColumnValues(raw, field, rowCount);
        }
    }

    private static object?[] ExtractColumnValues(RawColumnData raw, DataField field, int rowCount)
    {
        var rawType = raw.GetType();
        if (!rawType.IsGenericType)
            return new object?[rowCount];

        if (field.IsNullable && rawType.GetProperty("NullableValues") is { } nullableProp)
        {
            return MemoryToObjectArray(nullableProp.GetValue(raw)!, rowCount);
        }

        if (rawType.GetProperty("Values") is { } valuesProp)
        {
            return MemoryToObjectArray(valuesProp.GetValue(raw)!, rowCount);
        }

        return new object?[rowCount];
    }

    private static object?[] MemoryToObjectArray(object memory, int rowCount)
    {
        var span = memory.GetType().GetProperty("Span")!.GetValue(memory)!;
        var getItem = span.GetType().GetMethod("get_Item")!;
        var result = new object?[rowCount];

        for (var i = 0; i < rowCount; i++)
        {
            result[i] = getItem.Invoke(span, [i]);
        }

        return result;
    }

    private static async Task<Stream> EnsureSeekableAsync(Stream stream, CancellationToken ct)
    {
        if (stream.CanSeek)
            return stream;

        var memory = new MemoryStream();
        await stream.CopyToAsync(memory, ct);
        memory.Position = 0;
        return memory;
    }
}
