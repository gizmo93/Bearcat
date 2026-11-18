using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.RunUploadCompletionActions.Models;
using Bearcat.Domain.UseCases.RunUploadCompletionActions.ReadModels;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Shouldly;

namespace Bearcat.Infrastructure.IntegrationTest.Database.Repositories;

public class UploadCompletionActionRepositoryTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private static readonly DateTime RegistrationCreatedAt = new(
        2026,
        11,
        1,
        12,
        0,
        0,
        DateTimeKind.Utc
    );

    private UploadCompletionActionRepository repository = null!;

    [SetUp]
    public void Setup()
    {
        repository = new UploadCompletionActionRepository(
            dbRead: ReadDbContext,
            dbWrite: CreateDbContext()
        );
    }

    [Test]
    public async Task GetDueReleasesAsync_InitialUploadCompleted_ReturnsReleaseAsInitialUpload()
    {
        // Arrange
        var registration = AddRegistration();
        var hoster = CreateHosterRegistration("Alpha");
        var release = AddRelease("Bearcat.Release.001");
        AddUpload(AddUploadConfig(release, hoster), RegistrationCreatedAt.AddHours(1));
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetDueReleasesAsync(registration, CancellationToken.None);

        // Assert
        result.ShouldBe([
            new UploadCompletionActionDueRelease(
                ReleaseId: release.Id,
                IsReupload: false,
                NewestUploadedAt: RegistrationCreatedAt.AddHours(1)
            ),
        ]);
    }

    [Test]
    public async Task GetDueReleasesAsync_LatestUploadOfOneUploadConfigNotCompleted_ReturnsNothing()
    {
        // Arrange
        var registration = AddRegistration();
        var release = AddRelease("Bearcat.Release.001");

        AddUpload(
            AddUploadConfig(release, CreateHosterRegistration("Alpha")),
            RegistrationCreatedAt.AddHours(1)
        );

        AddUpload(
            uploadConfig: AddUploadConfig(release, CreateHosterRegistration("Beta")),
            uploadedAt: null,
            uploadState: UploadState.Uploading,
            createdAt: RegistrationCreatedAt.AddHours(1)
        );
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetDueReleasesAsync(registration, CancellationToken.None);

        // Assert
        result.ShouldBeEmpty();
    }

    [Test]
    public async Task GetDueReleasesAsync_UploadCompletedBeforeRegistrationWasCreated_ReturnsNothing()
    {
        // Arrange
        var registration = AddRegistration();
        var hoster = CreateHosterRegistration("Alpha");
        var release = AddRelease("Bearcat.Release.001");
        AddUpload(AddUploadConfig(release, hoster), RegistrationCreatedAt.AddHours(-1));
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetDueReleasesAsync(registration, CancellationToken.None);

        // Assert
        result.ShouldBeEmpty();
    }

    [Test]
    public async Task GetDueReleasesAsync_NewestUploadAlreadyHandled_ReturnsNothing()
    {
        // Arrange
        var registration = AddRegistration();
        var hoster = CreateHosterRegistration("Alpha");
        var release = AddRelease("Bearcat.Release.001");
        AddUpload(AddUploadConfig(release, hoster), RegistrationCreatedAt.AddHours(1));
        AddExecution(release, registration, handledUploadedAt: RegistrationCreatedAt.AddHours(1));
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetDueReleasesAsync(registration, CancellationToken.None);

        // Assert
        result.ShouldBeEmpty();
    }

    [Test]
    public async Task GetDueReleasesAsync_NewUploadAfterHandledUpload_ReturnsReleaseAsReupload()
    {
        // Arrange
        var registration = AddRegistration();
        var release = AddReuploadedRelease(registration);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetDueReleasesAsync(registration, CancellationToken.None);

        // Assert
        result.ShouldBe([
            new UploadCompletionActionDueRelease(
                ReleaseId: release.Id,
                IsReupload: true,
                NewestUploadedAt: RegistrationCreatedAt.AddDays(2).AddHours(1)
            ),
        ]);
    }

    [Test]
    public async Task GetDueReleasesAsync_ReuploadWithRunForReuploadDisabled_ReturnsNothing()
    {
        // Arrange
        var registration = AddRegistration(runForReupload: false);
        AddReuploadedRelease(registration);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetDueReleasesAsync(registration, CancellationToken.None);

        // Assert
        result.ShouldBeEmpty();
    }

    [Test]
    public async Task GetDueReleasesAsync_InitialUploadWithRunForInitialUploadDisabled_ReturnsNothing()
    {
        // Arrange
        var registration = AddRegistration(runForInitialUpload: false);
        var hoster = CreateHosterRegistration("Alpha");
        var release = AddRelease("Bearcat.Release.001");
        AddUpload(AddUploadConfig(release, hoster), RegistrationCreatedAt.AddHours(1));
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetDueReleasesAsync(registration, CancellationToken.None);

        // Assert
        result.ShouldBeEmpty();
    }

    [Test]
    public async Task GetCompletelyUploadedReleaseAsync_ReuploadAlreadyHandled_ReturnsNewestUploadAsReupload()
    {
        // Arrange
        var registration = AddRegistration();
        var release = AddReuploadedRelease(registration);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetCompletelyUploadedReleaseAsync(
            release.Id,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(
            new UploadCompletionActionDueRelease(
                ReleaseId: release.Id,
                IsReupload: true,
                NewestUploadedAt: RegistrationCreatedAt.AddDays(2).AddHours(1)
            )
        );
    }

    [Test]
    public async Task GetCompletelyUploadedReleaseAsync_LatestUploadOfOneUploadConfigNotCompleted_ReturnsNull()
    {
        // Arrange
        var release = AddRelease("Bearcat.Release.001");

        AddUpload(
            AddUploadConfig(release, CreateHosterRegistration("Alpha")),
            RegistrationCreatedAt.AddHours(1)
        );

        AddUpload(
            uploadConfig: AddUploadConfig(release, CreateHosterRegistration("Beta")),
            uploadedAt: null,
            uploadState: UploadState.Uploading,
            createdAt: RegistrationCreatedAt.AddHours(1)
        );
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetCompletelyUploadedReleaseAsync(
            release.Id,
            CancellationToken.None
        );

        // Assert
        result.ShouldBeNull();
    }

    [Test]
    public async Task GetReleaseStatesAsync_ActiveRegistrationsWithAndWithoutExecution_ReturnsStateOfEachActiveRegistration()
    {
        // Arrange
        var failedRegistration = AddRegistration(name: "Alpha export");
        var notRunRegistration = AddRegistration(name: "Beta export");
        AddRegistration(name: "Inactive export", isActive: false);
        var hoster = CreateHosterRegistration("Alpha");
        var release = AddRelease("Bearcat.Release.001");
        AddUpload(AddUploadConfig(release, hoster), RegistrationCreatedAt.AddHours(1));

        AddExecution(
            release: release,
            registration: failedRegistration,
            handledUploadedAt: RegistrationCreatedAt,
            errorMessage: "Target folder is missing"
        );
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetReleaseStatesAsync(release.Id, CancellationToken.None);

        // Assert
        result.ShouldBe([
            new UploadCompletionActionStateReadModel(
                RegistrationId: failedRegistration.Id,
                RegistrationName: "Alpha export",
                IsCompletelyUploaded: true,
                ExecutedAt: RegistrationCreatedAt,
                ResultText: null,
                ErrorMessage: "Target folder is missing"
            ),
            new UploadCompletionActionStateReadModel(
                RegistrationId: notRunRegistration.Id,
                RegistrationName: "Beta export",
                IsCompletelyUploaded: true,
                ExecutedAt: null,
                ResultText: null,
                ErrorMessage: null
            ),
        ]);
    }

    [Test]
    public async Task GetDueReleaseCollectionsAsync_AllEpisodesCompleted_ReturnsCollectionOnceAsInitialUpload()
    {
        // Arrange
        var registration = AddRegistration();
        var collection = AddReleaseCollection();
        var uploadSlot = AddUploadSlot(collection);
        AddEpisodeUpload(collection, uploadSlot, "S01E01", RegistrationCreatedAt.AddHours(1));
        AddEpisodeUpload(collection, uploadSlot, "S01E02", RegistrationCreatedAt.AddHours(2));
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetDueReleaseCollectionsAsync(
            registration,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe([
            new UploadCompletionActionDueReleaseCollection(
                ReleaseCollectionId: collection.Id,
                IsReupload: false,
                NewestUploadedAt: RegistrationCreatedAt.AddHours(2)
            ),
        ]);
    }

    [Test]
    public async Task GetDueReleaseCollectionsAsync_OneEpisodeStillUploading_ReturnsNothing()
    {
        // Arrange
        var registration = AddRegistration();
        var collection = AddReleaseCollection();
        var uploadSlot = AddUploadSlot(collection);
        AddEpisodeUpload(collection, uploadSlot, "S01E01", RegistrationCreatedAt.AddHours(1));

        AddEpisodeUpload(
            collection: collection,
            uploadSlot: uploadSlot,
            episodeName: "S01E02",
            uploadedAt: null,
            uploadState: UploadState.Uploading,
            createdAt: RegistrationCreatedAt.AddHours(1)
        );
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetDueReleaseCollectionsAsync(
            registration,
            CancellationToken.None
        );

        // Assert
        result.ShouldBeEmpty();
    }

    [Test]
    public async Task GetDueReleaseCollectionsAsync_MemberReleaseNotPassedQualityGate_ReturnsNothing()
    {
        // Arrange
        var registration = AddRegistration();
        var collection = AddReleaseCollection();
        var uploadSlot = AddUploadSlot(collection);
        AddEpisodeUpload(collection, uploadSlot, "S01E01", RegistrationCreatedAt.AddHours(1));

        var notEvaluatedEpisodeUploadConfig = AddEpisodeUpload(
            collection,
            uploadSlot,
            "S01E02",
            RegistrationCreatedAt.AddHours(1)
        );

        notEvaluatedEpisodeUploadConfig.Release.QualityGateState = QualityGateState.NotEvaluated;
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetDueReleaseCollectionsAsync(
            registration,
            CancellationToken.None
        );

        // Assert
        result.ShouldBeEmpty();
    }

    [Test]
    public async Task GetDueReleaseCollectionsAsync_NewEpisodeCompletedAfterHandledUpload_ReturnsCollectionAsInitialUpload()
    {
        // Arrange
        var registration = AddRegistration();
        var collection = AddReleaseCollection();
        var uploadSlot = AddUploadSlot(collection);
        AddEpisodeUpload(collection, uploadSlot, "S01E01", RegistrationCreatedAt.AddHours(1));
        AddEpisodeUpload(collection, uploadSlot, "S01E02", RegistrationCreatedAt.AddDays(2));

        AddReleaseCollectionExecution(
            collection,
            registration,
            handledUploadedAt: RegistrationCreatedAt.AddHours(1)
        );
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetDueReleaseCollectionsAsync(
            registration,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe([
            new UploadCompletionActionDueReleaseCollection(
                ReleaseCollectionId: collection.Id,
                IsReupload: false,
                NewestUploadedAt: RegistrationCreatedAt.AddDays(2)
            ),
        ]);
    }

    [Test]
    public async Task GetDueReleaseCollectionsAsync_NewestUploadAlreadyHandled_ReturnsNothing()
    {
        // Arrange
        var registration = AddRegistration();
        var collection = AddReleaseCollection();
        var uploadSlot = AddUploadSlot(collection);
        AddEpisodeUpload(collection, uploadSlot, "S01E01", RegistrationCreatedAt.AddHours(1));
        AddEpisodeUpload(collection, uploadSlot, "S01E02", RegistrationCreatedAt.AddHours(2));

        AddReleaseCollectionExecution(
            collection,
            registration,
            handledUploadedAt: RegistrationCreatedAt.AddHours(2)
        );
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetDueReleaseCollectionsAsync(
            registration,
            CancellationToken.None
        );

        // Assert
        result.ShouldBeEmpty();
    }

    [Test]
    public async Task GetDueReleaseCollectionsAsync_OnlyReuploadsSinceHandledUpload_ReturnsCollectionAsReupload()
    {
        // Arrange
        var registration = AddRegistration();
        var collection = AddReuploadedReleaseCollection(registration);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetDueReleaseCollectionsAsync(
            registration,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe([
            new UploadCompletionActionDueReleaseCollection(
                ReleaseCollectionId: collection.Id,
                IsReupload: true,
                NewestUploadedAt: RegistrationCreatedAt.AddDays(2).AddHours(1)
            ),
        ]);
    }

    [Test]
    public async Task GetDueReleaseCollectionsAsync_ReuploadWithRunForReuploadDisabled_ReturnsNothing()
    {
        // Arrange
        var registration = AddRegistration(runForReupload: false);
        AddReuploadedReleaseCollection(registration);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetDueReleaseCollectionsAsync(
            registration,
            CancellationToken.None
        );

        // Assert
        result.ShouldBeEmpty();
    }

    [Test]
    public async Task GetDueReleaseCollectionsAsync_InitialUploadWithRunForInitialUploadDisabled_ReturnsNothing()
    {
        // Arrange
        var registration = AddRegistration(runForInitialUpload: false);
        var collection = AddReleaseCollection();
        var uploadSlot = AddUploadSlot(collection);
        AddEpisodeUpload(collection, uploadSlot, "S01E01", RegistrationCreatedAt.AddHours(1));
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetDueReleaseCollectionsAsync(
            registration,
            CancellationToken.None
        );

        // Assert
        result.ShouldBeEmpty();
    }

    [Test]
    public async Task GetCompletelyUploadedReleaseCollectionAsync_OnlyOneEpisodeReuploaded_ReturnsNewestUploadAsInitialUpload()
    {
        // Arrange
        var registration = AddRegistration();
        var collection = AddReuploadedReleaseCollection(registration);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetCompletelyUploadedReleaseCollectionAsync(
            collection.Id,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(
            new UploadCompletionActionDueReleaseCollection(
                ReleaseCollectionId: collection.Id,
                IsReupload: false,
                NewestUploadedAt: RegistrationCreatedAt.AddDays(2).AddHours(1)
            )
        );
    }

    [Test]
    public async Task GetReleaseCollectionStatesAsync_CollectionWithExecution_ReturnsStateOfRegistration()
    {
        // Arrange
        var registration = AddRegistration(name: "Alpha export");
        var collection = AddReleaseCollection();
        var uploadSlot = AddUploadSlot(collection);
        AddEpisodeUpload(collection, uploadSlot, "S01E01", RegistrationCreatedAt.AddHours(1));

        AddReleaseCollectionExecution(
            collection,
            registration,
            handledUploadedAt: RegistrationCreatedAt.AddHours(1)
        );
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetReleaseCollectionStatesAsync(
            collection.Id,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe([
            new UploadCompletionActionStateReadModel(
                RegistrationId: registration.Id,
                RegistrationName: "Alpha export",
                IsCompletelyUploaded: true,
                ExecutedAt: RegistrationCreatedAt.AddHours(1),
                ResultText: null,
                ErrorMessage: null
            ),
        ]);
    }

    private ReleaseCollection AddReuploadedReleaseCollection(
        UploadCompletionActionRegistration registration
    )
    {
        var collection = AddReleaseCollection();
        var uploadSlot = AddUploadSlot(collection);
        AddEpisodeUpload(collection, uploadSlot, "S01E01", RegistrationCreatedAt.AddHours(1));

        var reuploadedEpisodeUploadConfig = AddEpisodeUpload(
            collection,
            uploadSlot,
            "S01E02",
            RegistrationCreatedAt.AddHours(1)
        );

        AddUpload(
            uploadConfig: reuploadedEpisodeUploadConfig,
            uploadedAt: RegistrationCreatedAt.AddDays(2).AddHours(1),
            uploadState: UploadState.Completed,
            createdAt: RegistrationCreatedAt.AddDays(2)
        );

        AddReleaseCollectionExecution(
            collection,
            registration,
            handledUploadedAt: RegistrationCreatedAt.AddHours(1)
        );

        return collection;
    }

    private ReleaseCollection AddReleaseCollection()
    {
        var collection = new ReleaseCollection
        {
            ReleaseContentType = ReleaseContentType.TvShowEpisode,
            ReleaseGroup = new ReleaseGroup
            {
                Name = "Bearcat.Collection group",
                EnableAutomaticReuploads = false,
                NumberOfHoursUntilReupload = 24,
            },
            Key = "bearcat-collection",
            Name = "Bearcat.Collection",
            CreatedAt = RegistrationCreatedAt,
        };

        DbContext.ReleaseCollections.Add(collection);

        return collection;
    }

    private CollectionUploadSlot AddUploadSlot(ReleaseCollection collection)
    {
        var uploadSlot = new CollectionUploadSlot
        {
            ReleaseCollection = collection,
            Key = "1080p",
            Name = "1080p",
        };

        DbContext.CollectionUploadSlots.Add(uploadSlot);

        return uploadSlot;
    }

    private UploadConfig AddEpisodeUpload(
        ReleaseCollection collection,
        CollectionUploadSlot uploadSlot,
        string episodeName,
        DateTime? uploadedAt,
        UploadState uploadState = UploadState.Completed,
        DateTime? createdAt = null
    )
    {
        var release = AddRelease($"Bearcat.Collection.{episodeName}");
        release.ReleaseCollection = collection;
        release.QualityGateState = QualityGateState.Passed;

        var uploadConfig = AddUploadConfig(release, CreateHosterRegistration(episodeName));
        uploadConfig.CollectionUploadSlot = uploadSlot;

        AddUpload(
            uploadConfig: uploadConfig,
            uploadedAt: uploadedAt,
            uploadState: uploadState,
            createdAt: createdAt
        );

        return uploadConfig;
    }

    private void AddReleaseCollectionExecution(
        ReleaseCollection collection,
        UploadCompletionActionRegistration registration,
        DateTime handledUploadedAt
    )
    {
        DbContext.UploadCompletionActionExecutions.Add(
            new UploadCompletionActionExecution
            {
                ReleaseCollection = collection,
                UploadCompletionActionRegistration = registration,
                HandledUploadedAt = handledUploadedAt,
                ExecutedAt = handledUploadedAt,
            }
        );
    }

    private Release AddReuploadedRelease(UploadCompletionActionRegistration registration)
    {
        var hoster = CreateHosterRegistration("Alpha");
        var release = AddRelease("Bearcat.Release.001");
        var uploadConfig = AddUploadConfig(release, hoster);
        AddUpload(uploadConfig, RegistrationCreatedAt.AddHours(1));

        AddUpload(
            uploadConfig: uploadConfig,
            uploadedAt: RegistrationCreatedAt.AddDays(2).AddHours(1),
            uploadState: UploadState.Completed,
            createdAt: RegistrationCreatedAt.AddDays(2)
        );

        AddExecution(release, registration, handledUploadedAt: RegistrationCreatedAt.AddHours(1));

        return release;
    }

    private UploadCompletionActionRegistration AddRegistration(
        bool runForInitialUpload = true,
        bool runForReupload = true,
        string name = "Export",
        bool isActive = true
    )
    {
        var registration = new UploadCompletionActionRegistration
        {
            Name = name,
            ActionClassName = "TestUploadCompletionAction",
            SerializedConfig = "{}",
            IsActive = isActive,
            RunForInitialUpload = runForInitialUpload,
            RunForReupload = runForReupload,
            CreatedAt = RegistrationCreatedAt,
        };

        DbContext.UploadCompletionActionRegistrations.Add(registration);

        return registration;
    }

    private void AddExecution(
        Release release,
        UploadCompletionActionRegistration registration,
        DateTime handledUploadedAt,
        string? errorMessage = null
    )
    {
        DbContext.UploadCompletionActionExecutions.Add(
            new UploadCompletionActionExecution
            {
                Release = release,
                UploadCompletionActionRegistration = registration,
                HandledUploadedAt = handledUploadedAt,
                ExecutedAt = handledUploadedAt,
                ErrorMessage = errorMessage,
            }
        );
    }

    private Release AddRelease(string name)
    {
        var release = new Release
        {
            Name = name,
            CreatedAt = RegistrationCreatedAt,
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = $"/tmp/{name}",
            ReleaseGroup = new ReleaseGroup
            {
                Name = $"{name} group",
                EnableAutomaticReuploads = false,
                NumberOfHoursUntilReupload = 24,
            },
        };

        DbContext.Releases.Add(release);

        return release;
    }

    private static HosterRegistration CreateHosterRegistration(string name)
    {
        return new HosterRegistration
        {
            Name = name,
            SerializedConfig = "{}",
            HosterClassName = $"{name}Hoster",
            IsActive = true,
        };
    }

    private UploadConfig AddUploadConfig(Release release, HosterRegistration hosterRegistration)
    {
        var uploadConfig = new UploadConfig
        {
            UploadSource = UploadSource.ArchiveFiles,
            Release = release,
            ArchiveConfig = new ArchiveConfig
            {
                Release = release,
                Name = "Main archive",
                ArchiveFilesBasePath = "/tmp/archive",
                ArchiverName = "zip",
                ArchiveFileSizeMb = 512,
            },
            HosterRegistration = hosterRegistration,
            Name = $"{hosterRegistration.Name} upload",
        };

        DbContext.UploadConfigs.Add(uploadConfig);

        return uploadConfig;
    }

    private void AddUpload(
        UploadConfig uploadConfig,
        DateTime? uploadedAt,
        UploadState uploadState = UploadState.Completed,
        DateTime? createdAt = null
    )
    {
        DbContext.Uploads.Add(
            new Upload
            {
                UploadConfig = uploadConfig,
                HosterRegistration = uploadConfig.HosterRegistration,
                CreatedAt = createdAt ?? uploadedAt!.Value,
                UploadedAt = uploadedAt,
                UploadState = uploadState,
                OnlineState = OnlineState.Online,
            }
        );
    }
}
