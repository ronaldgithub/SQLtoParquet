namespace SQLtoParquet.Core.Export;

public sealed record ExportResult(
    string TableName,
    bool Success,
    long RowsExported,
    string? OutputFilePath,
    long OutputFileSizeBytes,
    TimeSpan Duration,
    string? Error = null);
