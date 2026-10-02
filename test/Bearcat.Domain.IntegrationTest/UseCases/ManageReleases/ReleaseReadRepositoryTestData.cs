using Bearcat.Abstractions.ImageHoster.Results;
using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageReleases;

internal sealed class ReleaseReadRepositoryTestData(BearcatDbContext dbContext)
{
    public static readonly DateTime MidnightUtc = new(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);

    public Release AddRelease(
        string name,
        ReleaseContentType releaseContentType = ReleaseContentType.Movie,
        ReleaseType releaseType = ReleaseType.Managed,
        string? releaseFolderPath = null
    )
    {
        var releaseGroup = new ReleaseGroup
        {
            Name = $"{name} group",
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
            Releases = [],
        };
        var release = new Release
        {
            Name = name,
            CreatedAt = MidnightUtc,
            ReleaseType = releaseType,
            ReleaseContentType = releaseContentType,
            ReleaseFolderPath =
                releaseType == ReleaseType.Managed
                    ? releaseFolderPath ?? $"/data/releases/{name}"
                    : null,
            ReleaseGroup = releaseGroup,
            ArchiveConfigs = [],
            UploadConfigs = [],
        };
        dbContext.Releases.Add(release);

        return release;
    }

    public ReleaseCollection AddReleaseCollection(string name)
    {
        var releaseCollection = new ReleaseCollection
        {
            Name = name,
            Key = name.ToLowerInvariant(),
            CreatedAt = MidnightUtc,
            ReleaseContentType = ReleaseContentType.TvShowEpisode,
            ReleaseGroup = new ReleaseGroup
            {
                Name = $"{name} group",
                EnableAutomaticReuploads = false,
                NumberOfHoursUntilReupload = 24,
                Releases = [],
            },
        };
        dbContext.ReleaseCollections.Add(releaseCollection);

        return releaseCollection;
    }

    public HosterRegistration AddHosterRegistration(string name)
    {
        var hosterRegistration = new HosterRegistration
        {
            Name = name,
            SerializedConfig = "{}",
            HosterClassName = "ExampleHoster",
            IsActive = true,
            UploadConfigs = [],
        };
        dbContext.HosterRegistrations.Add(hosterRegistration);

        return hosterRegistration;
    }

    public ArchiveConfig AddArchiveConfig(
        Release release,
        string name,
        string archiverName = "RarArchiver",
        string? archivePassword = null
    )
    {
        var archiveConfig = new ArchiveConfig
        {
            Release = release,
            Name = name,
            ArchiveFilesBasePath = "/data/archives",
            ArchiverName = archiverName,
            ArchivePassword = archivePassword,
            ArchiveFileSizeMb = 100,
            Archives = [],
            UploadConfigs = [],
        };
        dbContext.ArchiveConfigs.Add(archiveConfig);

        return archiveConfig;
    }

    public UploadConfig AddUploadConfig(
        Release release,
        string name,
        HosterRegistration? hosterRegistration = null,
        ArchiveConfig? archiveConfig = null
    )
    {
        var uploadConfig = new UploadConfig
        {
            Release = release,
            Name = name,
            HosterRegistration = hosterRegistration ?? AddHosterRegistration($"{name} hoster"),
            ArchiveConfig = archiveConfig ?? AddArchiveConfig(release, $"{name} archive"),
            LinkCrypters = [],
            Uploads = [],
        };
        dbContext.UploadConfigs.Add(uploadConfig);

        return uploadConfig;
    }

    public Upload AddUpload(
        UploadConfig uploadConfig,
        DateTime createdAt,
        DateTime? uploadedAt,
        UploadState uploadState = UploadState.Completed,
        OnlineState onlineState = OnlineState.Online
    )
    {
        var upload = new Upload
        {
            UploadConfig = uploadConfig,
            CreatedAt = createdAt,
            UploadedAt = uploadedAt,
            UploadState = uploadState,
            OnlineState = onlineState,
            UploadedFiles = [],
            LinkCrypterContainers = [],
            Notifications = [],
        };
        dbContext.Uploads.Add(upload);

        return upload;
    }

    public UploadedFile AddUploadedFile(
        Upload upload,
        string fullFileName,
        string hosterFileLink,
        OnlineState onlineState = OnlineState.Online
    )
    {
        upload.Archive ??= AddArchive(upload.UploadConfig.ArchiveConfig, upload.CreatedAt);

        var archiveFile = new ArchiveFile
        {
            Archive = upload.Archive,
            FullFileName = fullFileName,
            UploadedFiles = [],
        };
        var uploadedFile = new UploadedFile
        {
            Upload = upload,
            ArchiveFile = archiveFile,
            HosterFileLink = hosterFileLink,
            OnlineState = onlineState,
            CreatedAt = upload.CreatedAt,
        };
        dbContext.AddRange(archiveFile, uploadedFile);

        return uploadedFile;
    }

    public Archive AddArchive(
        ArchiveConfig archiveConfig,
        DateTime createdAt,
        ArchiveState archiveState = ArchiveState.Created
    )
    {
        var archive = new Archive
        {
            ArchiveConfig = archiveConfig,
            ArchiveFolderPath = "/data/archives",
            CreatedAt = createdAt,
            ArchiveState = archiveState,
            ArchiveFileSizeMb = 100,
            ArchiveFiles = [],
            Uploads = [],
            Notifications = [],
        };
        dbContext.Archives.Add(archive);

        return archive;
    }

    public LinkCrypterRegistration AddLinkCrypterRegistration(
        string name,
        string linkCrypterClassName = "FileCrypt"
    )
    {
        var linkCrypterRegistration = new LinkCrypterRegistration
        {
            Name = name,
            LinkCrypterClassName = linkCrypterClassName,
            SerializedConfig = "{}",
            IsActive = true,
        };
        dbContext.LinkCrypterRegistrations.Add(linkCrypterRegistration);

        return linkCrypterRegistration;
    }

    public UploadConfigLinkCrypter AddUploadConfigLinkCrypter(
        UploadConfig uploadConfig,
        LinkCrypterRegistration linkCrypterRegistration,
        LinkCrypterContainerScope containerScope = LinkCrypterContainerScope.Release,
        string? password = null
    )
    {
        var uploadConfigLinkCrypter = new UploadConfigLinkCrypter
        {
            UploadConfig = uploadConfig,
            LinkCrypterRegistration = linkCrypterRegistration,
            ContainerScope = containerScope,
            Password = password,
            LinkCrypterContainers = [],
        };
        dbContext.UploadConfigLinkCrypters.Add(uploadConfigLinkCrypter);

        return uploadConfigLinkCrypter;
    }

    public LinkCrypterContainer AddReleaseContainer(
        Upload upload,
        LinkCrypterRegistration linkCrypterRegistration,
        string containerUrl,
        DateTime createdAt,
        UploadConfigLinkCrypter? uploadConfigLinkCrypter = null
    )
    {
        var container = new LinkCrypterContainer
        {
            Scope = LinkCrypterContainerScope.Release,
            Upload = upload,
            UploadConfigLinkCrypter = uploadConfigLinkCrypter,
            LinkCrypterRegistration = linkCrypterRegistration,
            ContainerUrl = containerUrl,
            State = LinkCrypterContainerState.Created,
            CreatedAt = createdAt,
            Notifications = [],
        };
        dbContext.LinkCrypterContainers.Add(container);

        return container;
    }

    public LinkCrypterContainer AddCollectionContainer(
        IReadOnlyList<Upload> sourceUploads,
        LinkCrypterRegistration linkCrypterRegistration,
        string containerUrl,
        DateTime createdAt
    )
    {
        var container = new LinkCrypterContainer
        {
            Scope = LinkCrypterContainerScope.ReleaseCollection,
            LinkCrypterRegistration = linkCrypterRegistration,
            ContainerUrl = containerUrl,
            State = LinkCrypterContainerState.Created,
            CreatedAt = createdAt,
            SourceUploads = sourceUploads
                .Select(upload => new LinkCrypterContainerSourceUpload { Upload = upload })
                .ToList(),
            Notifications = [],
        };
        dbContext.LinkCrypterContainers.Add(container);

        return container;
    }

    public ImageUploadConfig AddImageUploadConfig(
        string name,
        Release? release = null,
        ReleaseCollection? releaseCollection = null
    )
    {
        var imageUploadConfig = new ImageUploadConfig
        {
            Release = release,
            ReleaseCollection = releaseCollection,
            Name = name,
            ImageHosterRegistration = new ImageHosterRegistration
            {
                Name = $"{name} image hoster",
                ImageHosterClassName = "PiXhost",
                SerializedConfig = "{}",
                IsActive = true,
            },
        };
        dbContext.ImageUploadConfigs.Add(imageUploadConfig);

        return imageUploadConfig;
    }

    public ImageUpload AddImageUpload(
        ImageUploadConfig imageUploadConfig,
        DateTime createdAt,
        DateTime? uploadedAt,
        IReadOnlyList<ImageUploadUrl> imageUrls,
        UploadState uploadState = UploadState.Completed
    )
    {
        var imageUpload = new ImageUpload
        {
            ImageUploadConfig = imageUploadConfig,
            CreatedAt = createdAt,
            UploadedAt = uploadedAt,
            UploadState = uploadState,
            ImageUrls = imageUrls.ToList(),
        };
        dbContext.ImageUploads.Add(imageUpload);

        return imageUpload;
    }

    public static ImageUploadUrl CreateImageUrl(ImageSize imageSize, string url)
    {
        return new ImageUploadUrl { ImageSize = imageSize, Url = url };
    }
}
