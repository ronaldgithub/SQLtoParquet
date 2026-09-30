using SQLtoParquet.App.Services;

namespace SQLtoParquet.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public ConnectionViewModel Connection { get; }
    public TableListViewModel TableList { get; }
    public AnalysisViewModel Analysis { get; }

    public MainWindowViewModel() : this(new ConnectionProfileStore())
    {
    }

    public MainWindowViewModel(IConnectionProfileStore profileStore)
    {
        Connection = new ConnectionViewModel(profileStore);
        TableList = new TableListViewModel();
        Analysis = new AnalysisViewModel();

        Connection.PropertyChanged += async (_, e) =>
        {
            if (e.PropertyName != nameof(ConnectionViewModel.SelectedDatabase) || Connection.SelectedDatabase is null)
                return;

            // ApplyProfile (used by the Connect dialog's "Recent" list) sets SelectedDatabase before the
            // server is actually reconnected, which would otherwise fire this handler prematurely; ConnectAsync
            // then fires it again for real once connected. Without this guard both calls race and duplicate rows.
            if (!Connection.IsConnectedToServer)
                return;

            await TableList.LoadTablesAsync(Connection.BuildConnectionString()).ConfigureAwait(true);
            Analysis.SeedFromFolder(TableList.OutputFolder);
            await Connection.RememberCurrentAsync().ConfigureAwait(true);
            await Connection.LoadRecentConnectionsAsync().ConfigureAwait(true);
        };

        _ = Connection.LoadRecentConnectionsAsync();
    }
}
