using System.Data;
using DuckDB.NET.Data;

namespace SQLtoParquet.Core.Analysis;

/// <summary>
/// Runs ad-hoc SQL against local Parquet files via an in-memory DuckDB instance — DuckDB can query
/// Parquet files in place (<c>read_parquet('...')</c>), no import step needed. Read-only: this app
/// never writes back to the Parquet files or SQL Server from here.
/// </summary>
public sealed class DuckDbQueryService : IDuckDbQueryService
{
    public Task<DataTable> RunQueryAsync(string sql, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            using var connection = new DuckDBConnection("DataSource=:memory:");
            connection.Open();

            using IDbCommand command = connection.CreateCommand();
            command.CommandText = sql;

            using IDataReader reader = command.ExecuteReader();
            var table = new DataTable();
            table.Load(reader);
            return table;
        }, cancellationToken);
}
