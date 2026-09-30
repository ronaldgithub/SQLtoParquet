namespace SQLtoParquet.Core.Export.Destinations;

public sealed class LocalFileExportDestination(string outputDirectory) : IExportDestination
{
    public async Task<ExportedFileHandle> WriteAsync(string logicalTableName, Func<Stream, CancellationToken, Task> writeAction, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outputDirectory);

        string safeName = string.Join("_", logicalTableName.Split(Path.GetInvalidFileNameChars()));
        string path = Path.Combine(outputDirectory, $"{safeName}.snappy.parquet");

        await using (var stream = File.Create(path))
        {
            await writeAction(stream, cancellationToken).ConfigureAwait(false);
        }

        return new ExportedFileHandle(path);
    }

    public Task<long> GetResultingSizeAsync(ExportedFileHandle handle, CancellationToken cancellationToken) =>
        Task.FromResult(new FileInfo(handle.Path).Length);
}
