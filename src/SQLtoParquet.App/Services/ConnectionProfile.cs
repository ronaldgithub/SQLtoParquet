namespace SQLtoParquet.App.Services;

public enum AuthMode
{
    Windows,
    Sql
}

/// <summary>
/// A remembered connection target. Deliberately never carries a password — only Server/Database/
/// AuthMode/Username are persisted to disk; the user re-enters credentials for SQL auth each time.
/// </summary>
public sealed record ConnectionProfile(string Server, string Database, AuthMode AuthMode, string? Username, DateTime LastUsedUtc)
{
    public string DisplayName => $"{Server} / {Database}";
}
