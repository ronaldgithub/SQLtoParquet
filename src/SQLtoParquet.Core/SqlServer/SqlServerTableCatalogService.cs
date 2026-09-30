using Microsoft.Data.SqlClient;

namespace SQLtoParquet.Core.SqlServer;

/// <summary>
/// Lists user tables in the connection's current database along with row counts and
/// data/index space usage, using the same sys.allocation_units-based query sp_spaceused relies on.
/// </summary>
public sealed class SqlServerTableCatalogService : ITableCatalogService
{
    private const string Query = """
        SELECT
            s.name AS SchemaName,
            t.name AS TableName,
            SUM(CASE WHEN i.index_id IN (0, 1) THEN p.rows ELSE 0 END) AS RowCount,
            SUM(CASE WHEN i.index_id IN (0, 1) THEN a.used_pages ELSE 0 END) * 8 AS DataSpaceKb,
            SUM(CASE WHEN i.index_id > 1 THEN a.used_pages ELSE 0 END) * 8 AS IndexSpaceKb,
            SUM(a.total_pages) * 8 AS TotalSpaceKb
        FROM sys.tables t
        INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
        INNER JOIN sys.indexes i ON t.object_id = i.object_id
        INNER JOIN sys.partitions p ON i.object_id = p.object_id AND i.index_id = p.index_id
        INNER JOIN sys.allocation_units a ON p.partition_id = a.container_id
        WHERE t.is_ms_shipped = 0
        GROUP BY s.name, t.name
        ORDER BY s.name, t.name
        """;

    public async Task<IReadOnlyList<TableSizeInfo>> ListTablesAsync(SqlConnection connection, CancellationToken cancellationToken = default)
    {
        var results = new List<TableSizeInfo>();

        await using var command = new SqlCommand(Query, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(new TableSizeInfo(
                Schema: reader.GetString(0),
                Table: reader.GetString(1),
                RowCount: reader.GetInt64(2),
                DataSpaceKb: reader.GetInt64(3),
                IndexSpaceKb: reader.GetInt64(4),
                TotalSpaceKb: reader.GetInt64(5)));
        }

        return results;
    }
}
