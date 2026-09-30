using Parquet;
using Parquet.Schema;
using SQLtoParquet.Core.Export;
using Xunit;

namespace SQLtoParquet.Core.Tests.Export;

/// <summary>
/// Exercises the exact write path <see cref="TableExporter"/> uses (schema built by
/// <see cref="SqlToParquetTypeMapper"/>, buffered per-column, flushed via
/// <see cref="ColumnBufferFactory"/> into a real Parquet row group) without needing a live SQL Server
/// connection, then reads the bytes back through Parquet.Net's own typed row-group reader to confirm
/// round-tripping — including nulls — actually works. Parquet.Net normalizes string/byte[] DataFields to
/// ReadOnlyMemory&lt;char&gt;/ReadOnlyMemory&lt;byte&gt; internally (see SqlToParquetTypeMapperTests), so
/// this guards against that — or any other Parquet.Net API quirk — silently breaking writes.
/// </summary>
public class ParquetWritePipelineTests
{
    private static readonly FakeDbColumn[] Columns =
    [
        new("Id", "int"),
        new("Name", "nvarchar"),
        new("Blob", "varbinary"),
        new("Price", "decimal", numericPrecision: 10, numericScale: 2),
        new("CreatedAt", "datetime2"),
        new("Active", "bit"),
    ];

    private static readonly (int Id, string? Name, byte[]? Blob, decimal? Price, DateTime? CreatedAt, bool? Active)[] Rows =
    [
        (1, "Alice", [1, 2, 3], 12.34m, new DateTime(2024, 1, 15, 10, 30, 0), true),
        (2, null, null, null, null, null),
        (3, "Bob", [9, 9], 0.5m, new DateTime(2020, 6, 1), false),
    ];

    [Fact]
    public async Task WriteThenRead_RoundTripsAllRowsAndColumns_IncludingNulls()
    {
        var mappings = SqlToParquetTypeMapper.BuildMappings(Columns);
        var schema = new ParquetSchema(mappings.Select(m => (Field)m.Field).ToArray());
        var buffers = mappings.Select(m => ColumnBufferFactory.Create(m.Kind)).ToArray();

        foreach (var row in Rows)
        {
            buffers[0].Add(row.Id);
            buffers[1].Add(row.Name);
            buffers[2].Add(row.Blob);
            buffers[3].Add(row.Price);
            buffers[4].Add(row.CreatedAt);
            buffers[5].Add(row.Active);
        }

        using var stream = new MemoryStream();
        await using (var writer = await ParquetWriter.CreateAsync(schema, stream, new ParquetOptions { CompressionMethod = CompressionMethod.Snappy }, cancellationToken: default))
        {
            using var rowGroupWriter = writer.CreateRowGroup();
            for (int i = 0; i < mappings.Count; i++)
                await buffers[i].FlushAsync(rowGroupWriter, mappings[i].Field, default);
            rowGroupWriter.CompleteValidate();
        }

        stream.Position = 0;
        await using var reader = await ParquetReader.CreateAsync(stream);
        using var rowGroupReader = reader.OpenRowGroupReader(0);
        int rowCount = checked((int)rowGroupReader.RowCount);
        Assert.Equal(Rows.Length, rowCount);

        var ids = new int?[rowCount];
        await rowGroupReader.ReadAsync(mappings[0].Field, ids.AsMemory(), null, default);

        var names = new string?[rowCount];
        await rowGroupReader.ReadAsync(mappings[1].Field, names.AsMemory()!, null, default);

        var blobs = new byte[]?[rowCount];
        await rowGroupReader.ReadAsync(mappings[2].Field, blobs.AsMemory()!, null, default);

        var prices = new decimal?[rowCount];
        await rowGroupReader.ReadAsync(mappings[3].Field, prices.AsMemory(), null, default);

        var createdAts = new DateTime?[rowCount];
        await rowGroupReader.ReadAsync(mappings[4].Field, createdAts.AsMemory(), null, default);

        var actives = new bool?[rowCount];
        await rowGroupReader.ReadAsync(mappings[5].Field, actives.AsMemory(), null, default);

        for (int i = 0; i < Rows.Length; i++)
        {
            var expected = Rows[i];
            Assert.Equal(expected.Id, ids[i]);
            Assert.Equal(expected.Name, names[i]);
            Assert.Equal(expected.Blob, blobs[i]);
            Assert.Equal(expected.Price, prices[i]);
            Assert.Equal(expected.CreatedAt, createdAts[i]);
            Assert.Equal(expected.Active, actives[i]);
        }
    }
}
