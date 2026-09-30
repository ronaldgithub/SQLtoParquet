using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Data.SqlClient;
using SQLtoParquet.App.Services;

namespace SQLtoParquet.App.ViewModels;

public partial class ConnectionViewModel : ViewModelBase
{
    private readonly IConnectionProfileStore profileStore;

    public ObservableCollection<ConnectionProfile> RecentConnections { get; } = [];
    public ObservableCollection<string> AvailableDatabases { get; } = [];
    public IReadOnlyList<AuthMode> AuthModes { get; } = [AuthMode.Windows, AuthMode.Sql];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectionSummary))]
    private string server = "localhost";

    [ObservableProperty]
    private AuthMode selectedAuthMode = AuthMode.Windows;

    [ObservableProperty]
    private string username = string.Empty;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectionSummary))]
    private string? selectedDatabase;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = "Not connected.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectionSummary))]
    private bool isConnectedToServer;

    public bool IsSqlAuth => SelectedAuthMode == AuthMode.Sql;
    public bool HasRecentConnections => RecentConnections.Count > 0;
    public string ConnectionSummary => IsConnectedToServer && SelectedDatabase != null ? $"{Server} / {SelectedDatabase}" : "Not connected.";

    public ConnectionViewModel() : this(new ConnectionProfileStore())
    {
    }

    public ConnectionViewModel(IConnectionProfileStore profileStore)
    {
        this.profileStore = profileStore;
    }

    public async Task LoadRecentConnectionsAsync()
    {
        var recent = await profileStore.LoadRecentAsync().ConfigureAwait(true);
        RecentConnections.Clear();
        foreach (var p in recent)
            RecentConnections.Add(p);
        OnPropertyChanged(nameof(HasRecentConnections));
    }

    public void ApplyProfile(ConnectionProfile profile)
    {
        Server = profile.Server;
        SelectedAuthMode = profile.AuthMode;
        Username = profile.Username ?? string.Empty;
        SelectedDatabase = profile.Database;
    }

    partial void OnSelectedAuthModeChanged(AuthMode value) => OnPropertyChanged(nameof(IsSqlAuth));

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (string.IsNullOrWhiteSpace(Server))
        {
            StatusMessage = "Enter a server name first.";
            return;
        }

        IsBusy = true;
        IsConnectedToServer = false;
        AvailableDatabases.Clear();
        StatusMessage = "Connecting...";

        try
        {
            var connectionString = SqlConnectionStringFactory.Build(Server, SelectedAuthMode, Username, Password);
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync().ConfigureAwait(true);

            const string databaseQuery = "SELECT name FROM sys.databases WHERE state = 0 AND name NOT IN ('master','model','msdb','tempdb') ORDER BY name";
            await using var cmd = new SqlCommand(databaseQuery, connection);
            await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(true);
            var names = new List<string>();
            while (await reader.ReadAsync().ConfigureAwait(true))
                names.Add(reader.GetString(0));

            foreach (var name in names)
                AvailableDatabases.Add(name);

            IsConnectedToServer = true;
            StatusMessage = $"Connected to {Server}. {names.Count} database(s) found.";

            if (SelectedDatabase != null && AvailableDatabases.Contains(SelectedDatabase))
            {
                // Re-trigger selection (e.g. after applying a recent profile) so downstream table loading fires.
                var db = SelectedDatabase;
                SelectedDatabase = null;
                SelectedDatabase = db;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Connection failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public string BuildConnectionString(string? database = null) =>
        SqlConnectionStringFactory.Build(Server, SelectedAuthMode, Username, Password, database ?? SelectedDatabase);

    public Task RememberCurrentAsync()
    {
        if (SelectedDatabase is null)
            return Task.CompletedTask;

        return profileStore.RememberAsync(new ConnectionProfile(Server, SelectedDatabase, SelectedAuthMode, IsSqlAuth ? Username : null, DateTime.UtcNow));
    }
}
