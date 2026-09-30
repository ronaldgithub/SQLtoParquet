namespace SQLtoParquet.App.Services;

public interface IConnectionProfileStore
{
    Task<IReadOnlyList<ConnectionProfile>> LoadRecentAsync(CancellationToken cancellationToken = default);

    Task RememberAsync(ConnectionProfile profile, CancellationToken cancellationToken = default);
}
