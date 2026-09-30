using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Parquet;
using Parquet.Schema;
using SQLtoParquet.Core.Export.Destinations;

namespace SQLtoParquet.Core.Export;

/// <summary>
/// Streams one table's rows from SQL Server into a Parquet file: schema is discovered from the
/// reader (<see cref="SqlToParquetTypeMapper"/>), rows are buffered per-column in memory and flushed
/// as a Parquet row group every <see cref="TableExportOptions.RowsPerRowGroup"/> rows (plus a final
/// partial group), so the whole table is never held in memory at once. Ported from the reference
/// PowerShell exporter's batching/progress approach — see CLAUDE.md.
/// </summary>
public sealed class TableExporter : ITableExporter
{
    public async Task<ExportResult> ExportAsync(
        SqlConnection connection,
        string schema,
        string table,
        IExportDestination destination,
        TableExportOptions options,
        IProgress<TableExportProgress>? progress,
        long? estimatedTotalRows,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        string tableName = $"{schema}.{table}";

        try
        {
            await using var command = new SqlCommand($"SELECT * FROM [{schema}].[{table}]", connection) { CommandTimeout = 0 };
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            var columns = await reader.GetColumnSchemaAsync(cancellationToken).ConfigureAwait(false);
            IReadOnlyList<ColumnMapping> mappings = SqlToParquetTypeMapper.BuildMappings(columns);

            var parquetSchema = new ParquetSchema(mappings.Select(m => (Field)m.Field).ToArray());
            var buffers = mappings.Select(m => ColumnBufferFactory.Create(m.Kind)).ToArray();
            var parquetOptions = new ParquetOptions { CompressionMethod = options.Compression };

            long rowsInGroup = 0;
            long totalRows = 0;

            ExportedFileHandle handle = await destination.WriteAsync($"{schema}_{table}", async (stream, ct) =>
            {
                await using ParquetWriter writer = await ParquetWriter.CreateAsync(parquetSchema, stream, parquetOptions, cancellationToken: ct).ConfigureAwait(false);

                async Task FlushRowGroupAsync()
                {
                    if (rowsInGroup == 0)
                        return;

                    using ParquetRowGroupWriter rowGroupWriter = writer.CreateRowGroup();
                    for (int i = 0; i < mappings.Count; i++)
                    {
                        await buffers[i].FlushAsync(rowGroupWriter, mappings[i].Field, ct).ConfigureAwait(false);
                        buffers[i].Clear();
                    }

                    rowGroupWriter.CompleteValidate();
                    rowsInGroup = 0;
                }

                while (await reader.ReadAsync(ct).ConfigureAwait(false))
                {
                    for (int i = 0; i < mappings.Count; i++)
                        buffers[i].Add(SqlValueReader.Read(reader, i, mappings[i].Kind));

                    totalRows++;
                    rowsInGroup++;

                    if (totalRows % options.ReportProgressFrequency == 0)
                        progress?.Report(new TableExportProgress(tableName, totalRows, estimatedTotalRows, stopwatch.Elapsed));

                    if (rowsInGroup >= options.RowsPerRowGroup)
                        await FlushRowGroupAsync().ConfigureAwait(false);
                }

                await FlushRowGroupAsync().ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);

            progress?.Report(new TableExportProgress(tableName, totalRows, estimatedTotalRows, stopwatch.Elapsed));

            long fileSize = await destination.GetResultingSizeAsync(handle, cancellationToken).ConfigureAwait(false);
            return new ExportResult(tableName, Success: true, totalRows, handle.Path, fileSize, stopwatch.Elapsed);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new ExportResult(tableName, Success: false, 0, null, 0, stopwatch.Elapsed, ex.Message);
        }
    }
}
