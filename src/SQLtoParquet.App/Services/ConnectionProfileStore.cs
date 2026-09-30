using System.Text.Json;

namespace SQLtoParquet.App.Services;

public sealed class ConnectionProfileStore : IConnectionProfileStore
{
    private const int MaxRemembered = 10;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task<IReadOnlyList<ConnectionProfile>> LoadRecentAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(AppPaths.ConnectionsFile))
            return [];

        try
        {
            await using var stream = File.OpenRead(AppPaths.ConnectionsFile);
            var profiles = await JsonSerializer.DeserializeAsync<List<ConnectionProfile>>(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            return profiles?.OrderByDescending(p => p.LastUsedUtc).ToList() ?? [];
        }
        catch (JsonException)
        {
            // Corrupt/foreign file — treat as empty rather than crashing the app on startup.
            return [];
        }
    }

    public async Task RememberAsync(ConnectionProfile profile, CancellationToken cancellationToken = default)
    {
        var existing = (await LoadRecentAsync(cancellationToken).ConfigureAwait(false))
            .Where(p => !(p.Server.Equals(profile.Server, StringComparison.OrdinalIgnoreCase)
                       && p.Database.Equals(profile.Database, StringComparison.OrdinalIgnoreCase)
                       && p.AuthMode == profile.AuthMode
                       && p.Username == profile.Username))
            .ToList();

        var updated = new List<ConnectionProfile> { profile };
        updated.AddRange(existing);

        Directory.CreateDirectory(Path.GetDirectoryName(AppPaths.ConnectionsFile)!);
        await using var stream = File.Create(AppPaths.ConnectionsFile);
        await JsonSerializer.SerializeAsync(stream, updated.Take(MaxRemembered).ToList(), JsonOptions, cancellationToken).ConfigureAwait(false);
    }
}
