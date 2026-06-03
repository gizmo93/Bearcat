using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageUploadConfigs;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageUploadConfigs;

public class UploadConfigServiceTest : BearcatIntegrationTest
{
    private BearcatDbContext dbContext = null!;
    private UploadConfigService service = null!;

    [SetUp]
    public void Setup()
    {
        dbContext = Database.CreateDbContext();
        service = new UploadConfigService(new UploadConfigWriteRepository(dbContext));
    }

    [TearDown]
    public async Task DisposeDbContextAsync()
    {
        await dbContext.DisposeAsync();
    }

    [Test]
    public async Task CreateAsync_ValidUploadConfig_PersistsUploadConfigAndReturnsId()
    {
        // Arrange
        var seed = await AddUploadConfigDependenciesAsync();

        // Act
        var result = await service.CreateAsync(
            seed.ReleaseId,
            "Default upload",
            seed.HosterRegistrationId,
            seed.ArchiveConfigId,
            true,
            ["forum-a", "", " ", "forum-b"],
            CancellationToken.None
        );

        // Assert
        var uploadConfig = await dbContext.UploadConfigs.SingleAsync();

        result.ShouldBeGreaterThan(0);
        uploadConfig.ShouldNotBeNull();
        uploadConfig.Id.ShouldBe(result);
        uploadConfig.ReleaseId.ShouldBe(seed.ReleaseId);
        uploadConfig.HosterRegistrationId.ShouldBe(seed.HosterRegistrationId);
        uploadConfig.ArchiveConfigId.ShouldBe(seed.ArchiveConfigId);
        uploadConfig.Name.ShouldBe("Default upload");
        uploadConfig.PremiumOnlyDownload.ShouldBeTrue();
        uploadConfig.LinksDistributedTo.ShouldBe(["forum-a", "forum-b"]);
    }

    [Test]
    public async Task UpdateAsync_UploadConfigExists_UpdatesUploadConfig()
    {
        // Arrange
        var firstSeed = await AddUploadConfigDependenciesAsync();
        var uploadConfig = await AddUploadConfigAsync(firstSeed);
        var secondSeed = await AddUploadConfigDependenciesAsync("Second");

        // Act
        await service.UpdateAsync(
            uploadConfig.Id,
            "Updated upload",
            secondSeed.HosterRegistrationId,
            secondSeed.ArchiveConfigId,
            true,
            ["forum-c", ""],
            CancellationToken.None
        );

        // Assert
        var result = await dbContext.UploadConfigs.SingleAsync();

        result.ShouldNotBeNull();
        result.Id.ShouldBe(uploadConfig.Id);
        result.Name.ShouldBe("Updated upload");
        result.HosterRegistrationId.ShouldBe(secondSeed.HosterRegistrationId);
        result.ArchiveConfigId.ShouldBe(secondSeed.ArchiveConfigId);
        result.PremiumOnlyDownload.ShouldBeTrue();
        result.LinksDistributedTo.ShouldBe(["forum-c", ""]);
    }

    [Test]
    public async Task DeleteAsync_UploadConfigExists_RemovesUploadConfig()
    {
        // Arrange
        var seed = await AddUploadConfigDependenciesAsync();
        var uploadConfig = await AddUploadConfigAsync(seed);

        // Act
        await service.DeleteAsync(uploadConfig.Id, CancellationToken.None);

        // Assert
        var result = await dbContext.UploadConfigs.AnyAsync();

        result.ShouldBeFalse();
    }

    [Test]
    public async Task GetHosterRegistrationOptionsAsync_InactiveHosterRegistration_ExcludesInactiveOption()
    {
        // Arrange
        var activeSeed = await AddUploadConfigDependenciesAsync("Active", hosterIsActive: true);
        var inactiveSeed = await AddUploadConfigDependenciesAsync(
            "Inactive",
            hosterIsActive: false
        );
        var readRepository = new UploadConfigReadRepository(dbContext);

        // Act
        var result = await readRepository.GetHosterRegistrationOptionsAsync(CancellationToken.None);

        // Assert
        result.ShouldContainKey(activeSeed.HosterRegistrationId);
        result.ShouldNotContainKey(inactiveSeed.HosterRegistrationId);
    }

    private async Task<UploadConfig> AddUploadConfigAsync(UploadConfigSeed seed)
    {
        var uploadConfig = new UploadConfig
        {
            ReleaseId = seed.ReleaseId,
            ArchiveConfigId = seed.ArchiveConfigId,
            HosterRegistrationId = seed.HosterRegistrationId,
            Name = "Default upload",
            LinksDistributedTo = ["forum-a"],
        };

        dbContext.UploadConfigs.Add(uploadConfig);
        await dbContext.SaveChangesAsync();

        return uploadConfig;
    }

    private async Task<UploadConfigSeed> AddUploadConfigDependenciesAsync(
        string suffix = "First",
        bool hosterIsActive = true
    )
    {
        var releaseGroup = new ReleaseGroup
        {
            Name = $"{suffix} group",
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
        };
        var release = new Release
        {
            Name = $"Bearcat.Release.{suffix}",
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = $"/tmp/release-{suffix}",
            ReleaseGroup = releaseGroup,
        };
        var archiveConfig = new ArchiveConfig
        {
            Release = release,
            Name = $"{suffix} archive",
            ArchiveFilesBasePath = $"/tmp/archive-{suffix}",
            ArchiverName = "zip",
            ArchiveNamePrefix = "bearcat-release",
            ArchivePassword = "secret",
            ArchiveFileSizeMb = 512,
        };
        var hosterRegistration = new HosterRegistration
        {
            Name = $"{suffix} hoster",
            SerializedConfig = "{}",
            HosterClassName = "TestHoster",
            IsActive = hosterIsActive,
        };

        dbContext.ArchiveConfigs.Add(archiveConfig);
        dbContext.HosterRegistrations.Add(hosterRegistration);
        await dbContext.SaveChangesAsync();

        return new UploadConfigSeed(release.Id, archiveConfig.Id, hosterRegistration.Id);
    }

    private sealed record UploadConfigSeed(
        int ReleaseId,
        int ArchiveConfigId,
        int HosterRegistrationId
    );
}
