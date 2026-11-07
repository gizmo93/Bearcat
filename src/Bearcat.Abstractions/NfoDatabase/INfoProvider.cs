namespace Bearcat.Abstractions.NfoDatabase;

public interface INfoProvider
{
    int NfoResolutionPriority { get; }

    Task<ReleaseNfo?> GetReleaseNfoAsync(
        INfoDatabaseConfig config,
        string dirname,
        CancellationToken cancellationToken = default
    );
}
