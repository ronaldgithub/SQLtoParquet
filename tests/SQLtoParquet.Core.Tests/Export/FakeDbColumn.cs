using System.Data.Common;

namespace SQLtoParquet.Core.Tests.Export;

/// <summary>DbColumn's schema properties only have protected setters — this exposes a public constructor for tests.</summary>
internal sealed class FakeDbColumn : DbColumn
{
    public FakeDbColumn(string columnName, string dataTypeName, int? numericPrecision = null, int? numericScale = null)
    {
        ColumnName = columnName;
        DataTypeName = dataTypeName;
        NumericPrecision = numericPrecision;
        NumericScale = numericScale;
    }
}
