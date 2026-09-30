namespace SQLtoParquet.Core.Export;

public sealed record TableExportJob(string Schema, string Table, long? EstimatedTotalRows, IProgress<TableExportProgress>? Progress);
