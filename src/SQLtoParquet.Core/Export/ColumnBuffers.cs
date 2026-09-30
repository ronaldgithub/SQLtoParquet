using Parquet;
using Parquet.Schema;

namespace SQLtoParquet.Core.Export;

/// <summary>
/// Buffers one column's values in memory between row-group flushes, then writes them through
/// whichever <see cref="ParquetRowGroupWriter.WriteAsync"/> overload matches its CLR shape.
/// Parquet.Net (v6) has no more generic-object <c>DataColumn</c> type — writing is done through
/// typed overloads, hence one buffer implementation per CLR shape rather than a single generic one.
/// </summary>
internal interface IColumnBuffer
{
    void Add(object? value);
    Task FlushAsync(ParquetRowGroupWriter writer, DataField field, CancellationToken cancellationToken);
    void Clear();
}

/// <summary>Buffer for value types (int, decimal, DateTime, etc.) — written via the nullable generic overload.</summary>
internal sealed class ValueColumnBuffer<T> : IColumnBuffer where T : struct
{
    private readonly List<T?> values = [];

    public void Add(object? value) => values.Add(value is null ? null : (T)value);

    public async Task FlushAsync(ParquetRowGroupWriter writer, DataField field, CancellationToken cancellationToken)
    {
        await writer.WriteAsync(field, new ReadOnlyMemory<T?>([.. values]), null, null, cancellationToken).ConfigureAwait(false);
    }

    public void Clear() => values.Clear();
}

internal sealed class StringColumnBuffer : IColumnBuffer
{
    private readonly List<string?> values = [];

    public void Add(object? value) => values.Add((string?)value);

    public Task FlushAsync(ParquetRowGroupWriter writer, DataField field, CancellationToken cancellationToken) =>
        writer.WriteAsync(field, values!, null);

    public void Clear() => values.Clear();
}

internal sealed class ByteArrayColumnBuffer : IColumnBuffer
{
    private readonly List<byte[]?> values = [];

    public void Add(object? value) => values.Add((byte[]?)value);

    public Task FlushAsync(ParquetRowGroupWriter writer, DataField field, CancellationToken cancellationToken) =>
        writer.WriteAsync(field, values!, null);

    public void Clear() => values.Clear();
}

internal static class ColumnBufferFactory
{
    public static IColumnBuffer Create(ParquetValueKind kind) => kind switch
    {
        ParquetValueKind.Int32 => new ValueColumnBuffer<int>(),
        ParquetValueKind.Byte => new ValueColumnBuffer<byte>(),
        ParquetValueKind.Int16 => new ValueColumnBuffer<short>(),
        ParquetValueKind.Int64 => new ValueColumnBuffer<long>(),
        ParquetValueKind.Boolean => new ValueColumnBuffer<bool>(),
        ParquetValueKind.Double => new ValueColumnBuffer<double>(),
        ParquetValueKind.Single => new ValueColumnBuffer<float>(),
        ParquetValueKind.Decimal => new ValueColumnBuffer<decimal>(),
        ParquetValueKind.DateTime => new ValueColumnBuffer<DateTime>(),
        ParquetValueKind.TimeMicros => new ValueColumnBuffer<long>(),
        ParquetValueKind.Guid => new StringColumnBuffer(),
        ParquetValueKind.DateTimeOffsetString => new StringColumnBuffer(),
        ParquetValueKind.StringValue => new StringColumnBuffer(),
        ParquetValueKind.ByteArray => new ByteArrayColumnBuffer(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };
}
