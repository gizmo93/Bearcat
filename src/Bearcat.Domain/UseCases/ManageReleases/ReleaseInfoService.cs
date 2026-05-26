using Bearcat.Domain.UseCases.ManageReleases.Repositories;

namespace Bearcat.Domain.UseCases.ManageReleases;

public class ReleaseInfoService(IReleaseInfoRepository repository)
{
    public async Task DeleteAsync(int releaseInfoId, CancellationToken cancellationToken = default)
    {
        var releaseInfo = await repository.GetReleaseInfoByIdAsync(
            releaseInfoId,
            cancellationToken
        );
        repository.Remove(releaseInfo);

        await repository.SaveChangesAsync(cancellationToken);
    }
}
