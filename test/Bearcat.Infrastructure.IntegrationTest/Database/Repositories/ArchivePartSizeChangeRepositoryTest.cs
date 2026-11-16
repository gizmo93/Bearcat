using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageArchiveConfigs.PartSizeChange.ReadModels;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bearcat.Infrastructure.IntegrationTest.Database.Repositories;

public class ArchivePartSizeChangeRepositoryTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private const string LimitedHosterClassName = "LimitedHoster";
    private const string OtherLimitedHosterClassName = "OtherLimitedHoster";
    private const string UnlimitedHosterClassName = "UnlimitedHoster";

    private ArchivePartSizeChangeRepository repository = null!;
    private ReleaseGroup releaseGroup = null!;

    [SetUp]
    public async Task Setup()
    {
        repository = new ArchivePartSizeChangeRepository(ReadDbContext, DbContext);
        releaseGroup = new ReleaseGroup
        {
            Name = "Part size group",
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
        };

        DbContext.Add(releaseGroup);
        await DbContext.SaveChangesAsync();
    }

    [Test]
    public async Task GetArchiveConfigUploadHostersAsync_UploadConfigsOnRegistrationsAndPools_ReturnsMatchingSizeAndHostersOnly()
    {
        // Arrange
        var limitedRegistration = await AddRegistrationAsync(LimitedHosterClassName);
        var otherLimitedRegistration = await AddRegistrationAsync(OtherLimitedHosterClassName);
        var unlimitedRegistration = await AddRegistrationAsync(UnlimitedHosterClassName);
        var otherLimitedPool = await AddPoolAsync(otherLimitedRegistration);
        var release = await AddReleaseAsync("Release");
        var archiveConfigOnBothHosters = await AddArchiveConfigAsync(release, "Both", 1000);
        var archiveConfigOnUnlimitedHoster = await AddArchiveConfigAsync(release, "Free", 1000);
        var archiveConfigWithOtherSize = await AddArchiveConfigAsync(release, "Other", 2000);

        await AddUploadConfigAsync(
            archiveConfig: archiveConfigOnBothHosters,
            hosterRegistrationId: limitedRegistration.Id,
            hosterAccountPoolId: null
        );

        await AddUploadConfigAsync(
            archiveConfig: archiveConfigOnBothHosters,
            hosterRegistrationId: null,
            hosterAccountPoolId: otherLimitedPool.Id
        );

        await AddUploadConfigAsync(
            archiveConfig: archiveConfigOnUnlimitedHoster,
            hosterRegistrationId: unlimitedRegistration.Id,
            hosterAccountPoolId: null
        );

        await AddUploadConfigAsync(
            archiveConfig: archiveConfigWithOtherSize,
            hosterRegistrationId: limitedRegistration.Id,
            hosterAccountPoolId: null
        );

        // Act
        var result = await repository.GetArchiveConfigUploadHostersAsync(
            archiveFileSizeMb: 1000,
            hosterClassNames: [LimitedHosterClassName, OtherLimitedHosterClassName],
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.ShouldBe(
            [
                new ArchiveConfigUploadHosterRow(
                    ArchiveConfigId: archiveConfigOnBothHosters.Id,
                    ReleaseId: release.Id,
                    ReleaseName: "Release",
                    ArchiveConfigName: "Both",
                    HosterClassName: LimitedHosterClassName
                ),
                new ArchiveConfigUploadHosterRow(
                    ArchiveConfigId: archiveConfigOnBothHosters.Id,
                    ReleaseId: release.Id,
                    ReleaseName: "Release",
                    ArchiveConfigName: "Both",
                    HosterClassName: OtherLimitedHosterClassName
                ),
            ],
            ignoreOrder: true
        );
    }

    [Test]
    public async Task GetArchiveConfigTemplateUploadHostersAsync_UploadConfigTemplatesOnRegistrationAndPool_ReturnsMatchingSizeAndHostersOnly()
    {
        // Arrange
        var limitedRegistration = await AddRegistrationAsync(LimitedHosterClassName);
        var unlimitedRegistration = await AddRegistrationAsync(UnlimitedHosterClassName);
        var limitedPool = await AddPoolAsync(limitedRegistration);
        var releaseTemplate = await AddReleaseTemplateAsync("Template");
        var templateOnPool = await AddArchiveConfigTemplateAsync(releaseTemplate, "Pool", 1000);

        var templateOnUnlimitedHoster = await AddArchiveConfigTemplateAsync(
            releaseTemplate,
            "Free",
            1000
        );

        var templateWithOtherSize = await AddArchiveConfigTemplateAsync(
            releaseTemplate,
            "Other",
            2000
        );

        await AddUploadConfigTemplateAsync(
            archiveConfigTemplate: templateOnPool,
            hosterRegistrationId: null,
            hosterAccountPoolId: limitedPool.Id
        );

        await AddUploadConfigTemplateAsync(
            archiveConfigTemplate: templateOnUnlimitedHoster,
            hosterRegistrationId: unlimitedRegistration.Id,
            hosterAccountPoolId: null
        );

        await AddUploadConfigTemplateAsync(
            archiveConfigTemplate: templateWithOtherSize,
            hosterRegistrationId: limitedRegistration.Id,
            hosterAccountPoolId: null
        );

        // Act
        var result = await repository.GetArchiveConfigTemplateUploadHostersAsync(
            archiveFileSizeMb: 1000,
            hosterClassNames: [LimitedHosterClassName],
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.ShouldBe([
            new ArchiveConfigTemplateUploadHosterRow(
                ArchiveConfigTemplateId: templateOnPool.Id,
                ReleaseTemplateId: releaseTemplate.Id,
                ReleaseTemplateName: "Template",
                ArchiveConfigTemplateName: "Pool",
                HosterClassName: LimitedHosterClassName
            ),
        ]);
    }

    [Test]
    public async Task ChangeArchiveConfigFileSizesAsync_SkippedIdsGiven_ChangesOnlyNotSkippedConfigsWithCurrentSize()
    {
        // Arrange
        var release = await AddReleaseAsync("Release");
        var changedArchiveConfig = await AddArchiveConfigAsync(release, "Changed", 1000);
        var skippedArchiveConfig = await AddArchiveConfigAsync(release, "Skipped", 1000);
        var otherSizeArchiveConfig = await AddArchiveConfigAsync(release, "Other", 2000);

        // Act
        var result = await repository.ChangeArchiveConfigFileSizesAsync(
            currentArchiveFileSizeMb: 1000,
            newArchiveFileSizeMb: 1500,
            skippedArchiveConfigIds: [skippedArchiveConfig.Id],
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.ShouldBe(1);

        var archiveFileSizes = await ReadDbContext.ArchiveConfigs.ToDictionaryAsync(
            archiveConfig => archiveConfig.Id,
            archiveConfig => archiveConfig.ArchiveFileSizeMb
        );

        archiveFileSizes[changedArchiveConfig.Id].ShouldBe(1500);
        archiveFileSizes[skippedArchiveConfig.Id].ShouldBe(1000);
        archiveFileSizes[otherSizeArchiveConfig.Id].ShouldBe(2000);
    }

    private async Task<HosterRegistration> AddRegistrationAsync(string hosterClassName)
    {
        var registration = new HosterRegistration
        {
            Name = hosterClassName,
            SerializedConfig = "{}",
            HosterClassName = hosterClassName,
            IsActive = true,
        };

        DbContext.Add(registration);
        await DbContext.SaveChangesAsync();

        return registration;
    }

    private async Task<HosterAccountPool> AddPoolAsync(HosterRegistration member)
    {
        var pool = new HosterAccountPool
        {
            Name = $"{member.HosterClassName} pool",
            HosterClassName = member.HosterClassName,
            Members = [new HosterAccountPoolMember { HosterRegistrationId = member.Id }],
        };

        DbContext.Add(pool);
        await DbContext.SaveChangesAsync();

        return pool;
    }

    private async Task<Release> AddReleaseAsync(string name)
    {
        var release = new Release
        {
            Name = name,
            ReleaseType = ReleaseType.Managed,
            ReleaseContentType = ReleaseContentType.Movie,
            ReleaseFolderPath = "/tmp/release",
            ReleaseGroupId = releaseGroup.Id,
        };

        DbContext.Add(release);
        await DbContext.SaveChangesAsync();

        return release;
    }

    private async Task<ArchiveConfig> AddArchiveConfigAsync(
        Release release,
        string name,
        int archiveFileSizeMb
    )
    {
        var archiveConfig = new ArchiveConfig
        {
            ReleaseId = release.Id,
            Name = name,
            ArchiveFilesBasePath = "/tmp/archives",
            ArchiverName = "zip",
            ArchiveFileSizeMb = archiveFileSizeMb,
        };

        DbContext.Add(archiveConfig);
        await DbContext.SaveChangesAsync();

        return archiveConfig;
    }

    private async Task AddUploadConfigAsync(
        ArchiveConfig archiveConfig,
        int? hosterRegistrationId,
        int? hosterAccountPoolId
    )
    {
        DbContext.Add(
            new UploadConfig
            {
                ReleaseId = archiveConfig.ReleaseId,
                UploadSource = UploadSource.ArchiveFiles,
                ArchiveConfigId = archiveConfig.Id,
                HosterRegistrationId = hosterRegistrationId,
                HosterAccountPoolId = hosterAccountPoolId,
                Name = $"Upload {Guid.NewGuid():N}",
            }
        );

        await DbContext.SaveChangesAsync();
    }

    private async Task<ReleaseTemplate> AddReleaseTemplateAsync(string name)
    {
        var releaseTemplate = new ReleaseTemplate
        {
            Name = name,
            ReleaseType = ReleaseType.Managed,
            ReleaseContentType = ReleaseContentType.Movie,
            ReleaseGroupId = releaseGroup.Id,
        };

        DbContext.Add(releaseTemplate);
        await DbContext.SaveChangesAsync();

        return releaseTemplate;
    }

    private async Task<ArchiveConfigTemplate> AddArchiveConfigTemplateAsync(
        ReleaseTemplate releaseTemplate,
        string name,
        int archiveFileSizeMb
    )
    {
        var archiveConfigTemplate = new ArchiveConfigTemplate
        {
            ReleaseTemplateId = releaseTemplate.Id,
            Name = name,
            ArchiveFilesBasePath = "/tmp/archives",
            ArchiverName = "zip",
            ArchiveFileSizeMb = archiveFileSizeMb,
        };

        DbContext.Add(archiveConfigTemplate);
        await DbContext.SaveChangesAsync();

        return archiveConfigTemplate;
    }

    private async Task AddUploadConfigTemplateAsync(
        ArchiveConfigTemplate archiveConfigTemplate,
        int? hosterRegistrationId,
        int? hosterAccountPoolId
    )
    {
        DbContext.Add(
            new UploadConfigTemplate
            {
                ReleaseTemplateId = archiveConfigTemplate.ReleaseTemplateId,
                UploadSource = UploadSource.ArchiveFiles,
                ArchiveConfigTemplateId = archiveConfigTemplate.Id,
                HosterRegistrationId = hosterRegistrationId,
                HosterAccountPoolId = hosterAccountPoolId,
            }
        );

        await DbContext.SaveChangesAsync();
    }
}
