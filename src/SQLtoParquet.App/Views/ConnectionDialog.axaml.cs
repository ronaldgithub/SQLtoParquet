using Avalonia.Controls;
using Avalonia.Interactivity;
using SQLtoParquet.App.Services;
using SQLtoParquet.App.ViewModels;

namespace SQLtoParquet.App.Views;

public partial class ConnectionDialog : Window
{
    public ConnectionDialog()
    {
        InitializeComponent();
    }

    private void DatabaseCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Picking a database is the "done" action for this dialog — close as soon as one is chosen,
        // whether the user picked it by hand or ConnectAsync re-selected a remembered one.
        if (DataContext is ConnectionViewModel { SelectedDatabase: not null })
            Close();
    }

    private void RecentConnection_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ConnectionProfile profile } && DataContext is ConnectionViewModel vm)
        {
            vm.ApplyProfile(profile);
            vm.ConnectCommand.Execute(null);
        }
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close();
}
