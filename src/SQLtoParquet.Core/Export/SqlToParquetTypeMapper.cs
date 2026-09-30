using System.Data.Common;
using Parquet.Schema;

namespace SQLtoParquet.Core.Export;

/// <summary>
/// Identifies which CLR representation + <see cref="Microsoft.Data.SqlClient.SqlDataReader"/> accessor
/// a column's values are read/buffered as, ahead of being handed to Parquet.Net's row-group writer.
/// </summary>
public enum ParquetValueKind
{
    Int32,
    Byte,
    Int16,
    Int64,
    Boolean,
    Double,
    Single,
    Decimal,
    DateTime,
    /// <summary>SQL <c>time</c> — stored as microseconds-since-midnight (<see cref="long"/>), matching <see cref="TimeDataField"/>'s Micros unit.</summary>
    TimeMicros,
    /// <summary>SQL <c>uniqueidentifier</c> — read via GetGuid and stored as its string form.</summary>
    Guid,
    /// <summary>SQL <c>datetimeoffset</c> — Parquet.Net has no offset-aware temporal type, so it's stored as an ISO-ish formatted string (matches the original PowerShell exporter's approach).</summary>
    DateTimeOffsetString,
    StringValue,
    ByteArray
}

public sealed record ColumnMapping(string ColumnName, DataField Field, ParquetValueKind Kind);

/// <summary>
/// Maps SQL Server column types (as reported by <c>SqlDataReader.GetColumnSchema()</c>) to Parquet.Net
/// schema fields, ported from the reference PowerShell exporter's type-mapping table
/// (see CLAUDE.md for the full table) and adapted to Parquet.Net's current (v6) schema API.
/// All fields are declared nullable in the Parquet schema regardless of the source column's
/// nullability — simpler and safer than tracking it, since a nullable schema still holds non-null
/// data just fine.
/// </summary>
public static class SqlToParquetTypeMapper
{
    public static IReadOnlyList<ColumnMapping> BuildMappings(IReadOnlyList<DbColumn> columns)
    {
        var mappings = new List<ColumnMapping>(columns.Count);
        foreach (DbColumn column in columns)
        {
            string name = column.ColumnName ?? throw new InvalidOperationException("Column with no name in schema.");
            string sqlType = (column.DataTypeName ?? string.Empty).ToLowerInvariant();

            mappings.Add(sqlType switch
            {
                "int" => new ColumnMapping(name, new DataField(name, typeof(int), isNullable: true), ParquetValueKind.Int32),
                "tinyint" => new ColumnMapping(name, new DataField(name, typeof(byte), isNullable: true), ParquetValueKind.Byte),
                "smallint" => new ColumnMapping(name, new DataField(name, typeof(short), isNullable: true), ParquetValueKind.Int16),
                "bigint" => new ColumnMapping(name, new DataField(name, typeof(long), isNullable: true), ParquetValueKind.Int64),
                "bit" => new ColumnMapping(name, new DataField(name, typeof(bool), isNullable: true), ParquetValueKind.Boolean),

                "char" or "nchar" or "varchar" or "nvarchar" or "text" or "ntext" or "xml"
                    => new ColumnMapping(name, new DataField(name, typeof(string), isNullable: true), ParquetValueKind.StringValue),

                "uniqueidentifier" => new ColumnMapping(name, new DataField(name, typeof(string), isNullable: true), ParquetValueKind.Guid),

                "date" => new ColumnMapping(name, new DateTimeDataField(name, DateTimeFormat.Date, isAdjustedToUTC: false, isNullable: true), ParquetValueKind.DateTime),
                "time" => new ColumnMapping(name, new TimeDataField(name, TimeUnitPrecision.Micros, isNullable: true), ParquetValueKind.TimeMicros),
                "datetime" or "datetime2" or "smalldatetime"
                    => new ColumnMapping(name, new DateTimeDataField(name, DateTimeFormat.DateAndTime, isAdjustedToUTC: false, isNullable: true), ParquetValueKind.DateTime),
                "datetimeoffset" => new ColumnMapping(name, new DataField(name, typeof(string), isNullable: true), ParquetValueKind.DateTimeOffsetString),

                "money" => new ColumnMapping(name, new DecimalDataField(name, precision: 19, scale: 4, isNullable: true), ParquetValueKind.Decimal),
                "smallmoney" => new ColumnMapping(name, new DecimalDataField(name, precision: 10, scale: 4, isNullable: true), ParquetValueKind.Decimal),
                "decimal" or "numeric"
                    => new ColumnMapping(name, new DecimalDataField(name, precision: column.NumericPrecision ?? 38, scale: column.NumericScale ?? 0, isNullable: true), ParquetValueKind.Decimal),

                "float" => new ColumnMapping(name, new DataField(name, typeof(double), isNullable: true), ParquetValueKind.Double),
                "real" => new ColumnMapping(name, new DataField(name, typeof(float), isNullable: true), ParquetValueKind.Single),

                "binary" or "varbinary" or "image"
                    => new ColumnMapping(name, new DataField(name, typeof(byte[]), isNullable: true), ParquetValueKind.ByteArray),

                _ => throw new SqlToParquetUnsupportedTypeException(name, sqlType)
            });
        }

        return mappings;
    }
}
