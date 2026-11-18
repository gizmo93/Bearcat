using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.RunUploadCompletionActions.Models;
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
        bool runForReupload = true
    )
    {
        var registration = new UploadCompletionActionRegistration
        {
            Name = "Export",
            ActionClassName = "TestUploadCompletionAction",
            SerializedConfig = "{}",
            IsActive = true,
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
        DateTime handledUploadedAt
    )
    {
        DbContext.UploadCompletionActionExecutions.Add(
            new UploadCompletionActionExecution
            {
                Release = release,
                UploadCompletionActionRegistration = registration,
                HandledUploadedAt = handledUploadedAt,
                ExecutedAt = handledUploadedAt,
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
