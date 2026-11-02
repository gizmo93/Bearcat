using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Repositories;

public interface ILinkCrypterImportRepository
{
    Task<LinkCrypterRegistration?> GetLinkCrypterRegistrationOrDefaultAsync(
        int linkCrypterRegistrationId,
        CancellationToken cancellationToken
    );

    Task<bool> ReleaseTemplateExistsAsync(
        int releaseTemplateId,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyList<HosterRegistration>> GetHosterRegistrationsAsync(
        IReadOnlyList<int> hosterRegistrationIds,
        CancellationToken cancellationToken
    );

    Task<LinkCrypterImport> GetByIdAsync(
        int linkCrypterImportId,
        CancellationToken cancellationToken
    );

    Task<LinkCrypterImport> GetWithContainersAsync(
        int linkCrypterImportId,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyList<int>> GetIdsOfImportsWithBackgroundWorkAsync(
        CancellationToken cancellationToken
    );

    Task<HashSet<string>> GetExistingLinkCrypterContainerUrlsAsync(
        IReadOnlyList<string> containerUrls,
        CancellationToken cancellationToken
    );

    Task<HashSet<string>> GetExistingReleaseNamesAsync(
        IReadOnlyList<string> releaseNames,
        CancellationToken cancellationToken
    );

    Task<bool> ReleaseWithNameExistsAsync(string releaseName, CancellationToken cancellationToken);

    Task<IReadOnlyList<int>> GetIdsOfSelectedImportableContainersAsync(
        int linkCrypterImportId,
        CancellationToken cancellationToken
    );

    Task<LinkCrypterImportContainer> GetContainerWithImportAsync(
        int linkCrypterImportContainerId,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyList<string>> GetReleaseFolderAutomationBasePathsAsync(
        CancellationToken cancellationToken
    );

    void Add(LinkCrypterImport linkCrypterImport);

    void Remove(LinkCrypterImport linkCrypterImport);

    void ClearChangeTracker();

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
