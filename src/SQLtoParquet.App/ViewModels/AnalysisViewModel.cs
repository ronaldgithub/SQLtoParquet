using System.Data;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SQLtoParquet.App.Services;
using SQLtoParquet.Core.Analysis;

namespace SQLtoParquet.App.ViewModels;

public sealed record ExampleQuery(string Title, string Sql);

/// <summary>
/// The "Analyze (DuckDB)" tab: runs ad-hoc SQL against the Parquet files in an output folder via
/// an embedded DuckDB instance. Independent of the SQL Server connection — only needs a folder path.
/// </summary>
public partial class AnalysisViewModel : ViewModelBase
{
    private const int MaxDisplayRows = 500;

    private readonly IDuckDbQueryService queryService;
    private string? lastFolder;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunQueryCommand))]
    private string sqlText = string.Empty;

    [ObservableProperty]
    private DataTable? results;

    [ObservableProperty]
    private string resultsText = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunQueryCommand))]
    private bool isRunning;

    [ObservableProperty]
    private string statusMessage = "Export some tables, then run a SQL query against the resulting Parquet files.";

    public IReadOnlyList<ExampleQuery> Examples { get; } =
    [
        new("1. Row count & date range", """
            SELECT COUNT(*) AS badges, MIN(Date) AS first_badge, MAX(Date) AS last_badge
            FROM read_parquet('{folder}/dbo_Badges.snappy.parquet');
            """),
        new("2. Most common badge names (top 20)", """
            SELECT Name, COUNT(*) AS awarded
            FROM read_parquet('{folder}/dbo_Badges.snappy.parquet')
            GROUP BY Name
            ORDER BY awarded DESC
            LIMIT 20;
            """),
        new("3. Badge class breakdown (1=Gold, 2=Silver, 3=Bronze)", """
            SELECT Class,
                   COUNT(*) AS count,
                   ROUND(100.0 * COUNT(*) / SUM(COUNT(*)) OVER (), 2) AS pct
            FROM read_parquet('{folder}/dbo_Badges.snappy.parquet')
            GROUP BY Class
            ORDER BY Class;
            """),
        new("4. Badges awarded per year", """
            SELECT date_trunc('year', Date) AS year, COUNT(*) AS badges
            FROM read_parquet('{folder}/dbo_Badges.snappy.parquet')
            GROUP BY 1
            ORDER BY 1;
            """),
        new("5. Top 20 users by badge count", """
            SELECT UserId, COUNT(*) AS badge_count
            FROM read_parquet('{folder}/dbo_Badges.snappy.parquet')
            GROUP BY UserId
            ORDER BY badge_count DESC
            LIMIT 20;
            """),
        new("6. Each user's first badge", """
            SELECT UserId, Name, Date
            FROM (
                SELECT *, ROW_NUMBER() OVER (PARTITION BY UserId ORDER BY Date) AS rn
                FROM read_parquet('{folder}/dbo_Badges.snappy.parquet')
            ) WHERE rn = 1
            LIMIT 20;
            """),
        new("7. Tag-based vs. general badges", """
            SELECT TagBased, COUNT(*) AS count
            FROM read_parquet('{folder}/dbo_Badges.snappy.parquet')
            GROUP BY TagBased;
            """),
        new("8. Day-of-week seasonality", """
            SELECT strftime(Date, '%A') AS weekday, COUNT(*) AS badges
            FROM read_parquet('{folder}/dbo_Badges.snappy.parquet')
            GROUP BY 1
            ORDER BY badges DESC;
            """),
    ];

    public AnalysisViewModel() : this(new DuckDbQueryService())
    {
    }

    public AnalysisViewModel(IDuckDbQueryService queryService)
    {
        this.queryService = queryService;
    }

    /// <summary>Seeds the query box with a ready-to-run glob query once an export folder is known, but only if the user hasn't typed anything yet.</summary>
    public void SeedFromFolder(string folder)
    {
        lastFolder = folder;

        if (!string.IsNullOrWhiteSpace(SqlText))
            return;

        if (ParquetFileCatalog.ListParquetFiles(folder).Count == 0)
            return;

        string glob = Path.Combine(folder, "*.parquet").Replace('\\', '/');
        SqlText = $"SELECT * FROM read_parquet('{glob}') LIMIT 100;";
    }

    /// <summary>Loads an example query into the text box, substituting the most recently known export folder.</summary>
    public void UseExample(ExampleQuery example)
    {
        string folder = (lastFolder ?? AppPaths.DefaultOutputDir).Replace('\\', '/');
        SqlText = example.Sql.Replace("{folder}", folder);
    }

    private bool CanRunQuery => !IsRunning && !string.IsNullOrWhiteSpace(SqlText);

    [RelayCommand(CanExecute = nameof(CanRunQuery))]
    private async Task RunQueryAsync()
    {
        IsRunning = true;
        StatusMessage = "Running query...";

        try
        {
            var table = await queryService.RunQueryAsync(SqlText).ConfigureAwait(true);
            Results = table;
            ResultsText = FormatAsTable(table);
            StatusMessage = $"{table.Rows.Count} row(s) returned.";
        }
        catch (Exception ex)
        {
            ResultsText = string.Empty;
            StatusMessage = $"Query failed: {ex.Message}";
        }
        finally
        {
            IsRunning = false;
        }
    }

    private static string FormatAsTable(DataTable table)
    {
        if (table.Columns.Count == 0)
            return "(no columns returned)";

        int rowCount = Math.Min(table.Rows.Count, MaxDisplayRows);
        var cells = new string[rowCount + 1, table.Columns.Count];

        for (int c = 0; c < table.Columns.Count; c++)
            cells[0, c] = table.Columns[c].ColumnName;

        for (int r = 0; r < rowCount; r++)
            for (int c = 0; c < table.Columns.Count; c++)
                cells[r + 1, c] = table.Rows[r][c]?.ToString() ?? "NULL";

        var widths = new int[table.Columns.Count];
        for (int c = 0; c < table.Columns.Count; c++)
        {
            int max = 0;
            for (int r = 0; r <= rowCount; r++)
                max = Math.Max(max, cells[r, c].Length);
            widths[c] = max;
        }

        var sb = new StringBuilder();
        AppendRow(sb, cells, 0, widths);
        sb.AppendLine(string.Join("-+-", widths.Select(w => new string('-', w))));
        for (int r = 1; r <= rowCount; r++)
            AppendRow(sb, cells, r, widths);

        if (table.Rows.Count > MaxDisplayRows)
            sb.AppendLine($"... showing first {MaxDisplayRows:N0} of {table.Rows.Count:N0} rows");

        return sb.ToString();
    }

    private static void AppendRow(StringBuilder sb, string[,] cells, int row, int[] widths)
    {
        for (int c = 0; c < widths.Length; c++)
        {
            if (c > 0)
                sb.Append(" | ");
            sb.Append(cells[row, c].PadRight(widths[c]));
        }
        sb.AppendLine();
    }
}
