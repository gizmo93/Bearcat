using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Dto;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.ReadModels;

namespace Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Repositories;

public interface ILinkCrypterImportReadRepository
{
    Task<IReadOnlyList<LinkCrypterImportReadModel>> GetAllAsync(
        CancellationToken cancellationToken = default
    );

    Task<LinkCrypterImportDetailReadModel?> GetDetailAsync(
        int linkCrypterImportId,
        CancellationToken cancellationToken = default
    );

    Task<PagedResult<LinkCrypterImportReleaseReadModel>> SearchReleasesAsync(
        LinkCrypterImportReleaseSearchQuery query,
        CancellationToken cancellationToken = default
    );

    Task<
        IReadOnlyList<LinkCrypterImportSelectableContainerReadModel>
    > GetSelectableContainersOfFilteredReleasesAsync(
        LinkCrypterImportReleaseFilter filter,
        CancellationToken cancellationToken = default
    );
}
