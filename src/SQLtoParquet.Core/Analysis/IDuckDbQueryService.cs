using System.Data;

namespace SQLtoParquet.Core.Analysis;

public interface IDuckDbQueryService
{
    Task<DataTable> RunQueryAsync(string sql, CancellationToken cancellationToken = default);
}
