using BearCat.Core.Domain.UseCases.ManageReleases.Dto;

namespace BearCat.Core.Domain.UseCases.ManageReleases.Repositories;

public interface IReleaseReadRepository
{
    Task<IReadOnlyList<ReleaseListDto>> GetReleasesAsync(CancellationToken cancellationToken = default);
}
