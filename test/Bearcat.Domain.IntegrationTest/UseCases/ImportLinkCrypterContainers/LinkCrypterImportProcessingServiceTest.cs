using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Results;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Preview;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ImportLinkCrypterContainers;

public class LinkCrypterImportProcessingServiceTest(DatabaseProvider databaseProvider)
    : LinkCrypterImportIntegrationTest(databaseProvider)
{
    [Test]
    public async Task ProcessAsync_ContainersRead_MarksKnownContainersAndWaitsForHosterRegistrations()
    {
        // Arrange
        var seed = await AddReleaseTemplateAsync(ReleaseType.Unmanaged);
        await AddExistingLinkCrypterContainerAsync(seed, "https://crypter.test/existing");
        AddListedContainer(
            "https://crypter.test/new",
            ReleaseName,
            [OnlineArchiveLink("https://first.test/a1", FirstArchiveFileName)],
            [OnlineArchiveLink("https://second.test/b1", FirstArchiveFileName)]
        );
        AddListedContainer("https://crypter.test/existing", "Other.Release", []);
        var linkCrypterImportId = await StartImportAsync(seed);

        // Act
        await ProcessAsync();

        // Assert
        var linkCrypterImport = await LoadImportAsync(linkCrypterImportId);
        linkCrypterImport.State.ShouldBe(LinkCrypterImportState.WaitingForHosterRegistrations);
        linkCrypterImport
            .Containers.OrderBy(container => container.Id)
            .Select(container => container.Status)
            .ShouldBe([
                LinkCrypterImportContainerStatus.WaitingForFileCheck,
                LinkCrypterImportContainerStatus.AlreadyInBearcat,
            ]);
        linkCrypterImport
            .HosterRegistrations.OrderBy(choice => choice.HosterClassName)
            .Select(choice => (choice.HosterClassName, choice.HosterRegistrationId))
            .ShouldBe([
                (FirstHosterClassName, seed.FirstHosterRegistrationId),
                (SecondHosterClassName, seed.SecondHosterRegistrationId),
            ]);
        VerifyFileInfosWereNeverRequested(FirstHosterClassName);
        VerifyFileInfosWereNeverRequested(SecondHosterClassName);
    }

    [Test]
    public async Task ProcessAsync_FileCheckFails_ReturnsToHosterRegistrationChoiceWithError()
    {
        // Arrange
        var seed = await AddReleaseTemplateAsync(ReleaseType.Unmanaged);
        AddListedContainer(
            "https://crypter.test/container",
            ReleaseName,
            [OnlineArchiveLink("https://first.test/a1", FirstArchiveFileName)]
        );
        HosterMocksByClassName[FirstHosterClassName]
            .Setup(hoster =>
                hoster.GetFileInfosAsync(
                    It.IsAny<IHosterConfig>(),
                    It.IsAny<IReadOnlyList<string>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new FileInfoResult(
                    IsSuccess: false,
                    ErrorMessages: ["Quota exceeded"],
                    FileInfoPerFileLink: new Dictionary<string, HostedFileInfo>()
                )
            );
        var linkCrypterImportId = await StartImportAsync(seed);
        await ProcessAsync();
        await ChooseDefaultHosterRegistrationsAsync(linkCrypterImportId, seed);

        // Act
        await ProcessAsync();

        // Assert
        var linkCrypterImport = await LoadImportAsync(linkCrypterImportId);
        linkCrypterImport.State.ShouldBe(LinkCrypterImportState.WaitingForHosterRegistrations);
        linkCrypterImport.ErrorMessages.ShouldHaveSingleItem().ShouldContain("Quota exceeded");
        linkCrypterImport
            .Containers.ShouldHaveSingleItem()
            .Status.ShouldBe(LinkCrypterImportContainerStatus.WaitingForFileCheck);
    }

    [Test]
    public async Task ProcessAsync_FilesChecked_DecidesMirrorsAndNotifiesReadyForReview()
    {
        // Arrange
        var seed = await AddReleaseTemplateAsync(ReleaseType.Unmanaged);
        AddListedContainer(
            "https://crypter.test/first",
            ReleaseName,
            [
                OnlineArchiveLink("https://first.test/a1", FirstArchiveFileName),
                OnlineArchiveLink("https://first.test/a2", SecondArchiveFileName),
            ],
            [
                OnlineArchiveLink("https://second.test/b1", FirstArchiveFileName),
                OfflineLink("https://second.test/b2"),
            ],
            ["https://unknown.test/c1"],
            ["https://third.test/d1"]
        );
        AddListedContainer(
            "https://crypter.test/second",
            OtherReleaseName,
            [
                OnlineArchiveLink("https://first.test/e1", FirstArchiveFileName),
                OnlineArchiveLink("https://first.test/e2", SecondArchiveFileName),
            ],
            [OnlineArchiveLink("https://second.test/f1", FirstArchiveFileName)]
        );
        var linkCrypterImportId = await StartImportAsync(seed);
        await ProcessAsync();
        await ChooseDefaultHosterRegistrationsAsync(linkCrypterImportId, seed);

        // Act
        await ProcessAsync();

        // Assert
        var linkCrypterImport = await LoadImportAsync(linkCrypterImportId);
        linkCrypterImport.State.ShouldBe(LinkCrypterImportState.ReadyForReview);
        var containers = linkCrypterImport.Containers.OrderBy(container => container.Id).ToList();
        containers.ShouldAllBe(container =>
            container.Status == LinkCrypterImportContainerStatus.Importable && container.IsSelected
        );
        GetMirrorDecisions(containers[0])
            .ShouldBe([
                (LinkCrypterImportMirrorDecision.Adopt, null),
                (
                    LinkCrypterImportMirrorDecision.UploadByBearcat,
                    LinkCrypterImportUploadByBearcatReason.SomeFilesOffline
                ),
                (LinkCrypterImportMirrorDecision.UnsupportedHoster, null),
                (LinkCrypterImportMirrorDecision.HosterNotInRelease, null),
            ]);
        GetMirrorDecisions(containers[1])
            .ShouldBe([
                (LinkCrypterImportMirrorDecision.Adopt, null),
                (
                    LinkCrypterImportMirrorDecision.UploadByBearcat,
                    LinkCrypterImportUploadByBearcatReason.FileNamesDifferFromFirstAdoptedMirror
                ),
            ]);
        VerifyFileInfosWereNeverRequested(ThirdHosterClassName);
        (await CreateDbContext().Notifications.SingleAsync()).NotificationKind.ShouldBe(
            NotificationKind.LinkCrypterImportReadyForReview
        );
    }

    [Test]
    public async Task ProcessAsync_OnlyFilesThatAreNoArchiveParts_IsNotImportableWithoutReleaseFolder()
    {
        // Arrange
        var seed = await AddReleaseTemplateAsync(ReleaseType.Unmanaged);
        AddListedContainer(
            "https://crypter.test/container",
            ReleaseName,
            [OnlineArchiveLink("https://first.test/a1", "Bearcat.Movie.mkv")]
        );
        var linkCrypterImportId = await StartImportAsync(seed);
        await ProcessAsync();
        await ChooseDefaultHosterRegistrationsAsync(linkCrypterImportId, seed);

        // Act
        await ProcessAsync();

        // Assert
        var container = (await LoadImportAsync(linkCrypterImportId)).Containers.Single();
        container.Status.ShouldBe(LinkCrypterImportContainerStatus.NotImportable);
        container.IsSelected.ShouldBeFalse();
        container.NotImportableReason.ShouldBe(
            LinkCrypterImportNotImportableReason.NoAdoptedMirrorAndNoReleaseFolder
        );
        GetMirrorDecisions(container)
            .ShouldBe([
                (
                    LinkCrypterImportMirrorDecision.UploadByBearcat,
                    LinkCrypterImportUploadByBearcatReason.FileNamesAreNotArchiveParts
                ),
            ]);
    }

    [Test]
    public async Task ProcessAsync_SeasonContainerWithArchiveFolderPerEpisode_CreatesReleasePreviewPerEpisodeInOneCollection()
    {
        // Arrange
        var seed = await AddReleaseTemplateAsync(
            ReleaseType.Unmanaged,
            ReleaseCollectionDetectionMode.SeriesEpisodePattern
        );
        var firstEpisodeArchiveFolderPath = Path.Combine(
            WorkingDirectoryPath,
            FirstEpisodeReleaseName
        );
        var secondEpisodeArchiveFolderPath = Path.Combine(
            WorkingDirectoryPath,
            SecondEpisodeReleaseName
        );

        foreach (var fileName in GetSeasonArchiveFileNames())
        {
            var archiveFolderPath = fileName.StartsWith(
                FirstEpisodeReleaseName,
                StringComparison.Ordinal
            )
                ? firstEpisodeArchiveFolderPath
                : secondEpisodeArchiveFolderPath;

            Directory.CreateDirectory(archiveFolderPath);
            File.WriteAllText(Path.Combine(archiveFolderPath, fileName), fileName);
        }

        AddSeasonContainer();
        var linkCrypterImportId = await StartImportAsync(seed);
        await ProcessAsync();
        await ChooseDefaultHosterRegistrationsAsync(linkCrypterImportId, seed);

        // Act
        await ProcessAsync();

        // Assert
        var container = (await LoadImportAsync(linkCrypterImportId)).Containers.Single();
        container.Status.ShouldBe(LinkCrypterImportContainerStatus.Importable);
        container.IsSelected.ShouldBeTrue();
        var preview = LinkCrypterImportContainerPreview.FromJson(container.PreviewJson);
        preview.ReleaseCollectionName.ShouldBe(SeasonContainerName);
        preview.Mirrors.Count.ShouldBe(2);
        preview.Mirrors.ShouldAllBe(mirror => mirror.Decision == null);
        preview
            .Releases!.Select(releasePreview =>
                (
                    releasePreview.ReleaseName,
                    releasePreview.ReleaseAction,
                    releasePreview.ArchiveFolderPath,
                    releasePreview.NotImportableReason
                )
            )
            .ShouldBe([
                (
                    FirstEpisodeReleaseName,
                    LinkCrypterImportReleaseAction.CreateRelease,
                    firstEpisodeArchiveFolderPath,
                    null
                ),
                (
                    SecondEpisodeReleaseName,
                    LinkCrypterImportReleaseAction.CreateRelease,
                    secondEpisodeArchiveFolderPath,
                    null
                ),
            ]);
        preview
            .Releases!.Last()
            .Mirrors.Select(mirror =>
                (
                    mirror.MirrorNumber,
                    mirror.Decision,
                    string.Join(", ", mirror.Files.Select(file => file.FileName))
                )
            )
            .ShouldBe([
                (
                    1,
                    LinkCrypterImportMirrorDecision.Adopt,
                    $"{SecondEpisodeReleaseName}.part1.rar, {SecondEpisodeReleaseName}.part2.rar"
                ),
                (
                    2,
                    LinkCrypterImportMirrorDecision.Adopt,
                    $"{SecondEpisodeReleaseName}.part1.rar, {SecondEpisodeReleaseName}.part2.rar"
                ),
            ]);
    }

    [Test]
    public async Task ProcessAsync_SeasonContainerWithDisabledCollectionDetection_IsNotImportable()
    {
        // Arrange
        var seed = await AddReleaseTemplateAsync(ReleaseType.Unmanaged);
        AddSeasonContainer();
        var linkCrypterImportId = await StartImportAsync(seed);
        await ProcessAsync();
        await ChooseDefaultHosterRegistrationsAsync(linkCrypterImportId, seed);

        // Act
        await ProcessAsync();

        // Assert
        var container = (await LoadImportAsync(linkCrypterImportId)).Containers.Single();
        container.Status.ShouldBe(LinkCrypterImportContainerStatus.NotImportable);
        container.IsSelected.ShouldBeFalse();
        container.NotImportableReason.ShouldBe(
            LinkCrypterImportNotImportableReason.ReleaseCollectionDetectionDisabled
        );
        var preview = LinkCrypterImportContainerPreview.FromJson(container.PreviewJson);
        preview.ReleaseCollectionName.ShouldBeNull();
        preview
            .Releases!.Select(releasePreview => releasePreview.ReleaseName)
            .ShouldBe([FirstEpisodeReleaseName, SecondEpisodeReleaseName]);
    }

    [Test]
    public async Task ProcessAsync_SeasonContainerWithoutLocalFiles_ImportsEpisodesIntoCollectionWithCollectionContainer()
    {
        // Arrange
        var seed = await AddReleaseTemplateAsync(
            releaseType: ReleaseType.Unmanaged,
            releaseCollectionDetectionMode: ReleaseCollectionDetectionMode.SeriesEpisodePattern,
            firstHosterCollectionUploadSlotKey: SeasonSlotKey,
            secondHosterCollectionUploadSlotKey: SeasonSlotKey
        );

        AddSeasonContainer();

        // Act
        var linkCrypterImportId = await RunWholeImportAsync(seed);

        // Assert
        var container = (await LoadImportAsync(linkCrypterImportId)).Containers.Single();
        container.Status.ShouldBe(LinkCrypterImportContainerStatus.Imported);
        container.ErrorMessages.ShouldBeEmpty();
        container.ImportedReleaseId.ShouldBeNull();
        var releaseCollection = await LoadReleaseCollectionAsync();
        container.ImportedReleaseCollectionId.ShouldBe(releaseCollection.Id);
        releaseCollection.Name.ShouldBe(SeasonContainerName);
        releaseCollection.UploadsPostedAt.ShouldBe(StartTime);

        releaseCollection
            .Releases.Select(release => (release.Name, release.UploadsPostedAt))
            .OrderBy(release => release.Name)
            .ShouldBe([
                (FirstEpisodeReleaseName, StartTime),
                (SecondEpisodeReleaseName, StartTime),
            ]);

        var slot = releaseCollection.UploadSlots.ShouldHaveSingleItem();
        slot.UploadConfigs.Count.ShouldBe(4);

        var linkCrypters = slot
            .UploadConfigs.SelectMany(uploadConfig => uploadConfig.LinkCrypters)
            .ToList();

        linkCrypters.Count.ShouldBe(4);

        linkCrypters.ShouldAllBe(linkCrypter =>
            linkCrypter.ContainerScope == LinkCrypterContainerScope.ReleaseCollection
            && linkCrypter.Password == "template-secret"
        );

        var uploads = slot.UploadConfigs.SelectMany(uploadConfig => uploadConfig.Uploads).ToList();
        uploads.Count.ShouldBe(4);
        uploads.ShouldAllBe(upload => upload.UploadedFiles.Count == 2);

        var linkCrypterContainer = (await LoadLinkCrypterContainersAsync()).ShouldHaveSingleItem();
        linkCrypterContainer.Scope.ShouldBe(LinkCrypterContainerScope.ReleaseCollection);
        linkCrypterContainer.CollectionUploadSlotId.ShouldBe(slot.Id);
        linkCrypterContainer.ContainerUrl.ShouldBe("https://crypter.test/season");
        linkCrypterContainer.Password.ShouldBe("template-secret");

        linkCrypterContainer
            .SourceUploads.Select(source => source.UploadId)
            .Order()
            .ShouldBe(uploads.Select(upload => upload.Id).Order());
    }

    [Test]
    public async Task ProcessAsync_SeasonContainerWithMirrorsInDifferentUploadSlots_FailsWithoutCreatingReleases()
    {
        // Arrange
        var seed = await AddReleaseTemplateAsync(
            releaseType: ReleaseType.Unmanaged,
            releaseCollectionDetectionMode: ReleaseCollectionDetectionMode.SeriesEpisodePattern,
            firstHosterCollectionUploadSlotKey: SeasonSlotKey,
            secondHosterCollectionUploadSlotKey: "other-slot"
        );

        AddSeasonContainer();

        // Act
        var linkCrypterImportId = await RunWholeImportAsync(seed);

        // Assert
        var container = (await LoadImportAsync(linkCrypterImportId)).Containers.Single();
        container.Status.ShouldBe(LinkCrypterImportContainerStatus.ImportFailed);

        container.ErrorMessages.ShouldBe([
            "The mirrors of the container belong to different collection upload slots.",
        ]);

        container.ImportedReleaseCollectionId.ShouldBeNull();
        (await CreateDbContext().Releases.CountAsync()).ShouldBe(0);
        (await CreateDbContext().ReleaseCollections.CountAsync()).ShouldBe(0);
    }

    [Test]
    public async Task ProcessAsync_EpisodeContainerForUploadSlotWithSameLinkCrypterAccount_ImportsUploadsWithoutAdoptingContainer()
    {
        // Arrange
        var seed = await AddReleaseTemplateAsync(
            releaseType: ReleaseType.Unmanaged,
            releaseCollectionDetectionMode: ReleaseCollectionDetectionMode.SeriesEpisodePattern,
            firstHosterCollectionUploadSlotKey: SeasonSlotKey
        );

        AddEpisodeContainer("https://crypter.test/episode", FirstEpisodeReleaseName);

        // Act
        var linkCrypterImportId = await RunWholeImportAsync(seed);

        // Assert
        var container = (await LoadImportAsync(linkCrypterImportId)).Containers.Single();
        container.Status.ShouldBe(LinkCrypterImportContainerStatus.Imported);

        container.ErrorMessages.ShouldBe([
            "The container was not adopted because the release belongs to a release collection whose upload slot uses this link crypter account. Bearcat adds the release to the collection container.",
        ]);

        var release = await LoadReleaseAsync(FirstEpisodeReleaseName);
        container.ImportedReleaseId.ShouldBe(release.Id);

        release
            .UploadConfigs.SelectMany(uploadConfig => uploadConfig.Uploads)
            .ShouldHaveSingleItem()
            .UploadedFiles.Count.ShouldBe(2);

        release
            .UploadConfigs.SelectMany(uploadConfig => uploadConfig.LinkCrypters)
            .ShouldHaveSingleItem()
            .ContainerScope.ShouldBe(LinkCrypterContainerScope.ReleaseCollection);

        (await LoadLinkCrypterContainersAsync()).ShouldBeEmpty();
    }

    [Test]
    public async Task ProcessAsync_SeasonContainerForEpisodeWithReleaseScopedLinkCrypterOfSameAccount_Fails()
    {
        // Arrange
        var seed = await AddReleaseTemplateAsync(
            releaseType: ReleaseType.Unmanaged,
            releaseCollectionDetectionMode: ReleaseCollectionDetectionMode.SeriesEpisodePattern,
            firstHosterCollectionUploadSlotKey: SeasonSlotKey,
            secondHosterCollectionUploadSlotKey: SeasonSlotKey
        );

        AddEpisodeContainer("https://crypter.test/episode", FirstEpisodeReleaseName);
        await RunWholeImportAsync(seed);

        await using (var dbContext = CreateDbContext())
        {
            foreach (var linkCrypter in await dbContext.UploadConfigLinkCrypters.ToListAsync())
            {
                linkCrypter.ContainerScope = LinkCrypterContainerScope.Release;
            }

            await dbContext.SaveChangesAsync();
        }

        AddSeasonContainer();

        // Act
        var linkCrypterImportId = await RunWholeImportAsync(seed);

        // Assert
        var seasonContainer = (await LoadImportAsync(linkCrypterImportId)).Containers.Single(
            container => container.ContainerUrl == "https://crypter.test/season"
        );

        seasonContainer.Status.ShouldBe(LinkCrypterImportContainerStatus.ImportFailed);
        seasonContainer.ErrorMessages.ShouldHaveSingleItem().ShouldContain("release containers");
        (await CreateDbContext().Releases.CountAsync()).ShouldBe(1);

        (await LoadLinkCrypterContainersAsync()).ShouldNotContain(linkCrypterContainer =>
            linkCrypterContainer.Scope == LinkCrypterContainerScope.ReleaseCollection
        );
    }

    [Test]
    public async Task ProcessAsync_TwoEpisodeContainersOfOneCollection_ImportsBothIntoTheSameCollection()
    {
        // Arrange
        var seed = await AddReleaseTemplateAsync(
            releaseType: ReleaseType.Unmanaged,
            releaseCollectionDetectionMode: ReleaseCollectionDetectionMode.SeriesEpisodePattern,
            firstHosterCollectionUploadSlotKey: SeasonSlotKey
        );

        AddEpisodeContainer("https://crypter.test/first-episode", FirstEpisodeReleaseName);
        AddEpisodeContainer("https://crypter.test/second-episode", SecondEpisodeReleaseName);

        // Act
        var linkCrypterImportId = await RunWholeImportAsync(seed);

        // Assert
        (await LoadImportAsync(linkCrypterImportId))
            .Containers.Select(container => container.Status)
            .ShouldBe([
                LinkCrypterImportContainerStatus.Imported,
                LinkCrypterImportContainerStatus.Imported,
            ]);

        var releaseCollection = await LoadReleaseCollectionAsync();

        releaseCollection
            .Releases.Select(release => release.Name)
            .Order()
            .ShouldBe([FirstEpisodeReleaseName, SecondEpisodeReleaseName]);

        releaseCollection.UploadSlots.ShouldHaveSingleItem().UploadConfigs.Count.ShouldBe(2);
    }

    [Test]
    public async Task ProcessAsync_TwoContainersWithSameNameForDifferentHosters_ImportsBothIntoOneRelease()
    {
        // Arrange
        var seed = await AddReleaseTemplateAsync(ReleaseType.Unmanaged);
        AddListedContainer(
            "https://crypter.test/first",
            ReleaseName,
            [
                OnlineArchiveLink("https://first.test/a1", FirstArchiveFileName),
                OnlineArchiveLink("https://first.test/a2", SecondArchiveFileName),
            ]
        );
        AddListedContainer(
            "https://crypter.test/second",
            $" {ReleaseName.ToLowerInvariant()} ",
            [
                OnlineArchiveLink("https://second.test/b1", FirstArchiveFileName),
                OnlineArchiveLink("https://second.test/b2", SecondArchiveFileName),
            ]
        );

        // Act
        var linkCrypterImportId = await RunWholeImportAsync(seed);

        // Assert
        var release = await LoadReleaseAsync(ReleaseName);
        var containers = (await LoadImportAsync(linkCrypterImportId))
            .Containers.OrderBy(container => container.Id)
            .ToList();
        containers
            .Select(container =>
                (
                    container.Status,
                    container.ImportedReleaseId,
                    GetReleasePreview(container).ReleaseAction
                )
            )
            .ShouldBe([
                (
                    LinkCrypterImportContainerStatus.Imported,
                    release.Id,
                    LinkCrypterImportReleaseAction.CreateRelease
                ),
                (
                    LinkCrypterImportContainerStatus.Imported,
                    release.Id,
                    LinkCrypterImportReleaseAction.AttachToReleaseOfEarlierContainer
                ),
            ]);
        var archive = release.ArchiveConfigs.ShouldHaveSingleItem().Archives.ShouldHaveSingleItem();
        release.UploadConfigs.Count.ShouldBe(2);
        foreach (var uploadConfig in release.UploadConfigs)
        {
            var upload = uploadConfig.Uploads.ShouldHaveSingleItem();
            upload.ArchiveId.ShouldBe(archive.Id);
            var linkCrypter = uploadConfig.LinkCrypters.ShouldHaveSingleItem();
            linkCrypter.ContainerScope.ShouldBe(LinkCrypterContainerScope.Release);
            linkCrypter.LinkCrypterContainers.ShouldHaveSingleItem().UploadId.ShouldBe(upload.Id);
        }
        release.LinkCrypterContainers.ShouldBeEmpty();
    }

    [Test]
    public async Task ProcessAsync_ContainerOfSecondCrypterForExistingRelease_LinksUploadWithSameLinksAndAddsOtherHosterWithoutSourceUpload()
    {
        // Arrange
        var seed = await AddReleaseTemplateAsync(ReleaseType.Unmanaged);
        AddListedContainer(
            "https://crypter.test/first",
            ReleaseName,
            [
                OnlineArchiveLink("https://first.test/a1", FirstArchiveFileName),
                OnlineArchiveLink("https://first.test/a2", SecondArchiveFileName),
            ],
            [
                OnlineArchiveLink("https://second.test/b1", FirstArchiveFileName),
                OnlineArchiveLink("https://second.test/b2", SecondArchiveFileName),
            ]
        );
        await RunWholeImportAsync(seed);
        var secondLinkCrypterRegistrationId = await AddLinkCrypterRegistrationAsync();
        ListedContainers.Clear();
        AddListedContainer(
            "https://crypter.test/second-crypter",
            ReleaseName,
            ["https://first.test/a1", "https://first.test/a2"],
            [
                OnlineArchiveLink("https://second.test/c1", FirstArchiveFileName),
                OnlineArchiveLink("https://second.test/c2", SecondArchiveFileName),
            ]
        );

        // Act
        var linkCrypterImportId = await RunWholeImportAsync(seed, secondLinkCrypterRegistrationId);

        // Assert
        var release = await LoadReleaseAsync(ReleaseName);
        var importContainer = (await LoadImportAsync(linkCrypterImportId)).Containers.Single();
        importContainer.Status.ShouldBe(LinkCrypterImportContainerStatus.Imported);
        importContainer.ImportedReleaseId.ShouldBe(release.Id);
        GetReleasePreview(importContainer)
            .ReleaseAction.ShouldBe(LinkCrypterImportReleaseAction.AttachToExistingRelease);
        var firstUpload = release
            .UploadConfigs.Single(uploadConfig =>
                uploadConfig.HosterRegistrationId == seed.FirstHosterRegistrationId
            )
            .Uploads.ShouldHaveSingleItem();
        release.UploadConfigs.SelectMany(uploadConfig => uploadConfig.Uploads).Count().ShouldBe(2);
        release
            .UploadConfigs.SelectMany(uploadConfig => uploadConfig.LinkCrypters)
            .Where(linkCrypter =>
                linkCrypter.LinkCrypterRegistrationId == secondLinkCrypterRegistrationId
            )
            .Select(linkCrypter => linkCrypter.ContainerScope)
            .ShouldBe([
                LinkCrypterContainerScope.ReleaseAllHosters,
                LinkCrypterContainerScope.ReleaseAllHosters,
            ]);
        var container = release.LinkCrypterContainers.Single(container =>
            container.LinkCrypterRegistrationId == secondLinkCrypterRegistrationId
        );
        container.ContainerUrl.ShouldBe("https://crypter.test/second-crypter");
        container.SourceUploads.Select(source => source.UploadId).ShouldBe([firstUpload.Id]);
    }

    [Test]
    public async Task ProcessAsync_ReleaseHasContainerOfSameCrypterForHoster_FailsThatContainerAndImportsTheOthers()
    {
        // Arrange
        var seed = await AddReleaseTemplateAsync(ReleaseType.Unmanaged);
        AddListedContainer(
            "https://crypter.test/first",
            ReleaseName,
            [OnlineArchiveLink("https://first.test/a1", FirstArchiveFileName)]
        );
        await RunWholeImportAsync(seed);
        ListedContainers.Clear();
        AddListedContainer(
            "https://crypter.test/again",
            ReleaseName,
            [OnlineArchiveLink("https://first.test/b1", FirstArchiveFileName)]
        );
        AddListedContainer(
            "https://crypter.test/other",
            OtherReleaseName,
            [OnlineArchiveLink("https://second.test/c1", FirstArchiveFileName)]
        );

        // Act
        var linkCrypterImportId = await RunWholeImportAsync(seed);

        // Assert
        var containers = (await LoadImportAsync(linkCrypterImportId))
            .Containers.OrderBy(container => container.Id)
            .ToList();
        containers
            .Select(container => container.Status)
            .ShouldBe([
                LinkCrypterImportContainerStatus.ImportFailed,
                LinkCrypterImportContainerStatus.Imported,
            ]);
        containers[0].ErrorMessages.ShouldHaveSingleItem().ShouldContain(ReleaseName);
        var release = await LoadReleaseAsync(ReleaseName);
        release
            .UploadConfigs.SelectMany(uploadConfig => uploadConfig.Uploads)
            .ShouldHaveSingleItem()
            .UploadedFiles.ShouldHaveSingleItem()
            .HosterFileLink.ShouldBe("https://first.test/a1");
        release
            .UploadConfigs.SelectMany(uploadConfig => uploadConfig.LinkCrypters)
            .SelectMany(linkCrypter => linkCrypter.LinkCrypterContainers)
            .ShouldHaveSingleItem()
            .ContainerUrl.ShouldBe("https://crypter.test/first");
    }

    [Test]
    public async Task ProcessAsync_SingleHosterContainerWithReleaseFolder_ImportsManagedReleaseWithReleaseContainer()
    {
        // Arrange
        var seed = await AddReleaseTemplateAsync(ReleaseType.Managed);
        var releaseFolderPath = Directory
            .CreateDirectory(Path.Combine(WorkingDirectoryPath, ReleaseName))
            .FullName;
        AddListedContainer(
            "https://crypter.test/container",
            ReleaseName,
            [
                OnlineArchiveLink("https://first.test/a1", FirstArchiveFileName),
                OnlineArchiveLink("https://first.test/a2", SecondArchiveFileName),
            ]
        );

        // Act
        var linkCrypterImportId = await RunWholeImportAsync(
            seed: seed,
            primaryLanguageCode: " DE "
        );

        // Assert
        var linkCrypterImport = await LoadImportAsync(linkCrypterImportId);
        linkCrypterImport.State.ShouldBe(LinkCrypterImportState.Completed);
        linkCrypterImport
            .Containers.Single()
            .Status.ShouldBe(LinkCrypterImportContainerStatus.Imported);

        var release = await LoadReleaseAsync(ReleaseName);
        release.ReleaseType.ShouldBe(ReleaseType.Managed);
        release.ReleaseFolderPath.ShouldBe(releaseFolderPath);
        release.PrimaryLanguageCode.ShouldBe("de");
        release.UploadsPostedAt.ShouldBe(StartTime);
        var archive = release.ArchiveConfigs.ShouldHaveSingleItem().Archives.ShouldHaveSingleItem();
        archive.ArchiveState.ShouldBe(ArchiveState.Deleted);

        var firstUploadConfig = release.UploadConfigs.Single(uploadConfig =>
            uploadConfig.HosterRegistrationId == seed.FirstHosterRegistrationId
        );
        var upload = firstUploadConfig.Uploads.ShouldHaveSingleItem();
        upload.UploadState.ShouldBe(UploadState.Completed);
        upload.OnlineState.ShouldBe(OnlineState.Online);
        upload.UploadedFiles.ShouldAllBe(file => file.OnlineState == OnlineState.Online);
        upload.UploadedAt.ShouldBe(StartTime);
        upload.HosterRegistrationId.ShouldBe(seed.FirstHosterRegistrationId);
        upload.ArchiveId.ShouldBe(archive.Id);
        upload
            .UploadedFiles.OrderBy(file => file.HosterFileLink)
            .Select(file => (file.HosterFileLink, Path.GetFileName(file.ArchiveFile!.FullFileName)))
            .ShouldBe([
                ("https://first.test/a1", FirstArchiveFileName),
                ("https://first.test/a2", SecondArchiveFileName),
            ]);
        release
            .UploadConfigs.Single(uploadConfig =>
                uploadConfig.HosterRegistrationId == seed.SecondHosterRegistrationId
            )
            .Uploads.ShouldBeEmpty();

        var linkCrypter = firstUploadConfig.LinkCrypters.ShouldHaveSingleItem();
        linkCrypter.ContainerScope.ShouldBe(LinkCrypterContainerScope.Release);
        var container = linkCrypter.LinkCrypterContainers.ShouldHaveSingleItem();
        container.Scope.ShouldBe(LinkCrypterContainerScope.Release);
        container.UploadId.ShouldBe(upload.Id);
        container.ContainerUrl.ShouldBe("https://crypter.test/container");
        container.ExternalReference.ShouldBe("reference-https://crypter.test/container");
        container.State.ShouldBe(LinkCrypterContainerState.Created);
        container.Password.ShouldBe("template-secret");
    }

    [Test]
    public async Task ProcessAsync_MultiHosterContainerWithoutReleaseFolder_ImportsUnmanagedReleaseWithDeletedArchiveAndAllHostersContainer()
    {
        // Arrange
        var seed = await AddReleaseTemplateAsync(ReleaseType.Unmanaged);
        await AddThirdHosterUploadConfigTemplateAsync(seed);
        AddListedContainer(
            "https://crypter.test/container",
            ReleaseName,
            [
                OnlineArchiveLink("https://first.test/a1", FirstArchiveFileName),
                OnlineArchiveLink("https://first.test/a2", SecondArchiveFileName),
            ],
            [
                OnlineArchiveLink("https://second.test/b1", FirstArchiveFileName),
                OnlineArchiveLink("https://second.test/b2", SecondArchiveFileName),
            ]
        );

        // Act
        await RunWholeImportAsync(seed);

        // Assert
        var release = await LoadReleaseAsync(ReleaseName);
        release.ReleaseType.ShouldBe(ReleaseType.Unmanaged);
        release.ReleaseFolderPath.ShouldBeNull();
        release.ReleaseInfoCheckedAt.ShouldBeNull();
        var archiveConfig = release.ArchiveConfigs.ShouldHaveSingleItem();
        archiveConfig.ArchiveFilesBasePath.ShouldBe(ArchiveFilesBasePath);
        var archive = archiveConfig.Archives.ShouldHaveSingleItem();
        archive.ArchiveState.ShouldBe(ArchiveState.Deleted);
        archive
            .ArchiveFiles.Select(file => Path.GetFileName(file.FullFileName))
            .Order()
            .ShouldBe([FirstArchiveFileName, SecondArchiveFileName]);

        var uploads = release
            .UploadConfigs.SelectMany(uploadConfig => uploadConfig.Uploads)
            .ToList();
        uploads.Count.ShouldBe(2);
        uploads.ShouldAllBe(upload => upload.ArchiveId == archive.Id);
        release
            .UploadConfigs.SelectMany(uploadConfig => uploadConfig.LinkCrypters)
            .Select(linkCrypter => (linkCrypter.ContainerScope, linkCrypter.Password))
            .ShouldBe(
                [
                    (LinkCrypterContainerScope.ReleaseAllHosters, "template-secret"),
                    (LinkCrypterContainerScope.ReleaseAllHosters, null),
                    (LinkCrypterContainerScope.ReleaseAllHosters, null),
                ],
                ignoreOrder: true
            );
        var container = release.LinkCrypterContainers.ShouldHaveSingleItem();
        container.Scope.ShouldBe(LinkCrypterContainerScope.ReleaseAllHosters);
        container.ContainerUrl.ShouldBe("https://crypter.test/container");
        container.Password.ShouldBe("template-secret");
        container
            .SourceUploads.Select(source => source.UploadId)
            .Order()
            .ShouldBe(uploads.Select(upload => upload.Id).Order());
    }

    [Test]
    public async Task ProcessAsync_ArchivePartsInSubfolderOfArchiveStorageFolder_ImportsUnmanagedReleaseWithThisArchive()
    {
        // Arrange
        var seed = await AddReleaseTemplateAsync(ReleaseType.Unmanaged);
        var archiveStorageFolderPath = Directory
            .CreateDirectory(Path.Combine(TempRootPath, "storage"))
            .FullName;
        var archiveFolderPath = CreateArchivePartFiles(
            Path.Combine(archiveStorageFolderPath, ReleaseName)
        );
        var archiveStorageFolderId = await AddArchiveStorageFolderAsync(archiveStorageFolderPath);
        AddListedContainer(
            "https://crypter.test/container",
            ReleaseName,
            [
                OnlineArchiveLink("https://first.test/a1", FirstArchiveFileName),
                OnlineArchiveLink("https://first.test/a2", SecondArchiveFileName),
            ]
        );

        // Act
        var linkCrypterImportId = await RunWholeImportAsync(seed);

        // Assert
        var container = (await LoadImportAsync(linkCrypterImportId)).Containers.Single();
        container.Status.ShouldBe(LinkCrypterImportContainerStatus.Imported);
        GetReleasePreview(container).ArchiveFolderPath.ShouldBe(archiveFolderPath);

        var release = await LoadReleaseAsync(ReleaseName);
        release.ReleaseType.ShouldBe(ReleaseType.Unmanaged);
        var archiveConfig = release.ArchiveConfigs.ShouldHaveSingleItem();
        archiveConfig.ArchiveFilesBasePath.ShouldBe(archiveFolderPath);
        var archive = archiveConfig.Archives.ShouldHaveSingleItem();
        archive.ArchiveState.ShouldBe(ArchiveState.Created);
        archive.ArchiveFolderPath.ShouldBe(archiveFolderPath);
        archive.ArchiveStorageFolderId.ShouldBe(archiveStorageFolderId);

        var upload = release
            .UploadConfigs.SelectMany(uploadConfig => uploadConfig.Uploads)
            .Single();
        upload.ArchiveId.ShouldBe(archive.Id);
        upload
            .UploadedFiles.OrderBy(file => file.HosterFileLink)
            .Select(file => (file.HosterFileLink, file.ArchiveFile!.FullFileName))
            .ShouldBe([
                ("https://first.test/a1", Path.Combine(archiveFolderPath, FirstArchiveFileName)),
                ("https://first.test/a2", Path.Combine(archiveFolderPath, SecondArchiveFileName)),
            ]);
    }

    [Test]
    public async Task ProcessAsync_ManagedReleaseWithArchivePartsInSubfolderOfArchiveStorageFolder_AssignsArchiveToThisStorageFolder()
    {
        // Arrange
        var seed = await AddReleaseTemplateAsync(ReleaseType.Managed);
        var releaseFolderPath = Directory
            .CreateDirectory(Path.Combine(WorkingDirectoryPath, ReleaseName))
            .FullName;
        var archiveStorageFolderPath = Directory
            .CreateDirectory(Path.Combine(TempRootPath, "storage"))
            .FullName;
        var archiveFolderPath = CreateArchivePartFiles(
            Path.Combine(archiveStorageFolderPath, "Archives")
        );
        await AddArchiveStorageFolderAsync(Path.Combine(TempRootPath, "other-storage"));
        var archiveStorageFolderId = await AddArchiveStorageFolderAsync(archiveStorageFolderPath);
        AddListedContainer(
            "https://crypter.test/container",
            ReleaseName,
            [
                OnlineArchiveLink("https://first.test/a1", FirstArchiveFileName),
                OnlineArchiveLink("https://first.test/a2", SecondArchiveFileName),
            ]
        );

        // Act
        await RunWholeImportAsync(seed);

        // Assert
        var release = await LoadReleaseAsync(ReleaseName);
        release.ReleaseType.ShouldBe(ReleaseType.Managed);
        release.ReleaseFolderPath.ShouldBe(releaseFolderPath);
        var archive = release.ArchiveConfigs.ShouldHaveSingleItem().Archives.ShouldHaveSingleItem();
        archive.ArchiveState.ShouldBe(ArchiveState.Created);
        archive.ArchiveFolderPath.ShouldBe(archiveFolderPath);
        archive.ArchiveStorageFolderId.ShouldBe(archiveStorageFolderId);
    }

    [Test]
    public async Task ProcessAsync_TwoReleasesWithSameArchiveFileNames_ChoosesDifferentArchiveFolders()
    {
        // Arrange
        var seed = await AddReleaseTemplateAsync(ReleaseType.Unmanaged);
        var firstArchiveFolderPath = CreateArchivePartFiles(
            Path.Combine(WorkingDirectoryPath, "A-first")
        );
        var secondArchiveFolderPath = CreateArchivePartFiles(
            Path.Combine(WorkingDirectoryPath, "B-second")
        );
        AddListedContainer(
            "https://crypter.test/first",
            ReleaseName,
            [
                OnlineArchiveLink("https://first.test/a1", FirstArchiveFileName),
                OnlineArchiveLink("https://first.test/a2", SecondArchiveFileName),
            ]
        );
        AddListedContainer(
            "https://crypter.test/second",
            OtherReleaseName,
            [
                OnlineArchiveLink("https://first.test/b1", FirstArchiveFileName),
                OnlineArchiveLink("https://first.test/b2", SecondArchiveFileName),
            ]
        );

        // Act
        await RunWholeImportAsync(seed);

        // Assert
        (await LoadReleaseAsync(ReleaseName))
            .ArchiveConfigs.ShouldHaveSingleItem()
            .Archives.ShouldHaveSingleItem()
            .ArchiveFolderPath.ShouldBe(firstArchiveFolderPath);
        (await LoadReleaseAsync(OtherReleaseName))
            .ArchiveConfigs.ShouldHaveSingleItem()
            .Archives.ShouldHaveSingleItem()
            .ArchiveFolderPath.ShouldBe(secondArchiveFolderPath);
    }

    [Test]
    public async Task ProcessAsync_ArchiveFolderUsedByOtherRelease_ChoosesNextFolderWithSameArchiveParts()
    {
        // Arrange
        var seed = await AddReleaseTemplateAsync(ReleaseType.Unmanaged);
        var usedArchiveFolderPath = CreateArchivePartFiles(
            Path.Combine(WorkingDirectoryPath, "A-used")
        );
        var unusedArchiveFolderPath = CreateArchivePartFiles(
            Path.Combine(WorkingDirectoryPath, "B-unused")
        );
        var dbContext = CreateDbContext();
        dbContext.Releases.Add(
            new Release
            {
                Name = OtherReleaseName,
                ReleaseType = ReleaseType.Managed,
                ReleaseFolderPath = usedArchiveFolderPath,
                ReleaseGroupId = seed.ReleaseGroupId,
                CreatedAt = StartTime,
                UploadConfigs = [],
                ArchiveConfigs = [],
            }
        );
        await dbContext.SaveChangesAsync();
        AddListedContainer(
            "https://crypter.test/container",
            ReleaseName,
            [
                OnlineArchiveLink("https://first.test/a1", FirstArchiveFileName),
                OnlineArchiveLink("https://first.test/a2", SecondArchiveFileName),
            ]
        );
        var linkCrypterImportId = await StartImportAsync(seed);
        await ProcessAsync();
        await ChooseDefaultHosterRegistrationsAsync(linkCrypterImportId, seed);

        // Act
        await ProcessAsync();

        // Assert
        var container = (await LoadImportAsync(linkCrypterImportId)).Containers.Single();
        container.Status.ShouldBe(LinkCrypterImportContainerStatus.Importable);
        GetReleasePreview(container).ArchiveFolderPath.ShouldBe(unusedArchiveFolderPath);
    }

    [Test]
    public async Task ProcessAsync_SomeContainersFailToImport_ImportsTheOthersAndNotifiesFailure()
    {
        // Arrange
        var seed = await AddReleaseTemplateAsync(ReleaseType.Unmanaged);
        AddListedContainer(
            "https://crypter.test/missing-account",
            ReleaseName,
            [OnlineArchiveLink("https://first.test/a1", FirstArchiveFileName)]
        );
        AddListedContainer(
            "https://crypter.test/release-created-meanwhile",
            OtherReleaseName,
            [OnlineArchiveLink("https://second.test/b1", FirstArchiveFileName)]
        );
        AddListedContainer(
            "https://crypter.test/importable",
            ThirdReleaseName,
            [OnlineArchiveLink("https://second.test/c1", FirstArchiveFileName)]
        );
        var linkCrypterImportId = await StartImportAsync(seed);
        await ProcessAsync();
        await ChooseDefaultHosterRegistrationsAsync(linkCrypterImportId, seed);
        await ProcessAsync();
        await RemoveChosenHosterRegistrationAsync(linkCrypterImportId, FirstHosterClassName);
        await AddReleaseAsync(seed, OtherReleaseName);
        await CreateLinkCrypterImportService(CreateDbContext())
            .StartImportAsync(linkCrypterImportId);

        // Act
        await ProcessAsync();

        // Assert
        var linkCrypterImport = await LoadImportAsync(linkCrypterImportId);
        linkCrypterImport.State.ShouldBe(LinkCrypterImportState.Completed);
        linkCrypterImport.ProcessedContainerCount.ShouldBe(3);
        var containers = linkCrypterImport.Containers.OrderBy(container => container.Id).ToList();
        containers
            .Select(container => container.Status)
            .ShouldBe([
                LinkCrypterImportContainerStatus.ImportFailed,
                LinkCrypterImportContainerStatus.ImportFailed,
                LinkCrypterImportContainerStatus.Imported,
            ]);
        containers[0].ErrorMessages.ShouldHaveSingleItem();
        containers[1]
            .ErrorMessages.ShouldBe([
                $"No mirror of the container belongs to a hoster of release {OtherReleaseName}.",
            ]);
        containers[2].ImportedReleaseId.ShouldBe((await LoadReleaseAsync(ThirdReleaseName)).Id);
        (
            await CreateDbContext().Releases.CountAsync(release => release.Name == ReleaseName)
        ).ShouldBe(0);
        (
            await CreateDbContext()
                .Notifications.OrderByDescending(notification => notification.Id)
                .FirstAsync()
        ).NotificationKind.ShouldBe(NotificationKind.LinkCrypterImportFailed);
    }

    [Test]
    public async Task RenameReleaseAsync_ToReleaseNameOfOtherContainer_ImportsBothContainersIntoOneRelease()
    {
        // Arrange
        var seed = await AddReleaseTemplateAsync(ReleaseType.Unmanaged);
        AddListedContainer(
            "https://crypter.test/first",
            ReleaseName,
            [
                OnlineArchiveLink("https://first.test/a1", FirstArchiveFileName),
                OnlineArchiveLink("https://first.test/a2", SecondArchiveFileName),
            ]
        );
        AddListedContainer(
            "https://crypter.test/second",
            "Bearcat Movie 2026 Second Hoster",
            [
                OnlineArchiveLink("https://second.test/b1", FirstArchiveFileName),
                OnlineArchiveLink("https://second.test/b2", SecondArchiveFileName),
            ]
        );
        var linkCrypterImportId = await StartImportAsync(seed);
        await ProcessAsync();
        await ChooseDefaultHosterRegistrationsAsync(linkCrypterImportId, seed);
        await ProcessAsync();

        // Act
        await CreateLinkCrypterImportService(CreateDbContext())
            .RenameReleaseAsync(
                linkCrypterImportId: linkCrypterImportId,
                currentReleaseName: "bearcat movie 2026 second hoster",
                newReleaseName: $" {ReleaseName} "
            );
        await CreateLinkCrypterImportService(CreateDbContext())
            .StartImportAsync(linkCrypterImportId);
        await ProcessAsync();

        // Assert
        var release = await LoadReleaseAsync(ReleaseName);
        var containers = (await LoadImportAsync(linkCrypterImportId))
            .Containers.OrderBy(container => container.Id)
            .ToList();
        containers
            .Select(container =>
                (
                    container.ReleaseName,
                    container.Status,
                    container.ImportedReleaseId,
                    GetReleasePreview(container).ReleaseAction
                )
            )
            .ShouldBe([
                (
                    ReleaseName,
                    LinkCrypterImportContainerStatus.Imported,
                    release.Id,
                    LinkCrypterImportReleaseAction.CreateRelease
                ),
                (
                    ReleaseName,
                    LinkCrypterImportContainerStatus.Imported,
                    release.Id,
                    LinkCrypterImportReleaseAction.AttachToReleaseOfEarlierContainer
                ),
            ]);
        release.UploadConfigs.SelectMany(uploadConfig => uploadConfig.Uploads).Count().ShouldBe(2);
    }

    [Test]
    public async Task RenameReleaseAsync_ToNameOfExistingRelease_AttachesContainerToExistingRelease()
    {
        // Arrange
        var seed = await AddReleaseTemplateAsync(ReleaseType.Unmanaged);
        AddListedContainer(
            "https://crypter.test/first",
            ReleaseName,
            [
                OnlineArchiveLink("https://first.test/a1", FirstArchiveFileName),
                OnlineArchiveLink("https://first.test/a2", SecondArchiveFileName),
            ]
        );
        await RunWholeImportAsync(seed);
        var release = await LoadReleaseAsync(ReleaseName);
        ListedContainers.Clear();
        AddListedContainer(
            "https://crypter.test/second",
            "Wrong title",
            [
                OnlineArchiveLink("https://second.test/b1", FirstArchiveFileName),
                OnlineArchiveLink("https://second.test/b2", SecondArchiveFileName),
            ]
        );
        var linkCrypterImportId = await StartImportAsync(seed);
        await ProcessAsync();
        await ChooseDefaultHosterRegistrationsAsync(linkCrypterImportId, seed);
        await ProcessAsync();

        // Act
        await CreateLinkCrypterImportService(CreateDbContext())
            .RenameReleaseAsync(
                linkCrypterImportId: linkCrypterImportId,
                currentReleaseName: "Wrong title",
                newReleaseName: ReleaseName
            );

        // Assert
        var container = (await LoadImportAsync(linkCrypterImportId)).Containers.Single();
        container.ReleaseName.ShouldBe(ReleaseName);
        container.Status.ShouldBe(LinkCrypterImportContainerStatus.Importable);
        container.IsSelected.ShouldBeTrue();
        var preview = GetReleasePreview(container);
        preview.ReleaseAction.ShouldBe(LinkCrypterImportReleaseAction.AttachToExistingRelease);
        preview.ExistingReleaseId.ShouldBe(release.Id);
    }

    [Test]
    public async Task SetReleaseSelectionAsync_Select_SelectsOnlyImportableContainersOfRelease()
    {
        // Arrange
        var seed = await AddReleaseTemplateAsync(ReleaseType.Unmanaged);
        AddListedContainer(
            "https://crypter.test/first",
            ReleaseName,
            [OnlineArchiveLink("https://first.test/a1", FirstArchiveFileName)]
        );
        AddListedContainer(
            "https://crypter.test/second",
            ReleaseName.ToLowerInvariant(),
            [OnlineArchiveLink("https://second.test/b1", FirstArchiveFileName)]
        );
        AddListedContainer(
            "https://crypter.test/unknown",
            ReleaseName,
            ["https://unknown.test/c1"]
        );
        AddListedContainer(
            "https://crypter.test/other",
            OtherReleaseName,
            [OnlineArchiveLink("https://first.test/d1", FirstArchiveFileName)]
        );
        var linkCrypterImportId = await StartImportAsync(seed);
        await ProcessAsync();
        await ChooseDefaultHosterRegistrationsAsync(linkCrypterImportId, seed);
        await ProcessAsync();
        await CreateLinkCrypterImportService(CreateDbContext())
            .SetSelectionOfAllImportableContainersAsync(linkCrypterImportId, isSelected: false);

        // Act
        await CreateLinkCrypterImportService(CreateDbContext())
            .SetReleaseSelectionAsync(linkCrypterImportId, ReleaseName, isSelected: true);

        // Assert
        (await LoadImportAsync(linkCrypterImportId))
            .Containers.OrderBy(container => container.Id)
            .Select(container => (container.Status, container.IsSelected))
            .ShouldBe([
                (LinkCrypterImportContainerStatus.Importable, true),
                (LinkCrypterImportContainerStatus.Importable, true),
                (LinkCrypterImportContainerStatus.NotImportable, false),
                (LinkCrypterImportContainerStatus.Importable, false),
            ]);
    }
}
