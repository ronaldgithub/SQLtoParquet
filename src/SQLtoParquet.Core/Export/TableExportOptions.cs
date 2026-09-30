using Parquet;

namespace SQLtoParquet.Core.Export;

public sealed record TableExportOptions
{
    public int RowsPerRowGroup { get; init; } = 100_000;
    public int ReportProgressFrequency { get; init; } = 10_000;
    public CompressionMethod Compression { get; init; } = CompressionMethod.Snappy;
    public int MaxParallelism { get; init; } = 2;
}
