using Bearcat.Abstractions.Proxies;
using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.IntegrationTest.Database.MaterializedValues;
using Bearcat.Infrastructure.Security;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bearcat.Infrastructure.IntegrationTest.Database.Repositories;

public class UploadFilesRepositoryTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private UploadFilesRepository repository = null!;

    [SetUp]
    public void Setup()
    {
        repository = new UploadFilesRepository(DbContext, DbContext, NoOpSecretProtector.Instance);
    }

    [Test]
    public async Task GetUploadByIdAsync_UploadNotTracked_LoadsUploadFromDatabase()
    {
        // Arrange
        var upload = await AddUploadAsync(UploadState.Pending);

        // Act
        var result = await repository.GetUploadByIdAsync(upload.Id, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(upload.Id);
        result.UploadState.ShouldBe(UploadState.Pending);
    }

    [Test]
    public async Task GetUploadByIdAsync_TrackedUploadChangedInDatabase_ReloadsTrackedUpload()
    {
        // Arrange
        var upload = await AddUploadAsync(UploadState.Pending);
        var trackedUpload = (
            await repository.GetPendingUploadsAsync(new HashSet<int>(), CancellationToken.None)
        ).Single();
        await using (var otherContext = Database.CreateDbContext())
        {
            var storedUpload = await otherContext.Uploads.SingleAsync(u => u.Id == upload.Id);
            storedUpload.UploadState = UploadState.CancellationRequested;
            await otherContext.SaveChangesAsync();
        }

        // Act
        var result = await repository.GetUploadByIdAsync(upload.Id, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeSameAs(trackedUpload);
        result.UploadState.ShouldBe(UploadState.CancellationRequested);
    }

    [Test]
    public async Task GetUploadByIdAsync_TrackedUploadDeletedInDatabase_ReturnsNull()
    {
        // Arrange
        var upload = await AddUploadAsync(UploadState.Pending);
        await repository.GetPendingUploadsAsync(new HashSet<int>(), CancellationToken.None);
        await using (var otherContext = Database.CreateDbContext())
        {
            await otherContext.Uploads.Where(u => u.Id == upload.Id).ExecuteDeleteAsync();
        }

        // Act
        var result = await repository.GetUploadByIdAsync(upload.Id, CancellationToken.None);

        // Assert
        result.ShouldBeNull();
        DbContext.ChangeTracker.Entries<Upload>().ShouldNotContain(e => e.Entity.Id == upload.Id);
    }

    [Test]
    public async Task GetPendingUploadsAsync_HosterRegistrationOfUploadActiveButOfUploadConfigInactive_ReturnsUploadWithHosterRegistrationOfUpload()
    {
        // Arrange
        var upload = await AddUploadAsync(
            UploadState.Pending,
            hosterRegistrationOfUploadConfigIsActive: false,
            hosterRegistrationOfUpload: CreateHosterRegistration("Holding hoster", isActive: true)
        );

        // Act
        var result = await repository.GetPendingUploadsAsync(
            new HashSet<int>(),
            CancellationToken.None
        );

        // Assert
        var pendingUpload = result.ShouldHaveSingleItem();
        pendingUpload.Id.ShouldBe(upload.Id);
        pendingUpload.HosterRegistration!.Name.ShouldBe("Holding hoster");
    }

    [Test]
    public async Task GetPendingUploadsAsync_HosterRegistrationOfUploadInactiveButOfUploadConfigActive_ReturnsEmpty()
    {
        // Arrange
        await AddUploadAsync(
            UploadState.Pending,
            hosterRegistrationOfUploadConfigIsActive: true,
            hosterRegistrationOfUpload: CreateHosterRegistration("Holding hoster", isActive: false)
        );

        // Act
        var result = await repository.GetPendingUploadsAsync(
            new HashSet<int>(),
            CancellationToken.None
        );

        // Assert
        result.ShouldBeEmpty();
    }

    [Test]
    public async Task GetHosterRegistrationByIdAsync_SeveralRegistrations_ReturnsRequestedRegistration()
    {
        // Arrange
        var upload = await AddUploadAsync(UploadState.Pending);
        var otherRegistration = new HosterRegistration
        {
            Name = "Other hoster",
            SerializedConfig = "{\"apiKey\":\"other\"}",
            HosterClassName = "OtherHoster",
            IsActive = false,
            MaxParallelUploadsOverride = 4,
            UploadSpeedLimitMegabytesPerSecond = 12.345m,
            UploadProxySelection = ProxySelection.NoProxy,
        };
        DbContext.HosterRegistrations.Add(otherRegistration);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetHosterRegistrationByIdAsync(
            otherRegistration.Id,
            CancellationToken.None
        );

        // Assert
        result.Id.ShouldBe(otherRegistration.Id);
        result.Id.ShouldNotBe(upload.UploadConfig.HosterRegistrationId!.Value);
        result.Name.ShouldBe("Other hoster");
        result.SerializedConfig.ShouldBe("{\"apiKey\":\"other\"}");
        result.HosterClassName.ShouldBe("OtherHoster");
        result.IsActive.ShouldBeFalse();
        result.MaxParallelUploadsOverride.ShouldBe(4);
        result.UploadSpeedLimitMegabytesPerSecond.ShouldBe(12.345m);
        result.UploadProxySelection.ShouldBe(ProxySelection.NoProxy);
    }

    [Test]
    public async Task GetPendingUploadsAsync_UnassignedUploadOfHosterAccountPool_ReturnsUploadWithPoolMembersAndTheirHosterRegistrations()
    {
        // Arrange
        var testData = new MaterializedValuesTestData(DbContext);
        var firstMember = testData.AddHosterRegistration("First");
        var secondMember = testData.AddHosterRegistration("Second", isActive: false);
        var uploadConfig = testData.AddHosterAccountPoolUploadConfig(
            testData.AddRelease("Release"),
            testData.AddHosterAccountPool("Pool", firstMember, secondMember)
        );
        var upload = testData.AddUnassignedUpload(uploadConfig, UploadState.Pending);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetPendingUploadsAsync(
            new HashSet<int>(),
            CancellationToken.None
        );

        // Assert
        var pendingUpload = result.ShouldHaveSingleItem();
        pendingUpload.Id.ShouldBe(upload.Id);
        pendingUpload.HosterRegistration.ShouldBeNull();
        pendingUpload
            .UploadConfig.HosterAccountPool!.Members.Select(member =>
                (
                    member.Priority,
                    member.HosterRegistration.Name,
                    member.HosterRegistration.IsActive
                )
            )
            .ShouldBe([(1, "First", true), (2, "Second", false)], ignoreOrder: true);
    }

    private async Task<Upload> AddUploadAsync(
        UploadState uploadState,
        bool hosterRegistrationOfUploadConfigIsActive = true,
        HosterRegistration? hosterRegistrationOfUpload = null
    )
    {
        var release = new Release
        {
            Name = "Bearcat.Release.2026-GRP",
            CreatedAt = DateTime.UtcNow,
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = "/tmp/release",
            ReleaseGroup = new ReleaseGroup
            {
                Name = "Release group",
                EnableAutomaticReuploads = false,
                NumberOfHoursUntilReupload = 24,
            },
        };
        var archiveConfig = new ArchiveConfig
        {
            Release = release,
            Name = "Main archive",
            ArchiveFilesBasePath = "/tmp/archives",
            ArchiverName = "RarArchiver",
            ArchiveFileSizeMb = 100,
        };
        var hosterRegistration = new HosterRegistration
        {
            Name = "Hoster",
            SerializedConfig = "{}",
            HosterClassName = "TestHoster",
            IsActive = hosterRegistrationOfUploadConfigIsActive,
        };
        var upload = new Upload
        {
            UploadConfig = new UploadConfig
            {
                Release = release,
                ArchiveConfig = archiveConfig,
                HosterRegistration = hosterRegistration,
                Name = "Default upload",
            },
            HosterRegistration = hosterRegistrationOfUpload ?? hosterRegistration,
            CreatedAt = DateTime.UtcNow,
            UploadState = uploadState,
            OnlineState = OnlineState.Unknown,
            UploadedFiles = [],
            ErrorMessages = [],
        };

        DbContext.Uploads.Add(upload);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return upload;
    }

    private static HosterRegistration CreateHosterRegistration(string name, bool isActive)
    {
        return new HosterRegistration
        {
            Name = name,
            SerializedConfig = "{}",
            HosterClassName = "TestHoster",
            IsActive = isActive,
        };
    }
}
