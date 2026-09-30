namespace SQLtoParquet.Core.Export.Destinations;

public sealed record ExportedFileHandle(string Path);

/// <summary>
/// Where an exported table's Parquet bytes end up. <see cref="LocalFileExportDestination"/> is the only
/// implementation today; the stream-in/size-out shape is deliberately thin so a future
/// Azure Fabric OneLake/ADLS Gen2 destination can be added without changing <see cref="TableExporter"/>.
/// </summary>
public interface IExportDestination
{
    Task<ExportedFileHandle> WriteAsync(string logicalTableName, Func<Stream, CancellationToken, Task> writeAction, CancellationToken cancellationToken);

    Task<long> GetResultingSizeAsync(ExportedFileHandle handle, CancellationToken cancellationToken);
}
