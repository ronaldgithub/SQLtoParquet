using Microsoft.Data.SqlClient;

namespace SQLtoParquet.App.Services;

public static class SqlConnectionStringFactory
{
    public static string Build(string server, AuthMode authMode, string? username, string? password, string? database = null)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = server,
            TrustServerCertificate = true,
            ConnectTimeout = 10
        };

        if (!string.IsNullOrWhiteSpace(database))
            builder.InitialCatalog = database;

        if (authMode == AuthMode.Windows)
        {
            builder.IntegratedSecurity = true;
        }
        else
        {
            builder.UserID = username ?? string.Empty;
            builder.Password = password ?? string.Empty;
        }

        return builder.ConnectionString;
    }
}
