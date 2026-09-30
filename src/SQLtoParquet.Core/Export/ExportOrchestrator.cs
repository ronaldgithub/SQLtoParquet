using Microsoft.Data.SqlClient;
using SQLtoParquet.Core.Export.Destinations;

namespace SQLtoParquet.Core.Export;

/// <summary>
/// Runs several table exports with limited concurrency (<see cref="TableExportOptions.MaxParallelism"/>).
/// Each concurrent export opens its own <see cref="SqlConnection"/> — a single connection can't serve
/// multiple simultaneous readers. A per-table failure is caught inside <see cref="TableExporter"/> and
/// comes back as a failed <see cref="ExportResult"/> rather than aborting its siblings; only cancelling
/// the shared token stops the batch early.
/// </summary>
public sealed class ExportOrchestrator(ITableExporter exporter)
{
    public async Task<IReadOnlyList<ExportResult>> RunAsync(
        string connectionString,
        IReadOnlyList<TableExportJob> jobs,
        IExportDestination destination,
        TableExportOptions options,
        CancellationToken cancellationToken)
    {
        using var semaphore = new SemaphoreSlim(Math.Max(1, options.MaxParallelism));

        var tasks = jobs.Select(async job =>
        {
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

                return await exporter.ExportAsync(
                    connection, job.Schema, job.Table, destination, options,
                    job.Progress, job.EstimatedTotalRows, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                semaphore.Release();
            }
        }).ToList();

        return await Task.WhenAll(tasks).ConfigureAwait(false);
    }
}
