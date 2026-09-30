using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SQLtoParquet.App.Services;
using SQLtoParquet.App.ViewModels;

namespace SQLtoParquet.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void ConnectButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        var dialog = new ConnectionDialog { DataContext = vm.Connection };
        await dialog.ShowDialog(this);
    }

    private async void AboutButton_Click(object? sender, RoutedEventArgs e)
    {
        var dialog = new AboutDialog();
        await dialog.ShowDialog(this);
    }

    private async void BrowseOutputFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose an export output folder",
            AllowMultiple = false
        });

        if (folders.Count > 0)
            vm.TableList.OutputFolder = folders[0].Path.LocalPath;
    }

    private void OpenLogFolder_Click(object? sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(AppPaths.LogsDir);
        Process.Start(new ProcessStartInfo(AppPaths.LogsDir) { UseShellExecute = true });
    }
}
