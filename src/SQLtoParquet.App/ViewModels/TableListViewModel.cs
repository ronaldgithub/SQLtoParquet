using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Data.SqlClient;
using SQLtoParquet.App.Services;
using SQLtoParquet.Core.Export;
using SQLtoParquet.Core.Export.Destinations;
using SQLtoParquet.Core.Logging;
using SQLtoParquet.Core.SqlServer;

namespace SQLtoParquet.App.ViewModels;

public partial class TableListViewModel : ViewModelBase
{
    private readonly ITableCatalogService catalogService;
    private readonly ExportOrchestrator orchestrator;
    private string? connectionString;
    private CancellationTokenSource? exportCts;

    public ObservableCollection<TableExportRowViewModel> Tables { get; } = [];

    [ObservableProperty]
    private bool isAllSelected = true;

    [ObservableProperty]
    private string outputFolder;

    [ObservableProperty]
    private int maxParallelism = 2;

    [ObservableProperty]
    private int rowsPerRowGroup = 100_000;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartExportCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelExportCommand))]
    private bool isExporting;

    [ObservableProperty]
    private double overallProgressPercent;

    [ObservableProperty]
    private string statusMessage = "Connect and pick a database to see tables.";

    [ObservableProperty]
    private string? lastLogFilePath;

    public bool HasTables => Tables.Count > 0;

    public TableListViewModel() : this(new SqlServerTableCatalogService(), new ExportOrchestrator(new TableExporter()))
    {
    }

    public TableListViewModel(ITableCatalogService catalogService, ExportOrchestrator orchestrator)
    {
        this.catalogService = catalogService;
        this.orchestrator = orchestrator;
        outputFolder = AppPaths.DefaultOutputDir;
    }

    public async Task LoadTablesAsync(string newConnectionString)
    {
        connectionString = newConnectionString;
        Tables.Clear();
        OverallProgressPercent = 0;
        StatusMessage = "Loading tables...";

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync().ConfigureAwait(true);
            var infos = await catalogService.ListTablesAsync(connection).ConfigureAwait(true);

            foreach (var info in infos)
            {
                var row = new TableExportRowViewModel(info) { IsSelected = true };
                row.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(TableExportRowViewModel.IsSelected))
                        StartExportCommand.NotifyCanExecuteChanged();
                };
                Tables.Add(row);
            }

            OnPropertyChanged(nameof(HasTables));
            StatusMessage = $"{Tables.Count} table(s) found. Select tables and click Start Export.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load tables: {ex.Message}";
        }

        StartExportCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsAllSelectedChanged(bool value)
    {
        foreach (var t in Tables)
            t.IsSelected = value;
    }

    private bool CanStartExport => !IsExporting && connectionString != null && Tables.Any(t => t.IsSelected);

    [RelayCommand(CanExecute = nameof(CanStartExport))]
    private async Task StartExportAsync()
    {
        if (connectionString is null)
            return;

        var selected = Tables.Where(t => t.IsSelected).ToList();
        if (selected.Count == 0)
            return;

        IsExporting = true;
        OverallProgressPercent = 0;
        exportCts = new CancellationTokenSource();
        StatusMessage = $"Exporting {selected.Count} table(s)...";

        foreach (var row in selected)
        {
            row.Status = ExportRowStatus.Running;
            row.RowsProcessed = 0;
            row.ProgressPercent = 0;
            row.ErrorMessage = null;
            row.ParquetFileSizeMb = null;
        }

        var options = new TableExportOptions
        {
            MaxParallelism = Math.Max(1, MaxParallelism),
            RowsPerRowGroup = Math.Max(1, RowsPerRowGroup)
        };
        var destination = new LocalFileExportDestination(OutputFolder);

        LastLogFilePath = AppPaths.NewExportLogPath();
        await using var log = new ExportRunLogWriter(LastLogFilePath);
        log.WriteLine($"Export started: {selected.Count} table(s), parallel={options.MaxParallelism}, rowsPerGroup={options.RowsPerRowGroup}, output={OutputFolder}");
        foreach (var row in selected)
            log.WriteLine($"  queued: {row.QualifiedName} (~{row.RowCount:N0} rows, {row.TotalSizeMb:N1} MB)");

        var jobs = selected.Select(row => new TableExportJob(
            row.Schema,
            row.Table,
            row.RowCount,
            new Progress<TableExportProgress>(p =>
            {
                row.ReportProgress(p);
                RecomputeOverallProgress();
                log.WriteLine($"{row.QualifiedName}: {p.RowsProcessed:N0} row(s) processed");
            }))).ToList();

        try
        {
            var results = await orchestrator.RunAsync(connectionString, jobs, destination, options, exportCts.Token).ConfigureAwait(true);

            foreach (var result in results)
            {
                var row = selected.First(t => t.QualifiedName == result.TableName);
                if (result.Success)
                {
                    row.Status = ExportRowStatus.Succeeded;
                    row.RowsProcessed = result.RowsExported;
                    row.ProgressPercent = 100;
                    row.ParquetFileSizeMb = result.OutputFileSizeBytes / 1024.0 / 1024.0;
                    log.WriteLine($"{row.QualifiedName}: succeeded — {result.RowsExported:N0} rows, {row.ParquetFileSizeMb:N2} MB, {result.Duration:mm\\:ss\\.fff} -> {result.OutputFilePath}");
                }
                else
                {
                    row.Status = ExportRowStatus.Failed;
                    row.ErrorMessage = result.Error;
                    log.WriteLine($"{row.QualifiedName}: FAILED — {result.Error}");
                }
            }

            int succeeded = results.Count(r => r.Success);
            StatusMessage = $"Export complete: {succeeded}/{results.Count} table(s) succeeded. Log: {LastLogFilePath}";
            log.WriteLine($"Export complete: {succeeded}/{results.Count} table(s) succeeded.");
        }
        catch (OperationCanceledException)
        {
            foreach (var row in selected.Where(t => t.Status == ExportRowStatus.Running))
                row.Status = ExportRowStatus.Canceled;
            StatusMessage = "Export canceled.";
            log.WriteLine("Export canceled by user.");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export failed: {ex.Message}";
            log.WriteLine($"Export failed: {ex.Message}");
        }
        finally
        {
            IsExporting = false;
            exportCts?.Dispose();
            exportCts = null;
        }
    }

    private bool CanCancelExport => IsExporting;

    [RelayCommand(CanExecute = nameof(CanCancelExport))]
    private void CancelExport() => exportCts?.Cancel();

    private void RecomputeOverallProgress()
    {
        long processed = Tables.Sum(t => t.RowsProcessed);
        long total = Tables.Where(t => t.Status is ExportRowStatus.Running or ExportRowStatus.Succeeded).Sum(t => t.RowCount);
        OverallProgressPercent = total > 0 ? Math.Min(100.0, 100.0 * processed / total) : 0;
    }
}
