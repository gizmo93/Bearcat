using Bearcat.Abstractions.Archiver;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Domain.ValueObjects;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManageReleases;

public class ReleaseService(
    IReleaseWriteRepository writeRepository,
    TimeProvider timeProvider,
    IArchiverFactory archiverFactory
)
{
    public async Task<int> CreateAsync(
        string name,
        string releaseFolderPath,
        ReleaseType releaseType,
        int releaseGroupId,
        CancellationToken cancellationToken = default
    )
    {
        var localNow = timeProvider.GetLocalNow();

        var release = new Release
        {
            Name = name,
            CreatedAt = localNow,
            ReleaseType = releaseType,
            ReleaseGroupId = releaseGroupId,
            ReleaseFolderPath = releaseFolderPath,
            ArchiveConfigs = [],
            UploadConfigs = [],
        };

        if (release.ReleaseType is ReleaseType.Unmanaged)
        {
            EnsureInitialUnmanagedArchive(release, archiverFactory.GetArchivers(), localNow);
        }

        writeRepository.Add(release);
        await writeRepository.SaveChangesAsync(cancellationToken);

        return release.Id;
    }

    public async Task UpdateAsync(
        int releaseId,
        string name,
        string releaseFolderPath,
        int releaseGroupId,
        CancellationToken cancellationToken = default
    )
    {
        var release = await writeRepository.GetByIdAsync(releaseId, cancellationToken);

        var oldReleaseFolderPath = release.ReleaseFolderPath;

        if (
            release.ReleaseType is ReleaseType.Unmanaged
            && !string.Equals(oldReleaseFolderPath, releaseFolderPath, StringComparison.Ordinal)
        )
        {
            release.ReleaseFolderPath = releaseFolderPath;
            try
            {
                RefreshUnmanagedArchiveConfigs(
                    release,
                    archiverFactory.GetArchivers(),
                    timeProvider.GetLocalNow()
                );
            }
            catch
            {
                release.ReleaseFolderPath = oldReleaseFolderPath;
                throw;
            }
        }
        else
        {
            release.ReleaseFolderPath = releaseFolderPath;
        }

        release.Name = name;
        release.ReleaseGroupId = releaseGroupId;

        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateReleaseGroupAsync(
        IReadOnlyCollection<int> releaseIds,
        int releaseGroupId,
        CancellationToken cancellationToken = default
    )
    {
        if (releaseIds.Count == 0)
        {
            return;
        }

        var releases = await writeRepository.GetByIdsAsync(releaseIds, cancellationToken);

        foreach (var release in releases)
        {
            release.ReleaseGroupId = releaseGroupId;
        }

        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int releaseId, CancellationToken cancellationToken = default)
    {
        var release = await writeRepository.GetByIdAsync(releaseId, cancellationToken);
        writeRepository.Remove(release);

        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> CreateFromTemplateAsync(
        int releaseTemplateId,
        string releaseFolderPath,
        string? name = null,
        CancellationToken cancellationToken = default
    )
    {
        var releaseTemplate = await writeRepository.GetTemplateForReleaseCreationAsync(
            releaseTemplateId,
            cancellationToken
        );

        var localNow = timeProvider.GetLocalNow();

        var release = CreateFromTemplate(
            releaseTemplate,
            releaseFolderPath,
            name,
            releaseTemplate.ReleaseType,
            releaseTemplate.ReleaseType is ReleaseType.Unmanaged
                ? archiverFactory.GetArchivers()
                : [],
            localNow
        );

        release.CreatedAt = localNow;

        writeRepository.Add(release);
        await writeRepository.SaveChangesAsync(cancellationToken);

        return release.Id;
    }

    public static Release CreateFromTemplate(
        ReleaseTemplate releaseTemplate,
        string releaseFolderPath,
        string? name,
        ReleaseType releaseType,
        IReadOnlyList<ArchiverDto> archivers,
        DateTime localNow
    )
    {
        var releaseName = CleanOptional(name) ?? GetFolderName(releaseFolderPath);

        var release = new Release
        {
            Name = releaseName,
            ReleaseFolderPath = releaseFolderPath,
            ReleaseType = releaseType,
            ReleaseGroupId = releaseTemplate.ReleaseGroupId,
            ArchiveConfigs = [],
            UploadConfigs = [],
        };

        var archiveConfigsByTemplateId = new Dictionary<int, ArchiveConfig>();
        ArchiveConfig? unmanagedArchiveConfig = null;

        if (releaseType is ReleaseType.Managed)
        {
            archiveConfigsByTemplateId = releaseTemplate
                .ArchiveConfigTemplates.Select(template => new
                {
                    template.Id,
                    Config = new ArchiveConfig
                    {
                        Name = template.Name,
                        ArchiveFilesBasePath = template.ArchiveFilesBasePath,
                        ArchiverName = template.ArchiverName,
                        ArchivePassword = template.ArchivePassword,
                        ArchiveFileSizeMb = template.ArchiveFileSizeMb,
                        ArchiveNamePrefix = template.UseReleaseNameAsArchiveName
                            ? releaseName
                            : null,
                        Archives = [],
                        UploadConfigs = [],
                    },
                })
                .ToDictionary(item => item.Id, item => item.Config);
            release.ArchiveConfigs = archiveConfigsByTemplateId.Values.ToList();
        }
        else
        {
            unmanagedArchiveConfig = UnmanagedReleaseArchiveInitializer.CreateArchiveConfig(
                release,
                archivers,
                localNow
            );
            release.ArchiveConfigs.Add(unmanagedArchiveConfig);
        }

        release.UploadConfigs = releaseTemplate
            .UploadConfigTemplates.Select(template => new UploadConfig
            {
                Name = CleanOptional(template.Name) ?? template.HosterRegistration.Name,
                HosterRegistrationId = template.HosterRegistrationId,
                PremiumOnlyDownload = template.PremiumOnlyDownload,
                ArchiveConfig =
                    releaseType is ReleaseType.Managed
                        ? archiveConfigsByTemplateId[template.ArchiveConfigTemplateId]
                        : unmanagedArchiveConfig!,
                LinksDistributedTo = CleanLinks(template.LinksDistributedTo),
                Uploads = [],
                LinkCrypters = template
                    .LinkCrypterTemplates.Select(linkCrypter => new UploadConfigLinkCrypter
                    {
                        LinkCrypterRegistrationId = linkCrypter.LinkCrypterRegistrationId,
                        Password = CleanOptional(linkCrypter.Password),
                        LinkCrypterContainers = [],
                    })
                    .ToList(),
            })
            .ToList();

        return release;
    }

    private static void EnsureInitialUnmanagedArchive(
        Release release,
        IReadOnlyList<ArchiverDto> archivers,
        DateTime localNow
    )
    {
        if (release.ReleaseType is not ReleaseType.Unmanaged || release.ArchiveConfigs.Count > 0)
        {
            return;
        }

        release.ArchiveConfigs.Add(
            UnmanagedReleaseArchiveInitializer.CreateArchiveConfig(release, archivers, localNow)
        );
    }

    private static void RefreshUnmanagedArchiveConfigs(
        Release release,
        IReadOnlyList<ArchiverDto> archivers,
        DateTime localNow
    )
    {
        foreach (var archiveConfig in release.ArchiveConfigs)
        {
            UnmanagedReleaseArchiveInitializer.RefreshArchiveConfig(
                archiveConfig: archiveConfig,
                archivers: archivers,
                createdAt: localNow
            );
        }
    }

    private static string? CleanOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static List<string> CleanLinks(IReadOnlyList<string> links)
    {
        return links
            .Where(link => !string.IsNullOrWhiteSpace(link))
            .Select(link => link.Trim())
            .ToList();
    }

    private static string GetFolderName(string folderPath)
    {
        var normalizedPath = folderPath.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar
        );
        return Path.GetFileName(normalizedPath);
    }
}
