using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class LinkCrypterImportRepository(IBearcatWriteDbContext dbWrite)
    : ILinkCrypterImportRepository
{
    private static readonly LinkCrypterImportState[] StatesWithBackgroundWork =
    [
        LinkCrypterImportState.ReadingContainers,
        LinkCrypterImportState.CheckingFiles,
        LinkCrypterImportState.Importing,
    ];

    public async Task<LinkCrypterRegistration?> GetLinkCrypterRegistrationOrDefaultAsync(
        int linkCrypterRegistrationId,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite.LinkCrypterRegistrations.FirstOrDefaultAsync(
            registration => registration.Id == linkCrypterRegistrationId,
            cancellationToken
        );
    }

    public async Task<bool> ReleaseTemplateExistsAsync(
        int releaseTemplateId,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite.ReleaseTemplates.AnyAsync(
            template => template.Id == releaseTemplateId,
            cancellationToken
        );
    }

    public async Task<IReadOnlyList<HosterRegistration>> GetHosterRegistrationsAsync(
        IReadOnlyList<int> hosterRegistrationIds,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite
            .HosterRegistrations.Where(registration =>
                hosterRegistrationIds.Contains(registration.Id)
            )
            .ToListAsync(cancellationToken);
    }

    public async Task<LinkCrypterImport> GetByIdAsync(
        int linkCrypterImportId,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite
            .LinkCrypterImports.Include(import => import.HosterRegistrations)
            .FirstAsync(import => import.Id == linkCrypterImportId, cancellationToken);
    }

    public async Task<LinkCrypterImport> GetWithContainersAsync(
        int linkCrypterImportId,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite
            .LinkCrypterImports.AsSplitQuery()
            .Include(import => import.LinkCrypterRegistration)
            .Include(import => import.HosterRegistrations)
                .ThenInclude(hosterRegistration => hosterRegistration.HosterRegistration)
            .Include(import => import.Containers)
            .FirstAsync(import => import.Id == linkCrypterImportId, cancellationToken);
    }

    public async Task<IReadOnlyList<int>> GetIdsOfImportsWithBackgroundWorkAsync(
        CancellationToken cancellationToken
    )
    {
        return await dbWrite
            .LinkCrypterImports.Where(import => StatesWithBackgroundWork.Contains(import.State))
            .OrderBy(import => import.Id)
            .Select(import => import.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<HashSet<string>> GetExistingLinkCrypterContainerUrlsAsync(
        IReadOnlyList<string> containerUrls,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite
            .LinkCrypterContainers.Where(container =>
                EF.Parameter(containerUrls).Contains(container.ContainerUrl)
            )
            .Select(container => container.ContainerUrl)
            .ToHashSetAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Release>> GetReleasesByNameAsync(
        IReadOnlyList<string> releaseNames,
        CancellationToken cancellationToken
    )
    {
        var lowerReleaseNames = releaseNames
            .Select(releaseName => releaseName.ToLowerInvariant())
            .ToList();

        return await dbWrite
            .Releases.AsSplitQuery()
            .Include(release => release.UploadConfigs)
                .ThenInclude(uploadConfig => uploadConfig.HosterRegistration)
            .Include(release => release.UploadConfigs)
                .ThenInclude(uploadConfig => uploadConfig.HosterAccountPool)
            .Where(release => EF.Parameter(lowerReleaseNames).Contains(release.Name.ToLower()))
            .OrderBy(release => release.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<Release?> GetReleaseForImportByNameOrDefaultAsync(
        string releaseName,
        CancellationToken cancellationToken
    )
    {
        var lowerReleaseName = releaseName.ToLowerInvariant();

        return await dbWrite
            .Releases.AsSplitQuery()
            .Include(release => release.LinkCrypterContainers)
            .Include(release => release.UploadConfigs)
                .ThenInclude(uploadConfig => uploadConfig.HosterRegistration)
            .Include(release => release.UploadConfigs)
                .ThenInclude(uploadConfig => uploadConfig.HosterAccountPool)
            .Include(release => release.UploadConfigs)
                .ThenInclude(uploadConfig =>
                    uploadConfig.Uploads.Where(upload =>
                        upload.UploadState == UploadState.Completed
                    )
                )
                    .ThenInclude(upload => upload.UploadedFiles)
            .Include(release => release.UploadConfigs)
                .ThenInclude(uploadConfig => uploadConfig.ArchiveConfig)
                    .ThenInclude(archiveConfig => archiveConfig!.Archives)
                        .ThenInclude(archive => archive.ArchiveFiles)
            .Include(release => release.UploadConfigs)
                .ThenInclude(uploadConfig => uploadConfig.LinkCrypters)
                    .ThenInclude(linkCrypter => linkCrypter.LinkCrypterContainers)
            .OrderBy(release => release.Id)
            .FirstOrDefaultAsync(
                release => release.Name.ToLower() == lowerReleaseName,
                cancellationToken
            );
    }

    public async Task<IReadOnlyList<int>> GetIdsOfSelectedImportableContainersAsync(
        int linkCrypterImportId,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite
            .LinkCrypterImportContainers.Where(container =>
                container.LinkCrypterImportId == linkCrypterImportId
                && container.IsSelected
                && container.Status == LinkCrypterImportContainerStatus.Importable
            )
            .OrderBy(container => container.Id)
            .Select(container => container.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<LinkCrypterImportContainer> GetContainerWithImportAsync(
        int linkCrypterImportContainerId,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite
            .LinkCrypterImportContainers.Include(container => container.LinkCrypterImport)
                .ThenInclude(import => import.HosterRegistrations)
            .FirstAsync(
                container => container.Id == linkCrypterImportContainerId,
                cancellationToken
            );
    }

    public async Task<IReadOnlyList<string>> GetReleaseFolderAutomationBasePathsAsync(
        CancellationToken cancellationToken
    )
    {
        return await dbWrite
            .ReleaseFolderAutomations.Select(automation => automation.BasePath)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public void Add(LinkCrypterImport linkCrypterImport)
    {
        dbWrite.Add(linkCrypterImport);
    }

    public void Remove(LinkCrypterImport linkCrypterImport)
    {
        dbWrite.Remove(linkCrypterImport);
    }

    public void ClearChangeTracker()
    {
        dbWrite.ChangeTracker.Clear();
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await dbWrite.SaveChangesAsync(cancellationToken);
    }
}
