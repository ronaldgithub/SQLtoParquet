using System.Globalization;
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
    // Dutch number formatting throughout the grid: period as thousands separator (e.g. "1.102.878").
    private static readonly CultureInfo DisplayCulture = CultureInfo.GetCultureInfo("nl-NL");

    public TableSizeInfo Info { get; } = info;

    public string Schema => Info.Schema;
    public string Table => Info.Table;
    public string QualifiedName => Info.QualifiedName;
    public long RowCount => Info.RowCount;
    public double DataSizeMb => Info.DataSpaceKb / 1024.0;
    public double IndexSizeMb => Info.IndexSpaceKb / 1024.0;
    public double TotalSizeMb => Info.TotalSpaceKb / 1024.0;

    public string RowCountDisplay => RowCount.ToString("N0", DisplayCulture);
    public string TotalSizeMbDisplay => TotalSizeMb.ToString("N1", DisplayCulture);

    [ObservableProperty]
    private bool isSelected = true;

    [ObservableProperty]
    private ExportRowStatus status = ExportRowStatus.Pending;

    [ObservableProperty]
    private long rowsProcessed;

    [ObservableProperty]
    private double progressPercent;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ParquetFileSizeMbDisplay))]
    [NotifyPropertyChangedFor(nameof(RatioDisplay))]
    private double? parquetFileSizeMb;

    public string ParquetFileSizeMbDisplay => ParquetFileSizeMb?.ToString("N2", DisplayCulture) ?? string.Empty;

    /// <summary>How many times smaller the Parquet file is than the original SQL size, e.g. "3,3x".</summary>
    public string RatioDisplay =>
        ParquetFileSizeMb is > 0 && TotalSizeMb > 0
            ? (TotalSizeMb / ParquetFileSizeMb.Value).ToString("N1", DisplayCulture) + "x"
            : string.Empty;

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
