namespace SQLtoParquet.Core.Export;

public sealed class SqlToParquetUnsupportedTypeException : Exception
{
    public string ColumnName { get; }
    public string SqlTypeName { get; }

    public SqlToParquetUnsupportedTypeException(string columnName, string sqlTypeName)
        : base($"Column '{columnName}' has unsupported SQL type '{sqlTypeName}' — no Parquet mapping is defined for it.")
    {
        ColumnName = columnName;
        SqlTypeName = sqlTypeName;
    }
}
