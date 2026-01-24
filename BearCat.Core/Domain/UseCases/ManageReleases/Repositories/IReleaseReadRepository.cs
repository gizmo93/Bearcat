using BearCat.Core.Domain.UseCases.ManageReleases.Dto;
using BearCat.Core.Domain.UseCases.ManageUploadConfigs.Dto;

namespace BearCat.Core.Domain.UseCases.ManageReleases.Repositories;

public interface IReleaseReadRepository
{
    Task<IReadOnlyList<ReleaseListDto>> GetReleasesAsync(CancellationToken cancellationToken = default);
    Task<ReleaseDto?> GetReleaseAsync(int releaseId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ArchiveConfigDto>> GetArchiveConfigsAsync(int releaseId,
        CancellationToken cancellationToken);
}
