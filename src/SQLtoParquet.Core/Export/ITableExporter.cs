using Microsoft.Data.SqlClient;
using SQLtoParquet.Core.Export.Destinations;

namespace SQLtoParquet.Core.Export;

public interface ITableExporter
{
    Task<ExportResult> ExportAsync(
        SqlConnection connection,
        string schema,
        string table,
        IExportDestination destination,
        TableExportOptions options,
        IProgress<TableExportProgress>? progress,
        long? estimatedTotalRows,
        CancellationToken cancellationToken);
}
