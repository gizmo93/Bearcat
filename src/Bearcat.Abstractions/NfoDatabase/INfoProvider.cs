namespace Bearcat.Abstractions.NfoDatabase;

public interface INfoProvider
{
    Task<ReleaseNfo?> GetReleaseNfoAsync(
        INfoDatabaseConfig config,
        string dirname,
        CancellationToken cancellationToken = default
    );
}
