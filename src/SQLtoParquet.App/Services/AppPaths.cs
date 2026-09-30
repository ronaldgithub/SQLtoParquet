namespace SQLtoParquet.App.Services;

public static class AppPaths
{
    private static readonly string RootDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SQLtoParquet");

    public static string ConnectionsFile => Path.Combine(RootDir, "connections.json");

    public static string DefaultOutputDir => Path.Combine(RootDir, "exports");
}
