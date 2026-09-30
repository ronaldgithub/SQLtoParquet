namespace SQLtoParquet.Core.Logging;

/// <summary>Appends timestamped lines to a plain text log file for one export run.</summary>
public sealed class ExportRunLogWriter : IAsyncDisposable
{
    private readonly StreamWriter writer;

    public string FilePath { get; }

    public ExportRunLogWriter(string filePath)
    {
        FilePath = filePath;
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        writer = new StreamWriter(filePath, append: true) { AutoFlush = true };
    }

    public void WriteLine(string message) =>
        writer.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} | {message}");

    public ValueTask DisposeAsync() => writer.DisposeAsync();
}
