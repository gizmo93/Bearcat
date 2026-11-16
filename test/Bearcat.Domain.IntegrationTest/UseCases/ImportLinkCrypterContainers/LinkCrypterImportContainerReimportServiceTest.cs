using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Exceptions;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Importing;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Preview;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Reimporting;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
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

    private async Task<SeasonImport> ImportSeasonContainerAsOneReleaseAsync()
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
        var containerId = await ReplacePreviewWithOneSeasonReleaseAsync(linkCrypterImportId);

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

    private async Task<int> ReplacePreviewWithOneSeasonReleaseAsync(int linkCrypterImportId)
    {
        var dbContext = CreateDbContext();

        var container = await dbContext.LinkCrypterImportContainers.SingleAsync(container =>
            container.LinkCrypterImportId == linkCrypterImportId
        );

        var preview = LinkCrypterImportContainerPreview.FromJson(container.PreviewJson);

        container.PreviewJson = (
            preview with
            {
                ReleaseCollectionName = null,
                Releases =
                [
                    new LinkCrypterImportReleasePreview(
                        ReleaseName: SeasonContainerName,
                        Mirrors: preview
                            .Mirrors.Select(mirror =>
                                mirror with
                                {
                                    Decision = LinkCrypterImportMirrorDecision.Adopt,
                                }
                            )
                            .ToList(),
                        ReleaseFolderPath: null,
                        ArchiveFolderPath: null,
                        ReleaseAction: LinkCrypterImportReleaseAction.CreateRelease,
                        ExistingReleaseId: null,
                        NotImportableReason: null
                    ),
                ],
            }
        ).ToJson();

        await dbContext.SaveChangesAsync();

        return container.Id;
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
        var dbContext = CreateDbContext();
        var timeProvider = new ControllableTimeProvider(StartTime);
        var repository = new LinkCrypterImportRepository(dbContext);
        var releaseCollectionAssigner = CreateReleaseCollectionAssigner(dbContext, timeProvider);

        return new LinkCrypterImportContainerReimportService(
            repository,
            new ReleaseWriteRepository(dbContext),
            CreatePreviewService(dbContext, timeProvider),
            new CollectionContainerImportService(
                repository,
                CreateReleaseImportService(
                    dbContext: dbContext,
                    timeProvider: timeProvider,
                    releaseCollectionAssigner: releaseCollectionAssigner
                ),
                timeProvider,
                NullLogger<CollectionContainerImportService>.Instance
            ),
            releaseCollectionAssigner,
            NullLogger<LinkCrypterImportContainerReimportService>.Instance
        );
    }

    private sealed record SeasonImport(TemplateSeed Seed, int ContainerId, int SeasonReleaseId);

    private sealed record SecondCrypterImport(int LinkCrypterRegistrationId, int ContainerId);
}
