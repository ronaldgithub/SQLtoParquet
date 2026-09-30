namespace SQLtoParquet.Core.Export;

public sealed record TableExportProgress(string TableName, long RowsProcessed, long? EstimatedTotalRows, TimeSpan Elapsed);
