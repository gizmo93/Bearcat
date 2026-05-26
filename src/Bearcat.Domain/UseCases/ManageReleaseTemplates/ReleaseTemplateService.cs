using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.Repositories;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleaseTemplates;

public class ReleaseTemplateService(IReleaseTemplateWriteRepository writeRepository)
{
    private const string UnmanagedArchiveConfigTemplateName = "Unmanaged archives";
    private const string UnmanagedArchiveFilesBasePath = "Release folder";
    private const string UnmanagedArchiverName = "Unmanaged";

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

        EnsureUnmanagedArchiveConfigTemplate(releaseTemplate);

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
        var releaseTemplate = await writeRepository.GetByIdWithChildrenAsync(
            releaseTemplateId,
            cancellationToken
        );
        if (releaseTemplate.ReleaseType != releaseType)
        {
            throw new InvalidOperationException("Release template type cannot be changed.");
        }

        releaseTemplate.Name = name;
        releaseTemplate.ReleaseGroupId = releaseGroupId;
        EnsureUnmanagedArchiveConfigTemplate(releaseTemplate);

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

        var releaseTemplate = new ReleaseTemplate
        {
            Name = name,
            ReleaseType = release.ReleaseType,
            ReleaseGroupId = release.ReleaseGroupId,
        };

        Dictionary<int, ArchiveConfigTemplate> archiveConfigTemplatesByArchiveConfigId;
        ArchiveConfigTemplate? unmanagedArchiveConfigTemplate = null;

        if (release.ReleaseType is ReleaseType.Managed)
        {
            archiveConfigTemplatesByArchiveConfigId = release
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
            releaseTemplate.ArchiveConfigTemplates =
                archiveConfigTemplatesByArchiveConfigId.Values.ToList();
        }
        else
        {
            unmanagedArchiveConfigTemplate = CreateUnmanagedArchiveConfigTemplate();
            releaseTemplate.ArchiveConfigTemplates = [unmanagedArchiveConfigTemplate];
            archiveConfigTemplatesByArchiveConfigId = [];
        }

        releaseTemplate.UploadConfigTemplates = release
            .UploadConfigs.Select(config => new UploadConfigTemplate
            {
                ArchiveConfigTemplate =
                    release.ReleaseType is ReleaseType.Managed
                        ? archiveConfigTemplatesByArchiveConfigId[config.ArchiveConfigId]
                        : unmanagedArchiveConfigTemplate!,
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
        EnsureManagedReleaseTemplate(releaseTemplate);
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
        EnsureManagedReleaseTemplate(archiveConfigTemplate.ReleaseTemplate);

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
        EnsureManagedReleaseTemplate(archiveConfigTemplate.ReleaseTemplate);

        writeRepository.Remove(archiveConfigTemplate);
        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task EnsureUnmanagedArchiveConfigTemplateAsync(
        int releaseTemplateId,
        CancellationToken cancellationToken = default
    )
    {
        var releaseTemplate = await writeRepository.GetByIdWithChildrenAsync(
            releaseTemplateId,
            cancellationToken
        );
        EnsureUnmanagedArchiveConfigTemplate(releaseTemplate);
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
        EnsureUnmanagedArchiveConfigTemplate(releaseTemplate);
        var archiveConfigTemplate = ResolveArchiveConfigTemplate(
            releaseTemplate,
            archiveConfigTemplateId
        );
        var uploadConfigTemplate = new UploadConfigTemplate
        {
            Name = CleanOptional(name),
            HosterRegistrationId = hosterRegistrationId,
            ArchiveConfigTemplateId = archiveConfigTemplate?.Id ?? archiveConfigTemplateId,
            LinksDistributedTo = CleanLinks(linksDistributedTo),
        };
        if (archiveConfigTemplate is not null)
        {
            uploadConfigTemplate.ArchiveConfigTemplate = archiveConfigTemplate;
        }

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
        var archiveConfigTemplate = ResolveArchiveConfigTemplate(
            uploadConfigTemplate.ReleaseTemplate,
            archiveConfigTemplateId
        );
        uploadConfigTemplate.Name = CleanOptional(name);
        uploadConfigTemplate.HosterRegistrationId = hosterRegistrationId;
        uploadConfigTemplate.ArchiveConfigTemplateId =
            archiveConfigTemplate?.Id ?? archiveConfigTemplateId;
        if (archiveConfigTemplate is not null)
        {
            uploadConfigTemplate.ArchiveConfigTemplate = archiveConfigTemplate;
        }
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

    private static void EnsureManagedReleaseTemplate(ReleaseTemplate releaseTemplate)
    {
        if (releaseTemplate.ReleaseType is ReleaseType.Managed)
        {
            return;
        }

        throw new InvalidOperationException(
            "Archive config templates for unmanaged release templates cannot be changed."
        );
    }

    private static ArchiveConfigTemplate? ResolveArchiveConfigTemplate(
        ReleaseTemplate releaseTemplate,
        int archiveConfigTemplateId
    )
    {
        if (releaseTemplate.ReleaseType is ReleaseType.Managed)
        {
            return null;
        }

        EnsureUnmanagedArchiveConfigTemplate(releaseTemplate);
        return releaseTemplate.ArchiveConfigTemplates.Single();
    }

    private static void EnsureUnmanagedArchiveConfigTemplate(ReleaseTemplate releaseTemplate)
    {
        if (releaseTemplate.ReleaseType is not ReleaseType.Unmanaged)
        {
            return;
        }

        if (releaseTemplate.ArchiveConfigTemplates.Count > 0)
        {
            return;
        }

        releaseTemplate.ArchiveConfigTemplates.Add(CreateUnmanagedArchiveConfigTemplate());
    }

    private static ArchiveConfigTemplate CreateUnmanagedArchiveConfigTemplate()
    {
        return new ArchiveConfigTemplate
        {
            Name = UnmanagedArchiveConfigTemplateName,
            ArchiveFilesBasePath = UnmanagedArchiveFilesBasePath,
            ArchiverName = UnmanagedArchiverName,
            ArchivePassword = null,
            ArchiveFileSizeMb = 0,
            UseReleaseNameAsArchiveName = false,
        };
    }

    private static List<string> CleanLinks(IReadOnlyList<string> links)
    {
        return links
            .Where(link => !string.IsNullOrWhiteSpace(link))
            .Select(link => link.Trim())
            .ToList();
    }
}
