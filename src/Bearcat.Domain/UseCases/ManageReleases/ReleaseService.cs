using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Domain.ValueObjects;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManageReleases;

public class ReleaseService(IReleaseWriteRepository writeRepository, TimeProvider timeProvider)
{
    public async Task<int> CreateAsync(
        string name,
        string releaseFolderPath,
        ReleaseType releaseType,
        int releaseGroupId,
        CancellationToken cancellationToken = default
    )
    {
        var release = new Release
        {
            Name = name,
            CreatedAt = timeProvider.GetLocalNow(),
            ReleaseType = releaseType,
            ReleaseGroupId = releaseGroupId,
            ReleaseFolderPath = releaseFolderPath,
        };

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
        release.Name = name;
        release.ReleaseFolderPath = releaseFolderPath;
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
        var release = CreateFromTemplate(releaseTemplate, releaseFolderPath, name);
        release.CreatedAt = timeProvider.GetLocalNow();

        writeRepository.Add(release);
        await writeRepository.SaveChangesAsync(cancellationToken);

        return release.Id;
    }

    public static Release CreateFromTemplate(
        ReleaseTemplate releaseTemplate,
        string releaseFolderPath,
        string? name = null
    )
    {
        var releaseName = CleanOptional(name) ?? GetFolderName(releaseFolderPath);
        var archiveConfigsByTemplateId = releaseTemplate
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
                    ArchiveNamePrefix = template.UseReleaseNameAsArchiveName ? releaseName : null,
                    Archives = [],
                    UploadConfigs = [],
                },
            })
            .ToDictionary(item => item.Id, item => item.Config);

        var release = new Release
        {
            Name = releaseName,
            ReleaseFolderPath = releaseFolderPath,
            ReleaseType = releaseTemplate.ReleaseType,
            ReleaseGroupId = releaseTemplate.ReleaseGroupId,
            ArchiveConfigs = archiveConfigsByTemplateId.Values.ToList(),
            UploadConfigs = [],
            ReleaseInfos = [],
        };

        release.UploadConfigs = releaseTemplate
            .UploadConfigTemplates.Select(template => new UploadConfig
            {
                Name = CleanOptional(template.Name) ?? template.HosterRegistration.Name,
                HosterRegistrationId = template.HosterRegistrationId,
                ArchiveConfig = archiveConfigsByTemplateId[template.ArchiveConfigTemplateId],
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
