using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Shouldly;

namespace Bearcat.Infrastructure.IntegrationTest.Database.Repositories;

public class LinkCrypterContainerCreationWriteRepositoryTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private static readonly DateTime Now = new(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);

    private LinkCrypterContainerCreationWriteRepository repository = null!;

    [SetUp]
    public void Setup()
    {
        repository = new LinkCrypterContainerCreationWriteRepository(DbContext);
    }

    [Test]
    public async Task GetReleaseContainersWithUpdateRetryDueAsync_MixedContainers_ReturnsOnlyDueUpdateFailedContainersOfOnlineUploads()
    {
        // Arrange
        var activeLinkCrypter = CreateUploadConfigWithLinkCrypter("Active", isActive: true);
        var inactiveLinkCrypter = CreateUploadConfigWithLinkCrypter("Inactive", isActive: false);

        var dueContainer = AddContainer(
            activeLinkCrypter,
            LinkCrypterContainerState.UpdateFailed,
            updateFailedAt: Now.AddMinutes(-20),
            uploadOnlineState: OnlineState.Online
        );
        var dueContainerWithoutTimestamp = AddContainer(
            activeLinkCrypter,
            LinkCrypterContainerState.UpdateFailed,
            updateFailedAt: null,
            uploadOnlineState: OnlineState.Online
        );
        AddContainer(
            activeLinkCrypter,
            LinkCrypterContainerState.UpdateFailed,
            updateFailedAt: Now.AddMinutes(-5),
            uploadOnlineState: OnlineState.Online
        );
        AddContainer(
            activeLinkCrypter,
            LinkCrypterContainerState.Created,
            updateFailedAt: null,
            uploadOnlineState: OnlineState.Online
        );
        AddContainer(
            activeLinkCrypter,
            LinkCrypterContainerState.UpdateFailed,
            updateFailedAt: Now.AddMinutes(-20),
            uploadOnlineState: OnlineState.Offline
        );
        AddContainer(
            inactiveLinkCrypter,
            LinkCrypterContainerState.UpdateFailed,
            updateFailedAt: Now.AddMinutes(-20),
            uploadOnlineState: OnlineState.Online
        );
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetReleaseContainersWithUpdateRetryDueAsync(
            updateFailedBefore: Now.AddMinutes(-15),
            cancellationToken: CancellationToken.None
        );

        // Assert
        result
            .Select(container => container.Id)
            .ShouldBe([dueContainer.Id, dueContainerWithoutTimestamp.Id]);
        var first = result[0];
        first.Upload!.UploadedFiles.Single().HosterFileLink.ShouldBe("https://hoster.test/a");
        first.Upload.UploadConfig.Release.Name.ShouldBe("Bearcat.Release.Active");
        first.UploadConfigLinkCrypter!.LinkCrypterRegistration.Name.ShouldBe("Crypter Active");
    }

    [Test]
    public async Task GetReleaseIdsWithOutdatedAllHostersContainersAsync_MixedReleases_ReturnsReleasesWithMissingLatestUploadRemovedMemberOrDueRetry()
    {
        // Arrange
        var withoutContainer = CreateUploadConfigWithLinkCrypter(
            "WithoutContainer",
            isActive: true,
            LinkCrypterContainerScope.ReleaseAllHosters
        );
        AddCompletedUpload(withoutContainer.UploadConfig, Now.AddHours(-1));

        var inSync = CreateUploadConfigWithLinkCrypter(
            "InSync",
            isActive: true,
            LinkCrypterContainerScope.ReleaseAllHosters
        );
        AddAllHostersContainer(
            inSync,
            [AddCompletedUpload(inSync.UploadConfig, Now.AddHours(-1))],
            LinkCrypterContainerState.Created,
            updateFailedAt: null
        );

        var reuploaded = CreateUploadConfigWithLinkCrypter(
            "Reuploaded",
            isActive: true,
            LinkCrypterContainerScope.ReleaseAllHosters
        );
        AddAllHostersContainer(
            reuploaded,
            [AddCompletedUpload(reuploaded.UploadConfig, Now.AddHours(-2))],
            LinkCrypterContainerState.Created,
            updateFailedAt: null
        );
        AddCompletedUpload(reuploaded.UploadConfig, Now.AddHours(-1));

        var memberRemoved = CreateUploadConfigWithLinkCrypter(
            "MemberRemoved",
            isActive: true,
            LinkCrypterContainerScope.Release
        );
        AddAllHostersContainer(
            memberRemoved,
            [AddCompletedUpload(memberRemoved.UploadConfig, Now.AddHours(-1))],
            LinkCrypterContainerState.Created,
            updateFailedAt: null
        );

        var retryDue = CreateUploadConfigWithLinkCrypter(
            "RetryDue",
            isActive: true,
            LinkCrypterContainerScope.ReleaseAllHosters
        );
        AddAllHostersContainer(
            retryDue,
            [AddCompletedUpload(retryDue.UploadConfig, Now.AddHours(-1))],
            LinkCrypterContainerState.UpdateFailed,
            updateFailedAt: Now.AddMinutes(-20)
        );

        var retryNotDue = CreateUploadConfigWithLinkCrypter(
            "RetryNotDue",
            isActive: true,
            LinkCrypterContainerScope.ReleaseAllHosters
        );
        AddAllHostersContainer(
            retryNotDue,
            [AddCompletedUpload(retryNotDue.UploadConfig, Now.AddHours(-1))],
            LinkCrypterContainerState.UpdateFailed,
            updateFailedAt: Now.AddMinutes(-5)
        );

        var inactive = CreateUploadConfigWithLinkCrypter(
            "Inactive",
            isActive: false,
            LinkCrypterContainerScope.ReleaseAllHosters
        );
        AddCompletedUpload(inactive.UploadConfig, Now.AddHours(-1));

        DbContext.UploadConfigLinkCrypters.AddRange(
            withoutContainer,
            inSync,
            reuploaded,
            memberRemoved,
            retryDue,
            retryNotDue,
            inactive
        );
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetReleaseIdsWithOutdatedAllHostersContainersAsync(
            updateFailedBefore: Now.AddMinutes(-15),
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.ShouldBe(
            new[]
            {
                withoutContainer.UploadConfig.ReleaseId,
                reuploaded.UploadConfig.ReleaseId,
                memberRemoved.UploadConfig.ReleaseId,
                retryDue.UploadConfig.ReleaseId,
            }.Order()
        );
    }

    private Upload AddCompletedUpload(UploadConfig uploadConfig, DateTime uploadedAt)
    {
        var upload = new Upload
        {
            UploadConfig = uploadConfig,
            HosterRegistration = uploadConfig.HosterRegistration,
            CreatedAt = uploadedAt,
            UploadedAt = uploadedAt,
            UploadState = UploadState.Completed,
            OnlineState = OnlineState.Online,
            UploadedFiles =
            [
                new UploadedFile
                {
                    VideoFileRelativePath = "main.mkv",
                    HosterFileLink = $"https://hoster.test/{Guid.NewGuid():N}",
                    ErrorMessages = [],
                    OnlineState = OnlineState.Online,
                    CreatedAt = uploadedAt,
                },
            ],
            ErrorMessages = [],
        };
        DbContext.Uploads.Add(upload);

        return upload;
    }

    private void AddAllHostersContainer(
        UploadConfigLinkCrypter linkCrypterConfig,
        IReadOnlyList<Upload> sourceUploads,
        LinkCrypterContainerState state,
        DateTime? updateFailedAt
    )
    {
        DbContext.LinkCrypterContainers.Add(
            new LinkCrypterContainer
            {
                Scope = LinkCrypterContainerScope.ReleaseAllHosters,
                Release = linkCrypterConfig.UploadConfig.Release,
                LinkCrypterRegistration = linkCrypterConfig.LinkCrypterRegistration,
                ContainerUrl = "https://crypter.test/all-hosters",
                State = state,
                Errors = [],
                CreatedAt = Now.AddHours(-1),
                UpdateFailedAt = updateFailedAt,
                SourceUploads = sourceUploads
                    .Select(upload => new LinkCrypterContainerSourceUpload { Upload = upload })
                    .ToList(),
            }
        );
    }

    private LinkCrypterContainer AddContainer(
        UploadConfigLinkCrypter linkCrypterConfig,
        LinkCrypterContainerState state,
        DateTime? updateFailedAt,
        OnlineState uploadOnlineState
    )
    {
        var upload = new Upload
        {
            UploadConfig = linkCrypterConfig.UploadConfig,
            HosterRegistration = linkCrypterConfig.UploadConfig.HosterRegistration,
            CreatedAt = Now.AddHours(-1),
            UploadedAt = Now.AddHours(-1),
            UploadState = UploadState.Completed,
            OnlineState = uploadOnlineState,
            UploadedFiles =
            [
                new UploadedFile
                {
                    VideoFileRelativePath = "main.mkv",
                    HosterFileLink = "https://hoster.test/a",
                    ErrorMessages = [],
                    OnlineState = uploadOnlineState,
                    CreatedAt = Now.AddHours(-1),
                },
            ],
            ErrorMessages = [],
        };
        var container = new LinkCrypterContainer
        {
            Scope = LinkCrypterContainerScope.Release,
            Upload = upload,
            UploadConfigLinkCrypter = linkCrypterConfig,
            LinkCrypterRegistration = linkCrypterConfig.LinkCrypterRegistration,
            ContainerUrl = "https://crypter.test/container",
            State = state,
            Errors = [],
            CreatedAt = Now.AddHours(-1),
            UpdateFailedAt = updateFailedAt,
        };

        DbContext.LinkCrypterContainers.Add(container);

        return container;
    }

    private static UploadConfigLinkCrypter CreateUploadConfigWithLinkCrypter(
        string name,
        bool isActive,
        LinkCrypterContainerScope containerScope = LinkCrypterContainerScope.Release
    )
    {
        var uploadConfig = new UploadConfig
        {
            UploadSource = UploadSource.VideoFiles,
            Release = new Release
            {
                Name = $"Bearcat.Release.{name}",
                ReleaseType = ReleaseType.Managed,
                ReleaseFolderPath = $"/tmp/{name}",
                ReleaseGroup = new ReleaseGroup
                {
                    Name = $"Group {name}",
                    EnableAutomaticReuploads = false,
                    NumberOfHoursUntilReupload = 24,
                },
            },
            HosterRegistration = new HosterRegistration
            {
                Name = $"Hoster {name}",
                SerializedConfig = "{}",
                HosterClassName = "TestHoster",
                IsActive = true,
            },
            Name = $"Upload {name}",
        };
        var linkCrypterConfig = new UploadConfigLinkCrypter
        {
            UploadConfig = uploadConfig,
            ContainerScope = containerScope,
            LinkCrypterRegistration = new LinkCrypterRegistration
            {
                Name = $"Crypter {name}",
                LinkCrypterClassName = "TestCrypter",
                SerializedConfig = "{}",
                IsActive = isActive,
            },
        };
        uploadConfig.LinkCrypters = [linkCrypterConfig];

        return linkCrypterConfig;
    }
}
