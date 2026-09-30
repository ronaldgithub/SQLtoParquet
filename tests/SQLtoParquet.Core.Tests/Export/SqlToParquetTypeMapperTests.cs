using Parquet.Schema;
using SQLtoParquet.Core.Export;
using Xunit;

namespace SQLtoParquet.Core.Tests.Export;

public class SqlToParquetTypeMapperTests
{
    [Theory]
    [InlineData("int", typeof(int), ParquetValueKind.Int32)]
    [InlineData("tinyint", typeof(byte), ParquetValueKind.Byte)]
    [InlineData("smallint", typeof(short), ParquetValueKind.Int16)]
    [InlineData("bigint", typeof(long), ParquetValueKind.Int64)]
    [InlineData("bit", typeof(bool), ParquetValueKind.Boolean)]
    [InlineData("float", typeof(double), ParquetValueKind.Double)]
    [InlineData("real", typeof(float), ParquetValueKind.Single)]
    public void SimpleScalarTypes_MapToExpectedDataField(string sqlType, Type expectedClrType, ParquetValueKind expectedKind)
    {
        var mappings = SqlToParquetTypeMapper.BuildMappings([new FakeDbColumn("col", sqlType)]);

        var mapping = Assert.Single(mappings);
        Assert.Equal(expectedKind, mapping.Kind);
        Assert.IsType<DataField>(mapping.Field);
        Assert.Equal(expectedClrType, mapping.Field.ClrType);
        Assert.True(mapping.Field.IsNullable);
    }

    [Theory]
    [InlineData("nvarchar", ParquetValueKind.StringValue)]
    [InlineData("varchar", ParquetValueKind.StringValue)]
    [InlineData("uniqueidentifier", ParquetValueKind.Guid)]
    [InlineData("datetimeoffset", ParquetValueKind.DateTimeOffsetString)]
    public void StringBackedTypes_MapToStringDataField(string sqlType, ParquetValueKind expectedKind)
    {
        // Parquet.Net normalizes a string-typed DataField's ClrType to ReadOnlyMemory<char> internally
        // (see ParquetWritePipelineTests for confirmation the write/read path still round-trips strings fine).
        var mappings = SqlToParquetTypeMapper.BuildMappings([new FakeDbColumn("col", sqlType)]);

        var mapping = Assert.Single(mappings);
        Assert.Equal(expectedKind, mapping.Kind);
        Assert.Equal(typeof(ReadOnlyMemory<char>), mapping.Field.ClrType);
    }

    [Theory]
    [InlineData("datetime")]
    [InlineData("datetime2")]
    [InlineData("smalldatetime")]
    public void DateTimeVariants_MapToDateTimeDataField(string sqlType)
    {
        var mappings = SqlToParquetTypeMapper.BuildMappings([new FakeDbColumn("ts", sqlType)]);

        var mapping = Assert.Single(mappings);
        Assert.Equal(ParquetValueKind.DateTime, mapping.Kind);
        Assert.IsType<DateTimeDataField>(mapping.Field);
    }

    [Fact]
    public void Time_MapsToTimeDataField_WithMicrosReadKind()
    {
        var mappings = SqlToParquetTypeMapper.BuildMappings([new FakeDbColumn("t", "time")]);

        var mapping = Assert.Single(mappings);
        Assert.Equal(ParquetValueKind.TimeMicros, mapping.Kind);
        Assert.IsType<TimeDataField>(mapping.Field);
        Assert.Equal(typeof(long), mapping.Field.ClrType);
    }

    [Fact]
    public void Money_MapsToDecimalDataField_With19Precision4Scale()
    {
        var mappings = SqlToParquetTypeMapper.BuildMappings([new FakeDbColumn("amount", "money")]);

        var field = Assert.IsType<DecimalDataField>(Assert.Single(mappings).Field);
        Assert.Equal(19, field.Precision);
        Assert.Equal(4, field.Scale);
    }

    [Fact]
    public void SmallMoney_MapsToDecimalDataField_With10Precision4Scale()
    {
        var mappings = SqlToParquetTypeMapper.BuildMappings([new FakeDbColumn("amount", "smallmoney")]);

        var field = Assert.IsType<DecimalDataField>(Assert.Single(mappings).Field);
        Assert.Equal(10, field.Precision);
        Assert.Equal(4, field.Scale);
    }

    [Fact]
    public void Decimal_UsesActualPrecisionAndScaleFromSchema()
    {
        var mappings = SqlToParquetTypeMapper.BuildMappings([new FakeDbColumn("price", "decimal", numericPrecision: 12, numericScale: 3)]);

        var field = Assert.IsType<DecimalDataField>(Assert.Single(mappings).Field);
        Assert.Equal(12, field.Precision);
        Assert.Equal(3, field.Scale);
    }

    [Fact]
    public void Binary_MapsToByteArrayField()
    {
        var mappings = SqlToParquetTypeMapper.BuildMappings([new FakeDbColumn("blob", "varbinary")]);

        var mapping = Assert.Single(mappings);
        Assert.Equal(ParquetValueKind.ByteArray, mapping.Kind);
        Assert.Equal(typeof(ReadOnlyMemory<byte>), mapping.Field.ClrType);
    }

    [Fact]
    public void UnsupportedType_ThrowsWithColumnAndTypeName()
    {
        var ex = Assert.Throws<SqlToParquetUnsupportedTypeException>(() =>
            SqlToParquetTypeMapper.BuildMappings([new FakeDbColumn("shape", "geography")]));

        Assert.Equal("shape", ex.ColumnName);
        Assert.Equal("geography", ex.SqlTypeName);
    }

    [Fact]
    public void MultipleColumns_PreserveOrderAndAllMap()
    {
        var mappings = SqlToParquetTypeMapper.BuildMappings([
            new FakeDbColumn("Id", "int"),
            new FakeDbColumn("Name", "nvarchar"),
            new FakeDbColumn("CreatedAt", "datetime2")
        ]);

        Assert.Equal(3, mappings.Count);
        Assert.Equal(["Id", "Name", "CreatedAt"], mappings.Select(m => m.ColumnName));
    }
}
