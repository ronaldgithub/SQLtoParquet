using System.Diagnostics;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SQLtoParquet.App.Views;

public partial class AboutDialog : Window
{
    public AboutDialog()
    {
        InitializeComponent();
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = version is null ? "" : $"Version {version.Major}.{version.Minor}.{version.Build}";
    }

    private void Email_PointerPressed(object? sender, PointerPressedEventArgs e) =>
        Open("mailto:ronald.de.groot@opendata.nl");

    private void DbaronaldCom_PointerPressed(object? sender, PointerPressedEventArgs e) =>
        Open("https://www.dbaronald.com");

    private void DbaronaldNl_PointerPressed(object? sender, PointerPressedEventArgs e) =>
        Open("https://dbaronald.nl");

    private static void Open(string url) =>
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}
