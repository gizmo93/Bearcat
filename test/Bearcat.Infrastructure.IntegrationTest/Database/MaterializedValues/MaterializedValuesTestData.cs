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
            Release = release,
            ArchiveConfig = archiveConfig ?? AddArchiveConfig(release),
            HosterRegistration = hosterRegistration,
            Name = $"{hosterRegistration.Name} upload",
        };
        dbContext.UploadConfigs.Add(uploadConfig);

        return uploadConfig;
    }

    public Upload AddUpload(
        UploadConfig uploadConfig,
        OnlineState onlineState,
        params DateTime?[] fileCheckedAts
    )
    {
        var archive = AddArchive(uploadConfig.ArchiveConfig, fileCheckedAts.Length);
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
