using System.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SQLtoParquet.Core.Analysis;

namespace SQLtoParquet.App.ViewModels;

/// <summary>
/// The "Analyze (DuckDB)" tab: runs ad-hoc SQL against the Parquet files in an output folder via
/// an embedded DuckDB instance. Independent of the SQL Server connection — only needs a folder path.
/// </summary>
public partial class AnalysisViewModel : ViewModelBase
{
    private readonly IDuckDbQueryService queryService;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunQueryCommand))]
    private string sqlText = string.Empty;

    [ObservableProperty]
    private DataTable? results;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunQueryCommand))]
    private bool isRunning;

    [ObservableProperty]
    private string statusMessage = "Export some tables, then run a SQL query against the resulting Parquet files.";

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
        if (!string.IsNullOrWhiteSpace(SqlText))
            return;

        if (ParquetFileCatalog.ListParquetFiles(folder).Count == 0)
            return;

        string glob = Path.Combine(folder, "*.parquet").Replace('\\', '/');
        SqlText = $"SELECT * FROM read_parquet('{glob}') LIMIT 100;";
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
            StatusMessage = $"{table.Rows.Count} row(s) returned.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Query failed: {ex.Message}";
        }
        finally
        {
            IsRunning = false;
        }
    }
}
