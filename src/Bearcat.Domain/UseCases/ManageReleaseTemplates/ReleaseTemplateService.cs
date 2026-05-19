using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.Repositories;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleaseTemplates;

public class ReleaseTemplateService(IReleaseTemplateWriteRepository writeRepository)
{
    public async Task<int> CreateAsync(
        string name,
        ReleaseType releaseType,
        int releaseGroupId,
        CancellationToken cancellationToken = default
    )
    {
        var releaseTemplate = new ReleaseTemplate
        {
            Name = name,
            ReleaseType = releaseType,
            ReleaseGroupId = releaseGroupId,
        };

        writeRepository.Add(releaseTemplate);
        await writeRepository.SaveChangesAsync(cancellationToken);

        return releaseTemplate.Id;
    }

    public async Task UpdateAsync(
        int releaseTemplateId,
        string name,
        ReleaseType releaseType,
        int releaseGroupId,
        CancellationToken cancellationToken = default
    )
    {
        var releaseTemplate = await writeRepository.GetByIdAsync(
            releaseTemplateId,
            cancellationToken
        );
        releaseTemplate.Name = name;
        releaseTemplate.ReleaseType = releaseType;
        releaseTemplate.ReleaseGroupId = releaseGroupId;

        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        int releaseTemplateId,
        CancellationToken cancellationToken = default
    )
    {
        var releaseTemplate = await writeRepository.GetByIdWithChildrenAsync(
            releaseTemplateId,
            cancellationToken
        );
        writeRepository.Remove(releaseTemplate);

        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> CreateTemplateFromReleaseAsync(
        int releaseId,
        string name,
        CancellationToken cancellationToken = default
    )
    {
        var release = await writeRepository.GetReleaseForTemplateCreationAsync(
            releaseId,
            cancellationToken
        );

        var archiveConfigTemplatesByArchiveConfigId = release
            .ArchiveConfigs.Select(config => new
            {
                config.Id,
                Template = new ArchiveConfigTemplate
                {
                    Name = config.Name,
                    ArchiveFilesBasePath = config.ArchiveFilesBasePath,
                    ArchiverName = config.ArchiverName,
                    ArchivePassword = config.ArchivePassword,
                    ArchiveFileSizeMb = config.ArchiveFileSizeMb,
                    UseReleaseNameAsArchiveName = config.ArchiveNamePrefix == release.Name,
                },
            })
            .ToDictionary(item => item.Id, item => item.Template);

        var releaseTemplate = new ReleaseTemplate
        {
            Name = name,
            ReleaseType = release.ReleaseType,
            ReleaseGroupId = release.ReleaseGroupId,
            ArchiveConfigTemplates = archiveConfigTemplatesByArchiveConfigId.Values.ToList(),
        };

        releaseTemplate.UploadConfigTemplates = release
            .UploadConfigs.Select(config => new UploadConfigTemplate
            {
                ArchiveConfigTemplate = archiveConfigTemplatesByArchiveConfigId[
                    config.ArchiveConfigId
                ],
                HosterRegistrationId = config.HosterRegistrationId,
                Name = string.Equals(
                    config.Name,
                    config.HosterRegistration.Name,
                    StringComparison.Ordinal
                )
                    ? null
                    : config.Name,
                LinksDistributedTo = CleanLinks(config.LinksDistributedTo),
                LinkCrypterTemplates = config
                    .LinkCrypters.Select(linkCrypter => new UploadConfigLinkCrypterTemplate
                    {
                        LinkCrypterRegistrationId = linkCrypter.LinkCrypterRegistrationId,
                        Password = CleanOptional(linkCrypter.Password),
                    })
                    .ToList(),
            })
            .ToList();

        writeRepository.Add(releaseTemplate);
        await writeRepository.SaveChangesAsync(cancellationToken);

        return releaseTemplate.Id;
    }

    public async Task<int> CreateArchiveConfigTemplateAsync(
        int releaseTemplateId,
        string name,
        string archiveFilesBasePath,
        string archiverName,
        string? archivePassword,
        int archiveFileSizeMb,
        bool useReleaseNameAsArchiveName,
        CancellationToken cancellationToken = default
    )
    {
        var releaseTemplate = await writeRepository.GetByIdWithChildrenAsync(
            releaseTemplateId,
            cancellationToken
        );
        var archiveConfigTemplate = new ArchiveConfigTemplate
        {
            Name = name,
            ArchiveFilesBasePath = archiveFilesBasePath,
            ArchiverName = archiverName,
            ArchivePassword = CleanOptional(archivePassword),
            ArchiveFileSizeMb = archiveFileSizeMb,
            UseReleaseNameAsArchiveName = useReleaseNameAsArchiveName,
        };

        releaseTemplate.ArchiveConfigTemplates.Add(archiveConfigTemplate);
        await writeRepository.SaveChangesAsync(cancellationToken);

        return archiveConfigTemplate.Id;
    }

    public async Task UpdateArchiveConfigTemplateAsync(
        int archiveConfigTemplateId,
        string name,
        string archiveFilesBasePath,
        string archiverName,
        string? archivePassword,
        int archiveFileSizeMb,
        bool useReleaseNameAsArchiveName,
        CancellationToken cancellationToken = default
    )
    {
        var archiveConfigTemplate = await writeRepository.GetArchiveConfigTemplateAsync(
            archiveConfigTemplateId,
            cancellationToken
        );
        archiveConfigTemplate.Name = name;
        archiveConfigTemplate.ArchiveFilesBasePath = archiveFilesBasePath;
        archiveConfigTemplate.ArchiverName = archiverName;
        archiveConfigTemplate.ArchivePassword = CleanOptional(archivePassword);
        archiveConfigTemplate.ArchiveFileSizeMb = archiveFileSizeMb;
        archiveConfigTemplate.UseReleaseNameAsArchiveName = useReleaseNameAsArchiveName;

        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteArchiveConfigTemplateAsync(
        int archiveConfigTemplateId,
        CancellationToken cancellationToken = default
    )
    {
        var archiveConfigTemplate = await writeRepository.GetArchiveConfigTemplateAsync(
            archiveConfigTemplateId,
            cancellationToken
        );
        writeRepository.Remove(archiveConfigTemplate);
        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> CreateUploadConfigTemplateAsync(
        int releaseTemplateId,
        string? name,
        int hosterRegistrationId,
        int archiveConfigTemplateId,
        IReadOnlyList<string> linksDistributedTo,
        CancellationToken cancellationToken = default
    )
    {
        var releaseTemplate = await writeRepository.GetByIdWithChildrenAsync(
            releaseTemplateId,
            cancellationToken
        );
        var uploadConfigTemplate = new UploadConfigTemplate
        {
            Name = CleanOptional(name),
            HosterRegistrationId = hosterRegistrationId,
            ArchiveConfigTemplateId = archiveConfigTemplateId,
            LinksDistributedTo = CleanLinks(linksDistributedTo),
        };

        releaseTemplate.UploadConfigTemplates.Add(uploadConfigTemplate);
        await writeRepository.SaveChangesAsync(cancellationToken);

        return uploadConfigTemplate.Id;
    }

    public async Task UpdateUploadConfigTemplateAsync(
        int uploadConfigTemplateId,
        string? name,
        int hosterRegistrationId,
        int archiveConfigTemplateId,
        IReadOnlyList<string> linksDistributedTo,
        CancellationToken cancellationToken = default
    )
    {
        var uploadConfigTemplate = await writeRepository.GetUploadConfigTemplateAsync(
            uploadConfigTemplateId,
            cancellationToken
        );
        uploadConfigTemplate.Name = CleanOptional(name);
        uploadConfigTemplate.HosterRegistrationId = hosterRegistrationId;
        uploadConfigTemplate.ArchiveConfigTemplateId = archiveConfigTemplateId;
        uploadConfigTemplate.LinksDistributedTo = CleanLinks(linksDistributedTo);

        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteUploadConfigTemplateAsync(
        int uploadConfigTemplateId,
        CancellationToken cancellationToken = default
    )
    {
        var uploadConfigTemplate = await writeRepository.GetUploadConfigTemplateAsync(
            uploadConfigTemplateId,
            cancellationToken
        );
        writeRepository.Remove(uploadConfigTemplate);
        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> CreateUploadConfigLinkCrypterTemplateAsync(
        int uploadConfigTemplateId,
        int linkCrypterRegistrationId,
        string? password,
        CancellationToken cancellationToken = default
    )
    {
        var uploadConfigTemplate = await writeRepository.GetUploadConfigTemplateAsync(
            uploadConfigTemplateId,
            cancellationToken
        );
        var linkCrypterTemplate = new UploadConfigLinkCrypterTemplate
        {
            LinkCrypterRegistrationId = linkCrypterRegistrationId,
            Password = CleanOptional(password),
        };

        uploadConfigTemplate.LinkCrypterTemplates.Add(linkCrypterTemplate);
        await writeRepository.SaveChangesAsync(cancellationToken);

        return linkCrypterTemplate.Id;
    }

    public async Task UpdateUploadConfigLinkCrypterTemplateAsync(
        int uploadConfigLinkCrypterTemplateId,
        string? password,
        CancellationToken cancellationToken = default
    )
    {
        var linkCrypterTemplate = await writeRepository.GetUploadConfigLinkCrypterTemplateAsync(
            uploadConfigLinkCrypterTemplateId,
            cancellationToken
        );
        linkCrypterTemplate.Password = CleanOptional(password);

        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteUploadConfigLinkCrypterTemplateAsync(
        int uploadConfigLinkCrypterTemplateId,
        CancellationToken cancellationToken = default
    )
    {
        var linkCrypterTemplate = await writeRepository.GetUploadConfigLinkCrypterTemplateAsync(
            uploadConfigLinkCrypterTemplateId,
            cancellationToken
        );
        writeRepository.Remove(linkCrypterTemplate);
        await writeRepository.SaveChangesAsync(cancellationToken);
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
}
