namespace SQLtoParquet.Core.Analysis;

public static class ParquetFileCatalog
{
    public static IReadOnlyList<string> ListParquetFiles(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return [];

        return Directory.EnumerateFiles(folder, "*.parquet", SearchOption.TopDirectoryOnly)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
