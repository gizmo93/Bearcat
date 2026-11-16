using System.Linq.Expressions;
using Bearcat.Abstractions.Archiver;
using Bearcat.Abstractions.Configurations;
using Bearcat.Abstractions.Hoster;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageArchiveConfigs.PartSizeChange;
using Bearcat.Domain.UseCases.ManageArchiveConfigs.PartSizeChange.Dto;
using Bearcat.Domain.UseCases.ManageArchiveConfigs.PartSizeChange.ReadModels;
using Bearcat.Domain.UseCases.ManageUploadConfigs.Dto;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageArchiveConfigs;

public class ArchivePartSizeChangeServiceTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private const string UnlimitedHosterClassName = "UnlimitedHoster";
    private const string LimitedHosterClassName = "LimitedHoster";
    private const string LimitedHosterName = "limited.example";

    private ArchivePartSizeChangeService service = null!;
    private ReleaseGroup? releaseGroup;

    [SetUp]
    public void Setup()
    {
        var hostersByName = new Dictionary<string, IHoster>
        {
            [UnlimitedHosterClassName] = Mock.Of<IHoster>(hoster =>
                hoster.Name == "unlimited.example"
            ),
            [LimitedHosterClassName] = Mock.Of<IHosterWithFileSizeLimit>(hoster =>
                hoster.Name == LimitedHosterName && hoster.MaxFileSizeMb == 100
            ),
        };

        var hosterFactoryMock = new Mock<IHosterFactory>();
        hosterFactoryMock.Setup(factory => factory.GetHostersByName()).Returns(hostersByName);

        var configurationProviderMock = new Mock<IApplicationConfigurationProvider>();

        configurationProviderMock
            .Setup(provider =>
                provider.GetValue<ArchiveCreationConfiguration, ArchivePartSizeUnit>(
                    It.IsAny<Expression<Func<ArchiveCreationConfiguration, ArchivePartSizeUnit>>>()
                )
            )
            .Returns(ArchivePartSizeUnit.Mebibytes);

        service = new ArchivePartSizeChangeService(
            repository: new ArchivePartSizeChangeRepository(ReadDbContext, DbContext),
            hosterFactory: hosterFactoryMock.Object,
            configurationProvider: configurationProviderMock.Object,
            logger: NullLogger<ArchivePartSizeChangeService>.Instance
        );
    }

    [Test]
    public async Task GetPartSizeChoicesAsync_ConfigsAndTemplatesWithSeveralSizes_ReturnsManagedSizesAscendingWithCounts()
    {
        // Arrange
        var release = await AddReleaseAsync("Release");
        await AddArchiveConfigAsync(release, archiveFileSizeMb: 2000);
        await AddArchiveConfigAsync(release, archiveFileSizeMb: 1000);
        await AddArchiveConfigAsync(release, archiveFileSizeMb: 1000);
        await AddArchiveConfigAsync(release, archiveFileSizeMb: 0);
        var releaseTemplate = await AddReleaseTemplateAsync("Template");
        await AddArchiveConfigTemplateAsync(releaseTemplate, archiveFileSizeMb: 1000);
        await AddArchiveConfigTemplateAsync(releaseTemplate, archiveFileSizeMb: 500);
        await AddArchiveConfigTemplateAsync(releaseTemplate, archiveFileSizeMb: 0);

        // Act
        var result = await service.GetPartSizeChoicesAsync(CancellationToken.None);

        // Assert
        result.ShouldBe([
            new ArchivePartSizeChoiceReadModel(500, 0, 1),
            new ArchivePartSizeChoiceReadModel(1000, 2, 1),
            new ArchivePartSizeChoiceReadModel(2000, 1, 0),
        ]);
    }

    [Test]
    public async Task ApplyAsync_TemplatesNotIncluded_ChangesOnlyArchiveConfigsWithCurrentSize()
    {
        // Arrange
        var release = await AddReleaseAsync("Release");
        var matchingArchiveConfig = await AddArchiveConfigAsync(release, archiveFileSizeMb: 1000);
        var otherSizeArchiveConfig = await AddArchiveConfigAsync(release, archiveFileSizeMb: 2000);
        var unmanagedArchiveConfig = await AddArchiveConfigAsync(release, archiveFileSizeMb: 0);
        var releaseTemplate = await AddReleaseTemplateAsync("Template");

        var matchingTemplate = await AddArchiveConfigTemplateAsync(
            releaseTemplate,
            archiveFileSizeMb: 1000
        );

        // Act
        var result = await service.ApplyAsync(
            new ArchivePartSizeChangeRequest(
                CurrentArchiveFileSizeMb: 1000,
                NewArchiveFileSizeMb: 1500,
                IncludeArchiveConfigTemplates: false
            ),
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(new ArchivePartSizeChangeResult(1, 0, 0, 0));

        var archiveFileSizes = await ReadDbContext.ArchiveConfigs.ToDictionaryAsync(
            archiveConfig => archiveConfig.Id,
            archiveConfig => archiveConfig.ArchiveFileSizeMb
        );

        archiveFileSizes[matchingArchiveConfig.Id].ShouldBe(1500);
        archiveFileSizes[otherSizeArchiveConfig.Id].ShouldBe(2000);
        archiveFileSizes[unmanagedArchiveConfig.Id].ShouldBe(0);

        var template = await ReadDbContext.ArchiveConfigTemplates.SingleAsync(t =>
            t.Id == matchingTemplate.Id
        );

        template.ArchiveFileSizeMb.ShouldBe(1000);
    }

    [Test]
    public async Task ApplyAsync_UploadTargetsOnLimitedHoster_SkipsAffectedConfigsAndTemplatesAndChangesTheRest()
    {
        // Arrange
        var limitedRegistration = await AddRegistrationAsync("Limited", LimitedHosterClassName);

        var unlimitedRegistration = await AddRegistrationAsync(
            "Unlimited",
            UnlimitedHosterClassName
        );

        var limitedPool = await AddPoolAsync(limitedRegistration);
        var release = await AddReleaseAsync("Release");
        var archiveConfigOnRegistration = await AddArchiveConfigAsync(release, 90);
        var archiveConfigOnPool = await AddArchiveConfigAsync(release, 90);
        var archiveConfigOnUnlimitedHoster = await AddArchiveConfigAsync(release, 90);

        await AddUploadConfigAsync(
            archiveConfigOnRegistration,
            UploadTarget.ForHosterRegistration(limitedRegistration.Id)
        );

        await AddUploadConfigAsync(
            archiveConfigOnPool,
            UploadTarget.ForHosterAccountPool(limitedPool.Id)
        );

        await AddUploadConfigAsync(
            archiveConfigOnUnlimitedHoster,
            UploadTarget.ForHosterRegistration(unlimitedRegistration.Id)
        );

        var releaseTemplate = await AddReleaseTemplateAsync("Template");
        var templateOnLimitedHoster = await AddArchiveConfigTemplateAsync(releaseTemplate, 90);

        var templateWithoutUploadConfigTemplate = await AddArchiveConfigTemplateAsync(
            releaseTemplate,
            90
        );

        await AddUploadConfigTemplateAsync(templateOnLimitedHoster, limitedPool.Id);

        var request = new ArchivePartSizeChangeRequest(
            CurrentArchiveFileSizeMb: 90,
            NewArchiveFileSizeMb: 96,
            IncludeArchiveConfigTemplates: true
        );

        // Act
        var preview = await service.GetPreviewAsync(request, CancellationToken.None);
        var result = await service.ApplyAsync(request, CancellationToken.None);

        // Assert
        preview.ArchiveConfigCountToChange.ShouldBe(1);
        preview.ArchiveConfigTemplateCountToChange.ShouldBe(1);

        preview
            .SkippedArchiveConfigs.Select(skipped => skipped.ArchiveConfigId)
            .ShouldBe([archiveConfigOnRegistration.Id, archiveConfigOnPool.Id], ignoreOrder: true);

        preview.SkippedArchiveConfigs.ShouldAllBe(skipped =>
            skipped.HosterName == LimitedHosterName
            && skipped.MaxArchiveFileSize == 95
            && skipped.ReleaseId == release.Id
        );

        preview.SkippedArchiveConfigTemplates.ShouldBe([
            new SkippedArchiveConfigTemplateReadModel(
                ArchiveConfigTemplateId: templateOnLimitedHoster.Id,
                ReleaseTemplateId: releaseTemplate.Id,
                ReleaseTemplateName: "Template",
                ArchiveConfigTemplateName: templateOnLimitedHoster.Name,
                HosterName: LimitedHosterName,
                MaxArchiveFileSize: 95
            ),
        ]);

        result.ShouldBe(new ArchivePartSizeChangeResult(1, 1, 2, 1));

        var archiveFileSizes = await ReadDbContext.ArchiveConfigs.ToDictionaryAsync(
            archiveConfig => archiveConfig.Id,
            archiveConfig => archiveConfig.ArchiveFileSizeMb
        );

        archiveFileSizes[archiveConfigOnRegistration.Id].ShouldBe(90);
        archiveFileSizes[archiveConfigOnPool.Id].ShouldBe(90);
        archiveFileSizes[archiveConfigOnUnlimitedHoster.Id].ShouldBe(96);

        var templateFileSizes = await ReadDbContext.ArchiveConfigTemplates.ToDictionaryAsync(
            template => template.Id,
            template => template.ArchiveFileSizeMb
        );

        templateFileSizes[templateOnLimitedHoster.Id].ShouldBe(90);
        templateFileSizes[templateWithoutUploadConfigTemplate.Id].ShouldBe(96);
    }

    [Test]
    public async Task GetPreviewAsync_NewSizeWithinHosterLimit_SkipsNothing()
    {
        // Arrange
        var limitedRegistration = await AddRegistrationAsync("Limited", LimitedHosterClassName);
        var release = await AddReleaseAsync("Release");
        var archiveConfig = await AddArchiveConfigAsync(release, 90);

        await AddUploadConfigAsync(
            archiveConfig,
            UploadTarget.ForHosterRegistration(limitedRegistration.Id)
        );

        // Act
        var result = await service.GetPreviewAsync(
            new ArchivePartSizeChangeRequest(
                CurrentArchiveFileSizeMb: 90,
                NewArchiveFileSizeMb: 95,
                IncludeArchiveConfigTemplates: true
            ),
            CancellationToken.None
        );

        // Assert
        result.ArchiveConfigCountToChange.ShouldBe(1);
        result.SkippedArchiveConfigs.ShouldBeEmpty();
    }

    private async Task<HosterRegistration> AddRegistrationAsync(string name, string hosterClassName)
    {
        var registration = new HosterRegistration
        {
            Name = name,
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
            Name = "Limited pool",
            HosterClassName = member.HosterClassName,
            Members = [new HosterAccountPoolMember { HosterRegistrationId = member.Id }],
        };

        DbContext.Add(pool);
        await DbContext.SaveChangesAsync();

        return pool;
    }

    private async Task<ReleaseGroup> GetReleaseGroupAsync()
    {
        if (releaseGroup is not null)
        {
            return releaseGroup;
        }

        releaseGroup = new ReleaseGroup
        {
            Name = "Part size group",
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
        };

        DbContext.Add(releaseGroup);
        await DbContext.SaveChangesAsync();

        return releaseGroup;
    }

    private async Task<Release> AddReleaseAsync(string name)
    {
        var release = new Release
        {
            Name = name,
            ReleaseType = ReleaseType.Managed,
            ReleaseContentType = ReleaseContentType.Movie,
            ReleaseFolderPath = "/tmp/release",
            ReleaseGroup = await GetReleaseGroupAsync(),
        };

        DbContext.Add(release);
        await DbContext.SaveChangesAsync();

        return release;
    }

    private async Task<ArchiveConfig> AddArchiveConfigAsync(Release release, int archiveFileSizeMb)
    {
        var archiveConfig = new ArchiveConfig
        {
            ReleaseId = release.Id,
            Name = $"Archive {Guid.NewGuid():N}",
            ArchiveFilesBasePath = "/tmp/archives",
            ArchiverName = "zip",
            ArchiveFileSizeMb = archiveFileSizeMb,
        };

        DbContext.Add(archiveConfig);
        await DbContext.SaveChangesAsync();

        return archiveConfig;
    }

    private async Task AddUploadConfigAsync(ArchiveConfig archiveConfig, UploadTarget uploadTarget)
    {
        DbContext.Add(
            new UploadConfig
            {
                ReleaseId = archiveConfig.ReleaseId,
                UploadSource = UploadSource.ArchiveFiles,
                ArchiveConfigId = archiveConfig.Id,
                HosterRegistrationId = uploadTarget.HosterRegistrationId,
                HosterAccountPoolId = uploadTarget.HosterAccountPoolId,
                Name = "Upload",
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
            ReleaseGroup = await GetReleaseGroupAsync(),
        };

        DbContext.Add(releaseTemplate);
        await DbContext.SaveChangesAsync();

        return releaseTemplate;
    }

    private async Task<ArchiveConfigTemplate> AddArchiveConfigTemplateAsync(
        ReleaseTemplate releaseTemplate,
        int archiveFileSizeMb
    )
    {
        var archiveConfigTemplate = new ArchiveConfigTemplate
        {
            ReleaseTemplateId = releaseTemplate.Id,
            Name = $"Archive {Guid.NewGuid():N}",
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
        int hosterAccountPoolId
    )
    {
        DbContext.Add(
            new UploadConfigTemplate
            {
                ReleaseTemplateId = archiveConfigTemplate.ReleaseTemplateId,
                UploadSource = UploadSource.ArchiveFiles,
                ArchiveConfigTemplateId = archiveConfigTemplate.Id,
                HosterAccountPoolId = hosterAccountPoolId,
            }
        );

        await DbContext.SaveChangesAsync();
    }
}
