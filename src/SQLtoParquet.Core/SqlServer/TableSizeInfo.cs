namespace SQLtoParquet.Core.SqlServer;

/// <summary>
/// Size and row-count info for one table, as reported by SQL Server (sys.allocation_units-based,
/// the same data <c>sp_spaceused</c> is built on). DataSpaceKb covers the heap/clustered index
/// (the actual row data); IndexSpaceKb covers nonclustered indexes only.
/// </summary>
public sealed record TableSizeInfo(
    string Schema,
    string Table,
    long RowCount,
    long DataSpaceKb,
    long IndexSpaceKb,
    long TotalSpaceKb)
{
    public string QualifiedName => $"{Schema}.{Table}";
}
