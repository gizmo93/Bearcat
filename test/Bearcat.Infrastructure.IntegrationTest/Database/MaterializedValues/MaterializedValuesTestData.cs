using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;

namespace Bearcat.Infrastructure.IntegrationTest.Database.MaterializedValues;

internal sealed class MaterializedValuesTestData(BearcatDbContext dbContext)
{
    public static readonly DateTime LocalNow = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    public HosterRegistration AddHosterRegistration(string name, bool isActive = true)
    {
        var hosterRegistration = new HosterRegistration
        {
            Name = name,
            SerializedConfig = "{}",
            HosterClassName = $"{name}Hoster",
            IsActive = isActive,
        };
        dbContext.HosterRegistrations.Add(hosterRegistration);

        return hosterRegistration;
    }

    public Release AddRelease(string name)
    {
        var release = new Release
        {
            Name = name,
            CreatedAt = LocalNow,
            ReleaseType = ReleaseType.Unmanaged,
            ReleaseGroup = new ReleaseGroup
            {
                Name = $"{name} group",
                EnableAutomaticReuploads = false,
                NumberOfHoursUntilReupload = 24,
            },
        };
        dbContext.Releases.Add(release);

        return release;
    }

    public ArchiveConfig AddArchiveConfig(Release release)
    {
        var archiveConfig = new ArchiveConfig
        {
            Release = release,
            Name = "Main archive",
            ArchiveFilesBasePath = "/archives",
            ArchiverName = "rar",
            ArchiveFileSizeMb = 512,
        };
        dbContext.ArchiveConfigs.Add(archiveConfig);

        return archiveConfig;
    }

    public UploadConfig AddUploadConfig(
        Release release,
        HosterRegistration hosterRegistration,
        ArchiveConfig? archiveConfig = null
    )
    {
        var uploadConfig = new UploadConfig
        {
            UploadSource = UploadSource.ArchiveFiles,
            Release = release,
            ArchiveConfig = archiveConfig ?? AddArchiveConfig(release),
            HosterRegistration = hosterRegistration,
            Name = $"{hosterRegistration.Name} upload",
        };
        dbContext.UploadConfigs.Add(uploadConfig);

        return uploadConfig;
    }

    public HosterAccountPool AddHosterAccountPool(
        string name,
        params HosterRegistration[] membersInPriorityOrder
    )
    {
        var hosterAccountPool = new HosterAccountPool
        {
            Name = name,
            HosterClassName = $"{name}Hoster",
            Members = membersInPriorityOrder
                .Select(
                    (member, index) =>
                        new HosterAccountPoolMember
                        {
                            HosterRegistration = member,
                            Priority = index + 1,
                        }
                )
                .ToList(),
        };
        dbContext.HosterAccountPools.Add(hosterAccountPool);

        return hosterAccountPool;
    }

    public UploadConfig AddHosterAccountPoolUploadConfig(
        Release release,
        HosterAccountPool hosterAccountPool,
        ArchiveConfig? archiveConfig = null
    )
    {
        var uploadConfig = new UploadConfig
        {
            UploadSource = UploadSource.ArchiveFiles,
            Release = release,
            ArchiveConfig = archiveConfig ?? AddArchiveConfig(release),
            HosterAccountPool = hosterAccountPool,
            Name = $"{hosterAccountPool.Name} upload",
        };
        dbContext.UploadConfigs.Add(uploadConfig);

        return uploadConfig;
    }

    public UploadConfigTemplate AddHosterAccountPoolUploadConfigTemplate(
        HosterAccountPool hosterAccountPool
    )
    {
        var releaseTemplate = new ReleaseTemplate
        {
            Name = $"{hosterAccountPool.Name} template",
            ReleaseType = ReleaseType.Managed,
            ReleaseContentType = ReleaseContentType.Movie,
            ReleaseGroup = new ReleaseGroup
            {
                Name = $"{hosterAccountPool.Name} template group",
                EnableAutomaticReuploads = false,
                NumberOfHoursUntilReupload = 24,
            },
        };
        var uploadConfigTemplate = new UploadConfigTemplate
        {
            UploadSource = UploadSource.ArchiveFiles,
            ReleaseTemplate = releaseTemplate,
            ArchiveConfigTemplate = new ArchiveConfigTemplate
            {
                ReleaseTemplate = releaseTemplate,
                Name = "RAR",
                ArchiveFilesBasePath = "/archives",
                ArchiverName = "rar",
                ArchiveFileSizeMb = 512,
            },
            HosterAccountPool = hosterAccountPool,
        };
        dbContext.UploadConfigTemplates.Add(uploadConfigTemplate);

        return uploadConfigTemplate;
    }

    public Upload AddUnassignedUpload(UploadConfig uploadConfig, UploadState uploadState)
    {
        var upload = new Upload
        {
            UploadConfig = uploadConfig,
            CreatedAt = LocalNow,
            UploadState = uploadState,
            OnlineState = OnlineState.Unknown,
            UploadedFiles = [],
        };
        dbContext.Uploads.Add(upload);

        return upload;
    }

    public Upload AddUpload(
        UploadConfig uploadConfig,
        OnlineState onlineState,
        params DateTime?[] fileCheckedAts
    )
    {
        var archive = AddArchive(uploadConfig.ArchiveConfig!, fileCheckedAts.Length);
        var upload = new Upload
        {
            UploadConfig = uploadConfig,
            HosterRegistration = uploadConfig.HosterRegistration,
            Archive = archive,
            CreatedAt = LocalNow,
            UploadedAt = LocalNow,
            UploadState = UploadState.Completed,
            OnlineState = onlineState,
            UploadedFiles = [],
        };
        dbContext.Uploads.Add(upload);

        for (var index = 0; index < fileCheckedAts.Length; index++)
        {
            AddUploadedFile(upload, archive.ArchiveFiles[index], fileCheckedAts[index]);
        }

        return upload;
    }

    public Archive AddArchive(ArchiveConfig archiveConfig, int archiveFileCount)
    {
        var archive = new Archive
        {
            ArchiveConfig = archiveConfig,
            ArchiveFolderPath = "/archives/release",
            ArchiveState = ArchiveState.Created,
            ArchiveFileSizeMb = 512,
            CreatedAt = LocalNow,
            ArchiveFiles = Enumerable
                .Range(1, archiveFileCount)
                .Select(number => new ArchiveFile
                {
                    FullFileName = $"/archives/release/release.part{number}.rar",
                })
                .ToList(),
            Uploads = [],
        };
        dbContext.Archives.Add(archive);

        return archive;
    }

    public UploadedFile AddUploadedFile(Upload upload, ArchiveFile archiveFile, DateTime? checkedAt)
    {
        var uploadedFile = new UploadedFile
        {
            Upload = upload,
            ArchiveFile = archiveFile,
            HosterFileLink = $"https://hoster.test/{archiveFile.FullFileName}",
            OnlineState = upload.OnlineState,
            CreatedAt = LocalNow,
            CheckedAt = checkedAt,
        };
        upload.UploadedFiles.Add(uploadedFile);
        dbContext.UploadedFiles.Add(uploadedFile);

        return uploadedFile;
    }
}
