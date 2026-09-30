using Microsoft.Data.SqlClient;

namespace SQLtoParquet.Core.SqlServer;

public interface ITableCatalogService
{
    Task<IReadOnlyList<TableSizeInfo>> ListTablesAsync(SqlConnection connection, CancellationToken cancellationToken = default);
}
