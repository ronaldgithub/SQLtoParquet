using System.Globalization;
using Microsoft.Data.SqlClient;

namespace SQLtoParquet.Core.Export;

/// <summary>Reads one column's value out of a <see cref="SqlDataReader"/> row, converted to the shape its <see cref="ParquetValueKind"/> buffers.</summary>
internal static class SqlValueReader
{
    public static object? Read(SqlDataReader reader, int ordinal, ParquetValueKind kind)
    {
        if (reader.IsDBNull(ordinal))
            return null;

        return kind switch
        {
            ParquetValueKind.Int32 => reader.GetInt32(ordinal),
            ParquetValueKind.Byte => reader.GetByte(ordinal),
            ParquetValueKind.Int16 => reader.GetInt16(ordinal),
            ParquetValueKind.Int64 => reader.GetInt64(ordinal),
            ParquetValueKind.Boolean => reader.GetBoolean(ordinal),
            ParquetValueKind.Double => reader.GetDouble(ordinal),
            ParquetValueKind.Single => reader.GetFloat(ordinal),
            ParquetValueKind.Decimal => reader.GetDecimal(ordinal),
            ParquetValueKind.DateTime => reader.GetDateTime(ordinal),
            // TimeSpan ticks are 100ns each; microseconds-since-midnight = ticks / 10.
            ParquetValueKind.TimeMicros => reader.GetTimeSpan(ordinal).Ticks / 10,
            ParquetValueKind.Guid => reader.GetGuid(ordinal).ToString(),
            ParquetValueKind.DateTimeOffsetString => reader.GetDateTimeOffset(ordinal).ToString("yyyy-MM-dd HH:mm:ss.fffffff zzz", CultureInfo.InvariantCulture),
            ParquetValueKind.StringValue => reader.GetString(ordinal),
            ParquetValueKind.ByteArray => (byte[])reader.GetValue(ordinal),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }
}
