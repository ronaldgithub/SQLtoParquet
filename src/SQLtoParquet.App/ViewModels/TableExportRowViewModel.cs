using CommunityToolkit.Mvvm.ComponentModel;
using SQLtoParquet.Core.Export;
using SQLtoParquet.Core.SqlServer;

namespace SQLtoParquet.App.ViewModels;

public enum ExportRowStatus
{
    Pending,
    Running,
    Succeeded,
    Failed,
    Canceled
}

public partial class TableExportRowViewModel(TableSizeInfo info) : ViewModelBase
{
    public TableSizeInfo Info { get; } = info;

    public string Schema => Info.Schema;
    public string Table => Info.Table;
    public string QualifiedName => Info.QualifiedName;
    public long RowCount => Info.RowCount;
    public double DataSizeMb => Info.DataSpaceKb / 1024.0;
    public double IndexSizeMb => Info.IndexSpaceKb / 1024.0;
    public double TotalSizeMb => Info.TotalSpaceKb / 1024.0;

    [ObservableProperty]
    private bool isSelected = true;

    [ObservableProperty]
    private ExportRowStatus status = ExportRowStatus.Pending;

    [ObservableProperty]
    private long rowsProcessed;

    [ObservableProperty]
    private double progressPercent;

    [ObservableProperty]
    private double? parquetFileSizeMb;

    [ObservableProperty]
    private string? errorMessage;

    public void ReportProgress(TableExportProgress progress)
    {
        RowsProcessed = progress.RowsProcessed;
        ProgressPercent = progress.EstimatedTotalRows is > 0
            ? Math.Min(100.0, 100.0 * progress.RowsProcessed / progress.EstimatedTotalRows.Value)
            : 0;
    }
}
