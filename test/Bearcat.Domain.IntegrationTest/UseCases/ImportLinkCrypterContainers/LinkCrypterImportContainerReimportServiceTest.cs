using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Exceptions;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Preview;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Reimporting;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ImportLinkCrypterContainers;

public class LinkCrypterImportContainerReimportServiceTest(DatabaseProvider databaseProvider)
    : LinkCrypterImportIntegrationTest(databaseProvider)
{
    private const string SeasonContainerUrl = "https://crypter.test/season";
    private const string SecondCrypterContainerUrl = "https://second-crypter.test/season";
    private const string ThirdHosterUploadConfigName = "Third account";

    private static readonly DateTime SeasonUploadsPostedAt = StartTime.AddDays(-3);

    [Test]
    public async Task ReimportAsync_SeasonImportedAsOneRelease_SplitsUploadsIntoEpisodesOfReleaseCollection()
    {
        // Arrange
        var seasonImport = await ImportSeasonContainerAsOneReleaseAsync();
        var originalUploadedFileIds = await GetUploadedFileIdsAsync();
        await AddPostedLocationAsync(seasonImport.SeasonReleaseId);

        // Act
        await CreateReimportService().ReimportAsync(seasonImport.ContainerId);

        // Assert
        var dbContext = CreateDbContext();

        (
            await dbContext.Releases.AnyAsync(release => release.Id == seasonImport.SeasonReleaseId)
        ).ShouldBeFalse();

        var releaseCollection = await LoadReleaseCollectionAsync();
        releaseCollection.Name.ShouldBe(SeasonContainerName);
        releaseCollection.UploadsPostedAt.ShouldBe(SeasonUploadsPostedAt);

        releaseCollection
            .Releases.Select(release => (release.Name, release.UploadsPostedAt))
            .OrderBy(release => release.Name)
            .ShouldBe([
                (FirstEpisodeReleaseName, SeasonUploadsPostedAt),
                (SecondEpisodeReleaseName, SeasonUploadsPostedAt),
            ]);

        foreach (
            var episodeReleaseName in new[] { FirstEpisodeReleaseName, SecondEpisodeReleaseName }
        )
        {
            var episodeRelease = await LoadReleaseAsync(episodeReleaseName);

            episodeRelease
                .UploadConfigs.Select(uploadConfig => uploadConfig.Uploads.Count)
                .ShouldBe([1, 1, 1]);

            episodeRelease
                .UploadConfigs.SelectMany(uploadConfig => uploadConfig.Uploads)
                .SelectMany(upload => upload.UploadedFiles)
                .Select(uploadedFile => Path.GetFileName(uploadedFile.ArchiveFile!.FullFileName))
                .Distinct()
                .Order()
                .ShouldBe([$"{episodeReleaseName}.part1.rar", $"{episodeReleaseName}.part2.rar"]);

            episodeRelease
                .UploadConfigs.SelectMany(uploadConfig => uploadConfig.Uploads)
                .ShouldAllBe(upload =>
                    upload.UploadState == UploadState.Completed
                    && upload.UploadedFiles.Count == 2
                    && upload.ArchiveId == upload.UploadedFiles.First().ArchiveFile!.ArchiveId
                );
        }

        (await GetUploadedFileIdsAsync()).ShouldBe(originalUploadedFileIds);

        var linkCrypterContainer = (await LoadLinkCrypterContainersAsync()).ShouldHaveSingleItem();
        linkCrypterContainer.Scope.ShouldBe(LinkCrypterContainerScope.ReleaseCollection);
        linkCrypterContainer.ContainerUrl.ShouldBe(SeasonContainerUrl);

        linkCrypterContainer.CollectionUploadSlotId.ShouldBe(
            releaseCollection.UploadSlots.Single().Id
        );

        var uploadIdsOfSlot = releaseCollection
            .UploadSlots.Single()
            .UploadConfigs.SelectMany(uploadConfig => uploadConfig.Uploads)
            .Select(upload => upload.Id)
            .Order()
            .ToList();

        uploadIdsOfSlot.Count.ShouldBe(4);

        linkCrypterContainer
            .SourceUploads.Select(sourceUpload => sourceUpload.UploadId)
            .Order()
            .ShouldBe(uploadIdsOfSlot);

        var postedLocation = await dbContext.PostedLocations.SingleAsync();
        postedLocation.ReleaseId.ShouldBeNull();
        postedLocation.ReleaseCollectionId.ShouldBe(releaseCollection.Id);

        var container = await dbContext.LinkCrypterImportContainers.SingleAsync();
        container.Status.ShouldBe(LinkCrypterImportContainerStatus.Imported);
        container.ImportedReleaseId.ShouldBeNull();
        container.ImportedReleaseCollectionId.ShouldBe(releaseCollection.Id);

        LinkCrypterImportContainerPreview
            .FromJson(container.PreviewJson)
            .Releases!.Select(releasePreview => releasePreview.ReleaseName)
            .ShouldBe([FirstEpisodeReleaseName, SecondEpisodeReleaseName]);
    }

    [Test]
    public async Task ReimportAsync_ContainerOfSecondCrypterOnRelease_ConvertsItIntoCollectionContainer()
    {
        // Arrange
        var seasonImport = await ImportSeasonContainerAsOneReleaseAsync();
        var secondCrypterImport = await AddSecondCrypterContainerOfReleaseAsync(seasonImport);

        // Act
        await CreateReimportService().ReimportAsync(seasonImport.ContainerId);

        // Assert
        var releaseCollection = await LoadReleaseCollectionAsync();
        var slot = releaseCollection.UploadSlots.Single();

        var secondCrypterContainer = (await LoadLinkCrypterContainersAsync()).Single(
            linkCrypterContainer => linkCrypterContainer.ContainerUrl == SecondCrypterContainerUrl
        );

        secondCrypterContainer.Scope.ShouldBe(LinkCrypterContainerScope.ReleaseCollection);
        secondCrypterContainer.CollectionUploadSlotId.ShouldBe(slot.Id);
        secondCrypterContainer.ReleaseId.ShouldBeNull();
        secondCrypterContainer.Password.ShouldBe("second-secret");

        secondCrypterContainer
            .SourceUploads.Select(sourceUpload => sourceUpload.UploadId)
            .Order()
            .ShouldBe(
                slot.UploadConfigs.SelectMany(uploadConfig => uploadConfig.Uploads)
                    .Select(upload => upload.Id)
                    .Order()
            );

        slot.UploadConfigs.ShouldAllBe(uploadConfig =>
            uploadConfig.LinkCrypters.Any(linkCrypter =>
                linkCrypter.LinkCrypterRegistrationId
                    == secondCrypterImport.LinkCrypterRegistrationId
                && linkCrypter.ContainerScope == LinkCrypterContainerScope.ReleaseCollection
                && linkCrypter.Password == "second-secret"
            )
        );

        var secondCrypterImportContainer = await CreateDbContext()
            .LinkCrypterImportContainers.SingleAsync(container =>
                container.Id == secondCrypterImport.ContainerId
            );

        secondCrypterImportContainer.ImportedReleaseId.ShouldBeNull();
        secondCrypterImportContainer.ImportedReleaseCollectionId.ShouldBe(releaseCollection.Id);
    }

    [Test]
    public async Task ReimportAsync_UploadOfReleaseInProgress_FailsWithoutChanges()
    {
        // Arrange
        var seasonImport = await ImportSeasonContainerAsOneReleaseAsync();
        await AddThirdHosterUploadAsync(seasonImport.SeasonReleaseId, UploadState.Uploading);

        // Act
        var plan = await CreateReimportService().CreatePlanAsync(seasonImport.ContainerId);

        var exception = await Should.ThrowAsync<InvalidLinkCrypterImportException>(() =>
            CreateReimportService().ReimportAsync(seasonImport.ContainerId)
        );

        // Assert
        plan.Blockers.ShouldBe([
            new LinkCrypterImportReimportBlocker(
                Reason: LinkCrypterImportReimportBlockerReason.UploadInProgress,
                Detail: ThirdHosterUploadConfigName,
                NotImportableReason: null
            ),
        ]);

        exception.Message.ShouldContain("UploadInProgress");

        var dbContext = CreateDbContext();
        (await dbContext.Releases.SingleAsync()).Id.ShouldBe(seasonImport.SeasonReleaseId);
        (await dbContext.ReleaseCollections.CountAsync()).ShouldBe(0);

        (await dbContext.LinkCrypterImportContainers.SingleAsync()).ImportedReleaseId.ShouldBe(
            seasonImport.SeasonReleaseId
        );
    }

    [Test]
    public async Task CreatePlanAsync_SeasonImportedAsOneRelease_DescribesReimportWithoutChangingDatabase()
    {
        // Arrange
        var seasonImport = await ImportSeasonContainerAsOneReleaseAsync();
        var originalUploadedFileIds = await GetUploadedFileIdsAsync();

        var originalPreviewJson = (
            await CreateDbContext().LinkCrypterImportContainers.SingleAsync()
        ).PreviewJson;

        // Act
        var plan = await CreateReimportService().CreatePlanAsync(seasonImport.ContainerId);

        // Assert
        plan.Blockers.ShouldBeEmpty();
        plan.ImportedReleaseName.ShouldBe(SeasonContainerName);
        plan.ReleaseCollectionName.ShouldBe(SeasonContainerName);
        plan.UploadsNotMoved.ShouldBeEmpty();
        plan.ConvertedContainerUrls.ShouldBeEmpty();
        plan.ContainersNotConverted.ShouldBeEmpty();

        plan.Releases.Select(release =>
                (
                    release.Preview.ReleaseName,
                    release.Preview.ReleaseAction,
                    string.Join(
                        ", ",
                        release.MovedUploads.Select(movedUpload =>
                            $"{movedUpload.UploadConfigName}: {movedUpload.FileCount}"
                        )
                    )
                )
            )
            .ShouldBe([
                (
                    FirstEpisodeReleaseName,
                    LinkCrypterImportReleaseAction.CreateRelease,
                    "First account: 2, Second account: 2, Third account: 2"
                ),
                (
                    SecondEpisodeReleaseName,
                    LinkCrypterImportReleaseAction.CreateRelease,
                    "First account: 2, Second account: 2, Third account: 2"
                ),
            ]);

        var dbContext = CreateDbContext();
        (await dbContext.Releases.SingleAsync()).Id.ShouldBe(seasonImport.SeasonReleaseId);
        (await dbContext.ReleaseCollections.CountAsync()).ShouldBe(0);
        (await GetUploadedFileIdsAsync()).ShouldBe(originalUploadedFileIds);

        var container = await dbContext.LinkCrypterImportContainers.SingleAsync();
        container.ImportedReleaseId.ShouldBe(seasonImport.SeasonReleaseId);
        container.PreviewJson.ShouldBe(originalPreviewJson);
    }

    [Test]
    public async Task ReimportAsync_SeasonReleaseWithArchiveInFolderOfAllEpisodes_MovesPartsIntoEpisodeSubfoldersOnlyWhenReimporting()
    {
        // Arrange
        var seasonFolderPath = CreateSeasonArchivePartFiles(
            Path.Combine(WorkingDirectoryPath, SeasonContainerName)
        );

        var seasonImport = await ImportSeasonContainerAsOneReleaseAsync(seasonFolderPath);

        // Act
        var plan = await CreateReimportService().CreatePlanAsync(seasonImport.ContainerId);

        var fileNamesInSeasonFolderAfterPlan = Directory
            .GetFiles(seasonFolderPath)
            .Select(filePath => Path.GetFileName(filePath))
            .Order()
            .ToList();

        await CreateReimportService().ReimportAsync(seasonImport.ContainerId);

        // Assert
        plan.Blockers.ShouldBeEmpty();

        plan.Releases.ShouldAllBe(release =>
            release.Preview.ArchiveFolderPath == seasonFolderPath
            && release.Preview.ArchiveFolderContainsArchivesOfOtherReleases
        );

        fileNamesInSeasonFolderAfterPlan.ShouldBe(GetSeasonArchiveFileNames());
        Directory.GetFiles(seasonFolderPath).ShouldBeEmpty();

        foreach (
            var episodeReleaseName in new[] { FirstEpisodeReleaseName, SecondEpisodeReleaseName }
        )
        {
            var episodeFolderPath = Path.Combine(seasonFolderPath, episodeReleaseName);
            var episodeRelease = await LoadReleaseAsync(episodeReleaseName);
            var archive = episodeRelease.ArchiveConfigs.Single().Archives.Single();

            archive.ArchiveState.ShouldBe(ArchiveState.Created);
            archive.ArchiveFolderPath.ShouldBe(episodeFolderPath);
            archive.ArchiveFiles.ShouldAllBe(archiveFile => File.Exists(archiveFile.FullFileName));

            episodeRelease
                .UploadConfigs.SelectMany(uploadConfig => uploadConfig.Uploads)
                .ShouldAllBe(upload => upload.ArchiveId == archive.Id);
        }
    }

    [Test]
    public async Task StartReimportAsync_TwoOfThreeSeasonReleasesSelected_ReimportsSelectedContainersInBackground()
    {
        // Arrange
        var linkCrypterImportId = await ImportSeasonContainersAsOneReleaseEachAsync([1, 2, 3]);

        await CreateLinkCrypterImportService(CreateDbContext())
            .SetReleaseSelectionAsync(
                linkCrypterImportId,
                GetSeasonContainerName(3),
                isSelected: false
            );

        var thirdSeasonReleaseId = (await LoadReleaseAsync(GetSeasonContainerName(3))).Id;

        // Act
        await StartReimportAndProcessAsync(linkCrypterImportId);

        // Assert
        (await CreateDbContext().Releases.Select(release => release.Name).ToListAsync())
            .Order()
            .ShouldBe([
                GetEpisodeReleaseName(1, 1),
                GetEpisodeReleaseName(1, 2),
                GetEpisodeReleaseName(2, 1),
                GetEpisodeReleaseName(2, 2),
                GetSeasonContainerName(3),
            ]);

        var containersByReleaseName = await LoadContainersByReleaseNameAsync(linkCrypterImportId);

        foreach (var seasonNumber in new[] { 1, 2 })
        {
            var container = containersByReleaseName[GetSeasonContainerName(seasonNumber)];
            container.ImportedReleaseId.ShouldBeNull();
            container.ImportedReleaseCollectionId.ShouldNotBeNull();
            container.ErrorMessages.ShouldBeEmpty();
        }

        var thirdSeasonContainer = containersByReleaseName[GetSeasonContainerName(3)];
        thirdSeasonContainer.ImportedReleaseId.ShouldBe(thirdSeasonReleaseId);
        thirdSeasonContainer.IsSelected.ShouldBeFalse();

        var linkCrypterImport = await LoadImportAsync(linkCrypterImportId);
        linkCrypterImport.State.ShouldBe(LinkCrypterImportState.Completed);
        linkCrypterImport.ProcessedContainerCount.ShouldBe(2);
        linkCrypterImport.ContainerCountToProcess.ShouldBe(2);

        var notification = await LoadLatestNotificationAsync();
        notification.NotificationKind.ShouldBe(NotificationKind.LinkCrypterImportCompleted);
        notification.Message.ShouldContain("2 containers were re-imported, 0 containers failed.");
    }

    [Test]
    public async Task StartReimportAsync_UploadOfOneSelectedReleaseInProgress_KeepsItsReleaseWithErrorAndReimportsOthers()
    {
        // Arrange
        var linkCrypterImportId = await ImportSeasonContainersAsOneReleaseEachAsync([1, 2, 3]);
        await SetFirstUploadOfReleaseToUploadingAsync(GetSeasonContainerName(2));
        var secondSeasonReleaseId = (await LoadReleaseAsync(GetSeasonContainerName(2))).Id;

        // Act
        await StartReimportAndProcessAsync(linkCrypterImportId);

        // Assert
        var containersByReleaseName = await LoadContainersByReleaseNameAsync(linkCrypterImportId);

        var blockedContainer = containersByReleaseName[GetSeasonContainerName(2)];
        blockedContainer.Status.ShouldBe(LinkCrypterImportContainerStatus.Imported);
        blockedContainer.ImportedReleaseId.ShouldBe(secondSeasonReleaseId);
        blockedContainer.ErrorMessages.ShouldHaveSingleItem().ShouldContain("UploadInProgress");

        foreach (var seasonNumber in new[] { 1, 3 })
        {
            var container = containersByReleaseName[GetSeasonContainerName(seasonNumber)];
            container.ImportedReleaseId.ShouldBeNull();
            container.ImportedReleaseCollectionId.ShouldNotBeNull();
        }

        (await LoadImportAsync(linkCrypterImportId)).State.ShouldBe(
            LinkCrypterImportState.Completed
        );

        var notification = await LoadLatestNotificationAsync();
        notification.NotificationKind.ShouldBe(NotificationKind.LinkCrypterImportFailed);
        notification.Message.ShouldContain("2 containers were re-imported, 1 containers failed.");
    }

    [Test]
    public async Task StartReimportAsync_TwoSelectedContainers_SearchesLocalFilesOnce()
    {
        // Arrange
        var linkCrypterImportId = await ImportSeasonContainersAsOneReleaseEachAsync([1, 2]);
        PreviewFileSystemServiceMock.Invocations.Clear();

        // Act
        await StartReimportAndProcessAsync(linkCrypterImportId);

        // Assert
        PreviewFileSystemServiceMock.Verify(
            service => service.GetFoldersInPath(WorkingDirectoryPath),
            Times.Once
        );

        (await LoadImportAsync(linkCrypterImportId)).Containers.ShouldAllBe(container =>
            container.ImportedReleaseId == null && container.ImportedReleaseCollectionId != null
        );
    }

    private async Task<SeasonImport> ImportSeasonContainerAsOneReleaseAsync(
        string? archiveFolderPath = null
    )
    {
        var seed = await AddReleaseTemplateAsync(
            releaseType: ReleaseType.Unmanaged,
            releaseCollectionDetectionMode: ReleaseCollectionDetectionMode.SeriesEpisodePattern,
            firstHosterCollectionUploadSlotKey: SeasonSlotKey,
            secondHosterCollectionUploadSlotKey: SeasonSlotKey
        );

        await AddThirdHosterUploadConfigTemplateAsync(seed);

        AddSeasonContainer();
        var linkCrypterImportId = await StartImportAsync(seed);
        await ProcessAsync();
        await ChooseDefaultHosterRegistrationsAsync(linkCrypterImportId, seed);
        await ProcessAsync();
        var containerId = (
            await ReplacePreviewsWithOneSeasonReleaseEachAsync(
                linkCrypterImportId,
                archiveFolderPath
            )
        ).Single();

        await CreateLinkCrypterImportService(CreateDbContext())
            .StartImportAsync(linkCrypterImportId);

        await ProcessAsync();

        var dbContext = CreateDbContext();

        var seasonRelease = await dbContext.Releases.SingleAsync(release =>
            release.Name == SeasonContainerName
        );

        seasonRelease.UploadsPostedAt = SeasonUploadsPostedAt;
        await dbContext.SaveChangesAsync();

        await AddThirdHosterUploadAsync(seasonRelease.Id, UploadState.Completed);

        return new SeasonImport(seed, containerId, seasonRelease.Id);
    }

    private async Task<List<int>> ReplacePreviewsWithOneSeasonReleaseEachAsync(
        int linkCrypterImportId,
        string? archiveFolderPath
    )
    {
        var dbContext = CreateDbContext();

        var containers = await dbContext
            .LinkCrypterImportContainers.Where(container =>
                container.LinkCrypterImportId == linkCrypterImportId
            )
            .OrderBy(container => container.Id)
            .ToListAsync();

        foreach (var container in containers)
        {
            var preview = LinkCrypterImportContainerPreview.FromJson(container.PreviewJson);

            container.PreviewJson = (
                preview with
                {
                    ReleaseCollectionName = null,
                    Releases =
                    [
                        new LinkCrypterImportReleasePreview(
                            ReleaseName: container.ReleaseName,
                            Mirrors: preview
                                .Mirrors.Select(mirror =>
                                    mirror with
                                    {
                                        Decision = LinkCrypterImportMirrorDecision.Adopt,
                                    }
                                )
                                .ToList(),
                            ReleaseFolderPath: null,
                            ArchiveFolderPath: archiveFolderPath,
                            ArchiveFolderContainsArchivesOfOtherReleases: false,
                            ReleaseAction: LinkCrypterImportReleaseAction.CreateRelease,
                            ExistingReleaseId: null,
                            NotImportableReason: null
                        ),
                    ],
                }
            ).ToJson();
        }

        await dbContext.SaveChangesAsync();

        return containers.Select(container => container.Id).ToList();
    }

    private async Task<int> ImportSeasonContainersAsOneReleaseEachAsync(
        IReadOnlyList<int> seasonNumbers
    )
    {
        var seed = await AddReleaseTemplateAsync(
            releaseType: ReleaseType.Unmanaged,
            releaseCollectionDetectionMode: ReleaseCollectionDetectionMode.SeriesEpisodePattern,
            firstHosterCollectionUploadSlotKey: SeasonSlotKey,
            secondHosterCollectionUploadSlotKey: SeasonSlotKey
        );

        foreach (var seasonNumber in seasonNumbers)
        {
            AddContainerOfSeason(seasonNumber);
        }

        var linkCrypterImportId = await StartImportAsync(seed);
        await ProcessAsync();
        await ChooseDefaultHosterRegistrationsAsync(linkCrypterImportId, seed);
        await ProcessAsync();

        await ReplacePreviewsWithOneSeasonReleaseEachAsync(
            linkCrypterImportId,
            archiveFolderPath: null
        );

        await CreateLinkCrypterImportService(CreateDbContext())
            .StartImportAsync(linkCrypterImportId);

        await ProcessAsync();

        return linkCrypterImportId;
    }

    private void AddContainerOfSeason(int seasonNumber)
    {
        var fileNames = Enumerable
            .Range(1, 2)
            .SelectMany(episodeNumber =>
                Enumerable
                    .Range(1, 2)
                    .Select(partNumber =>
                        $"{GetEpisodeReleaseName(seasonNumber, episodeNumber)}.part{partNumber}.rar"
                    )
            )
            .ToList();

        AddListedContainer(
            $"https://crypter.test/season-{seasonNumber}",
            GetSeasonContainerName(seasonNumber),
            fileNames
                .Select(
                    (fileName, index) =>
                        OnlineArchiveLink($"https://first.test/s{seasonNumber}-{index}", fileName)
                )
                .ToList(),
            fileNames
                .Select(
                    (fileName, index) =>
                        OnlineArchiveLink($"https://second.test/s{seasonNumber}-{index}", fileName)
                )
                .ToList()
        );
    }

    private static string GetSeasonContainerName(int seasonNumber)
    {
        return $"Bearcat.Show.S{seasonNumber:00}.German.1080p.WEB.x264-GRP";
    }

    private static string GetEpisodeReleaseName(int seasonNumber, int episodeNumber)
    {
        return $"Bearcat.Show.S{seasonNumber:00}E{episodeNumber:00}.German.1080p.WEB.x264-GRP";
    }

    private async Task StartReimportAndProcessAsync(int linkCrypterImportId)
    {
        await CreateLinkCrypterImportService(CreateDbContext())
            .StartReimportAsync(linkCrypterImportId);

        await ProcessAsync();
    }

    private async Task<
        Dictionary<string, LinkCrypterImportContainer>
    > LoadContainersByReleaseNameAsync(int linkCrypterImportId)
    {
        return (await LoadImportAsync(linkCrypterImportId)).Containers.ToDictionary(container =>
            container.ReleaseName
        );
    }

    private async Task<Notification> LoadLatestNotificationAsync()
    {
        return await CreateDbContext()
            .Notifications.OrderByDescending(notification => notification.Id)
            .FirstAsync();
    }

    private async Task SetFirstUploadOfReleaseToUploadingAsync(string releaseName)
    {
        var dbContext = CreateDbContext();

        var upload = await dbContext
            .Uploads.OrderBy(upload => upload.Id)
            .FirstAsync(upload => upload.UploadConfig.Release.Name == releaseName);

        upload.UploadState = UploadState.Uploading;
        await dbContext.SaveChangesAsync();
    }

    private async Task AddThirdHosterUploadAsync(int seasonReleaseId, UploadState uploadState)
    {
        var dbContext = CreateDbContext();

        var seasonRelease = await dbContext
            .Releases.Include(release => release.UploadConfigs)
                .ThenInclude(uploadConfig => uploadConfig.HosterRegistration)
            .Include(release => release.ArchiveConfigs)
                .ThenInclude(archiveConfig => archiveConfig.Archives)
                    .ThenInclude(archive => archive.ArchiveFiles)
            .SingleAsync(release => release.Id == seasonReleaseId);

        var thirdHosterUploadConfig = seasonRelease.UploadConfigs.Single(uploadConfig =>
            uploadConfig.HosterRegistration!.HosterClassName == ThirdHosterClassName
        );

        var archive = seasonRelease.ArchiveConfigs.Single().Archives.Single();

        dbContext.Uploads.Add(
            new Upload
            {
                UploadConfigId = thirdHosterUploadConfig.Id,
                HosterRegistrationId = thirdHosterUploadConfig.HosterRegistrationId,
                ArchiveId = archive.Id,
                CreatedAt = StartTime,
                UploadedAt = uploadState is UploadState.Completed ? StartTime : null,
                UploadState = uploadState,
                OnlineState = OnlineState.Online,
                ErrorMessages = [],
                UploadedFiles = archive
                    .ArchiveFiles.OrderBy(archiveFile => archiveFile.Id)
                    .Select(
                        (archiveFile, index) =>
                            new UploadedFile
                            {
                                ArchiveFileId = archiveFile.Id,
                                HosterFileLink = $"https://third.test/{uploadState}/{index}",
                                OnlineState = OnlineState.Online,
                                ErrorMessages = [],
                                CreatedAt = StartTime,
                            }
                    )
                    .ToList(),
            }
        );

        await dbContext.SaveChangesAsync();
    }

    private async Task AddPostedLocationAsync(int seasonReleaseId)
    {
        var dbContext = CreateDbContext();

        dbContext.PostedLocations.Add(
            new PostedLocation
            {
                ReleaseId = seasonReleaseId,
                Url = "https://forum.test/threads/season",
                CreatedAt = StartTime,
            }
        );

        await dbContext.SaveChangesAsync();
    }

    private async Task<SecondCrypterImport> AddSecondCrypterContainerOfReleaseAsync(
        SeasonImport seasonImport
    )
    {
        var linkCrypterRegistrationId = await AddLinkCrypterRegistrationAsync();
        var dbContext = CreateDbContext();

        var uploadIdsOfSeasonContainer = await dbContext
            .LinkCrypterContainerSourceUploads.Select(sourceUpload => sourceUpload.UploadId)
            .ToListAsync();

        dbContext.LinkCrypterContainers.Add(
            new LinkCrypterContainer
            {
                Scope = LinkCrypterContainerScope.ReleaseAllHosters,
                ReleaseId = seasonImport.SeasonReleaseId,
                LinkCrypterRegistrationId = linkCrypterRegistrationId,
                ContainerUrl = SecondCrypterContainerUrl,
                Password = "second-secret",
                State = LinkCrypterContainerState.Created,
                Errors = [],
                CreatedAt = StartTime,
                SourceUploads = uploadIdsOfSeasonContainer
                    .Select(uploadId => new LinkCrypterContainerSourceUpload
                    {
                        UploadId = uploadId,
                    })
                    .ToList(),
            }
        );

        var importContainer = new LinkCrypterImportContainer
        {
            ContainerId = SecondCrypterContainerUrl,
            ContainerUrl = SecondCrypterContainerUrl,
            ContainerName = SeasonContainerName,
            ReleaseName = SeasonContainerName,
            ExternalReference = "second-reference",
            Status = LinkCrypterImportContainerStatus.Imported,
            ImportedReleaseId = seasonImport.SeasonReleaseId,
            ErrorMessages = [],
            PreviewJson = new LinkCrypterImportContainerPreview([], null, null).ToJson(),
        };

        dbContext.LinkCrypterImports.Add(
            new LinkCrypterImport
            {
                LinkCrypterRegistrationId = linkCrypterRegistrationId,
                ReleaseTemplateId = seasonImport.Seed.ReleaseTemplateId,
                State = LinkCrypterImportState.Completed,
                CreatedAt = StartTime,
                UpdatedAt = StartTime,
                ErrorMessages = [],
                Containers = [importContainer],
            }
        );

        await dbContext.SaveChangesAsync();

        return new SecondCrypterImport(linkCrypterRegistrationId, importContainer.Id);
    }

    private async Task<List<int>> GetUploadedFileIdsAsync()
    {
        return await CreateDbContext()
            .UploadedFiles.Select(uploadedFile => uploadedFile.Id)
            .Order()
            .ToListAsync();
    }

    private LinkCrypterImportContainerReimportService CreateReimportService()
    {
        return CreateReimportService(CreateDbContext(), new ControllableTimeProvider(StartTime));
    }

    private sealed record SeasonImport(TemplateSeed Seed, int ContainerId, int SeasonReleaseId);

    private sealed record SecondCrypterImport(int LinkCrypterRegistrationId, int ContainerId);
}
