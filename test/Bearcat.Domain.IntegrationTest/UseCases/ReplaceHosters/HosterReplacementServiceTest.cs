using Bearcat.Abstractions.Configurations;
using Bearcat.Abstractions.Hoster;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.Shared.QualityGate;
using Bearcat.Domain.UseCases.ManageUploadConfigs.Dto;
using Bearcat.Domain.UseCases.ManageUploads.UploadCreation;
using Bearcat.Domain.UseCases.ReplaceHosters;
using Bearcat.Domain.UseCases.ReplaceHosters.Dto;
using Bearcat.Domain.UseCases.ReplaceHosters.ReadModels;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.FileSystem;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.IntegrationTest.UseCases.ReplaceHosters;

public class HosterReplacementServiceTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private const string SourceHosterClassName = "SourceHoster";
    private const string ReplacementHosterClassName = "ReplacementHoster";
    private const string PremiumReplacementHosterClassName = "PremiumReplacementHoster";
    private const string MirrorHosterClassName = "MirrorHoster";
    private const string VideoHosterClassName = "VideoHoster";
    private const string OtherVideoHosterClassName = "OtherVideoHoster";

    private static readonly DateTime LocalNow = new(2026, 11, 5, 12, 0, 0);

    private HosterReplacementService service = null!;
    private TimeProvider timeProvider = null!;
    private ReleaseGroup? releaseGroup;

    public enum InvalidHosterReplacementRequest
    {
        EmptySources = 1,
        DuplicateSources = 2,
        SourcesOfDifferentHosters = 3,
        ReplacementIsSource = 4,
        InactiveReplacementAccount = 5,
        ReplacementPoolWithoutUsableMember = 6,
        ReplacementOfOtherHosterKind = 7,
        DeadSourceHosterWithReplacementOfSameHoster = 8,
    }

    [SetUp]
    public void Setup()
    {
        var hostersByName = new Dictionary<string, IHoster>
        {
            [SourceHosterClassName] = Mock.Of<IHosterWithDownload>(hoster =>
                hoster.SupportsPremiumOnlyDownloads == true
            ),
            [ReplacementHosterClassName] = Mock.Of<IHoster>(hoster =>
                hoster.SupportsPremiumOnlyDownloads == false
            ),
            [PremiumReplacementHosterClassName] = Mock.Of<IHoster>(hoster =>
                hoster.SupportsPremiumOnlyDownloads == true
            ),
            [MirrorHosterClassName] = Mock.Of<IHosterWithDownload>(),
            [VideoHosterClassName] = Mock.Of<IVideoHoster>(),
            [OtherVideoHosterClassName] = Mock.Of<IVideoHoster>(),
        };

        var hosterFactoryMock = new Mock<IHosterFactory>();
        hosterFactoryMock.Setup(factory => factory.GetHostersByName()).Returns(hostersByName);
        hosterFactoryMock
            .Setup(factory => factory.GetByName(It.IsAny<string>()))
            .Returns((string hosterClassName) => hostersByName[hosterClassName]);

        var timeProviderMock = new Mock<TimeProvider>(new ConfigurationBuilder().Build());
        timeProviderMock.Setup(provider => provider.GetLocalNow()).Returns(LocalNow);
        timeProvider = timeProviderMock.Object;

        service = new HosterReplacementService(
            repository: new HosterReplacementRepository(ReadDbContext, DbContext),
            hosterFactory: hosterFactoryMock.Object,
            timeProvider: timeProvider,
            logger: NullLogger<HosterReplacementService>.Instance
        );
    }

    [Test]
    public async Task ExecuteAsync_AccountToAccount_ReplacesTargetAndOnlyDefaultNames()
    {
        // Arrange
        var source = await AddRegistrationAsync("Old account");
        var replacement = await AddRegistrationAsync("New account", ReplacementHosterClassName);
        var release = await AddReleaseAsync("Release");
        var firstArchiveConfig = await AddArchiveConfigAsync(release, "First");
        var secondArchiveConfig = await AddArchiveConfigAsync(release, "Second");
        var defaultNameConfig = await AddUploadConfigAsync(
            archiveConfig: firstArchiveConfig,
            uploadTarget: TargetOf(source),
            name: "Old account"
        );
        var customNameConfig = await AddUploadConfigAsync(
            archiveConfig: secondArchiveConfig,
            uploadTarget: TargetOf(source),
            name: "Old account 100MB"
        );
        var releaseTemplate = await AddReleaseTemplateAsync("Template");
        var defaultNameTemplate = await AddUploadConfigTemplateAsync(
            archiveConfigTemplate: await AddArchiveConfigTemplateAsync(releaseTemplate, "First"),
            uploadTarget: TargetOf(source),
            name: "Old account"
        );
        var customNameTemplate = await AddUploadConfigTemplateAsync(
            archiveConfigTemplate: await AddArchiveConfigTemplateAsync(releaseTemplate, "Second"),
            uploadTarget: TargetOf(source),
            name: "Old account 100MB"
        );
        var templateWithoutName = await AddUploadConfigTemplateAsync(
            archiveConfigTemplate: await AddArchiveConfigTemplateAsync(releaseTemplate, "Third"),
            uploadTarget: TargetOf(source),
            name: null
        );

        // Act
        var result = await service.ExecuteAsync(
            new HosterReplacementRequest([TargetOf(source)], TargetOf(replacement))
        );

        // Assert
        result.UploadConfigCount.ShouldBe(2);
        result.UploadConfigTemplateCount.ShouldBe(3);

        var uploadConfigs = await ReadDbContext.UploadConfigs.ToDictionaryAsync(c => c.Id);
        uploadConfigs.Values.ShouldAllBe(c => c.HosterRegistrationId == replacement.Id);
        uploadConfigs[defaultNameConfig.Id].Name.ShouldBe("New account");
        uploadConfigs[customNameConfig.Id].Name.ShouldBe("Old account 100MB");

        var templates = await ReadDbContext.UploadConfigTemplates.ToDictionaryAsync(t => t.Id);
        templates.Values.ShouldAllBe(t => t.HosterRegistrationId == replacement.Id);
        templates[defaultNameTemplate.Id].Name.ShouldBe("New account");
        templates[customNameTemplate.Id].Name.ShouldBe("Old account 100MB");
        templates[templateWithoutName.Id].Name.ShouldBeNull();
    }

    [Test]
    public async Task ExecuteAsync_PoolToPool_ReplacesPoolTargetAndDefaultName()
    {
        // Arrange
        var sourceAccount = await AddRegistrationAsync("Old account");
        var replacementAccount = await AddRegistrationAsync(
            "New account",
            ReplacementHosterClassName
        );
        var sourcePool = await AddPoolAsync("Old pool", SourceHosterClassName, sourceAccount);
        var replacementPool = await AddPoolAsync(
            name: "New pool",
            hosterClassName: ReplacementHosterClassName,
            member: replacementAccount
        );
        var release = await AddReleaseAsync("Release");
        var archiveConfig = await AddArchiveConfigAsync(release, "Archive");
        var uploadConfig = await AddUploadConfigAsync(
            archiveConfig: archiveConfig,
            uploadTarget: TargetOf(sourcePool),
            name: "Old pool"
        );
        var releaseTemplate = await AddReleaseTemplateAsync("Template");
        var template = await AddUploadConfigTemplateAsync(
            archiveConfigTemplate: await AddArchiveConfigTemplateAsync(releaseTemplate, "Archive"),
            uploadTarget: TargetOf(sourcePool),
            name: "Old pool"
        );

        // Act
        await service.ExecuteAsync(
            new HosterReplacementRequest([TargetOf(sourcePool)], TargetOf(replacementPool))
        );

        // Assert
        var updatedConfig = await ReadDbContext.UploadConfigs.SingleAsync(c =>
            c.Id == uploadConfig.Id
        );
        updatedConfig.HosterRegistrationId.ShouldBeNull();
        updatedConfig.HosterAccountPoolId.ShouldBe(replacementPool.Id);
        updatedConfig.Name.ShouldBe("New pool");

        var updatedTemplate = await ReadDbContext.UploadConfigTemplates.SingleAsync(t =>
            t.Id == template.Id
        );
        updatedTemplate.HosterRegistrationId.ShouldBeNull();
        updatedTemplate.HosterAccountPoolId.ShouldBe(replacementPool.Id);
        updatedTemplate.Name.ShouldBe("New pool");
    }

    [Test]
    public async Task ExecuteAsync_AccountToPoolOfSameHoster_MovesOnlyUnstartedUploadsWithoutFiles()
    {
        // Arrange
        var source = await AddRegistrationAsync("Old account");
        var poolMember = await AddRegistrationAsync("Pool member");
        var replacement = await AddPoolAsync("New pool", SourceHosterClassName, poolMember);
        var release = await AddReleaseAsync("Release");
        var archiveConfig = await AddArchiveConfigAsync(release, "Archive");
        var uploadConfig = await AddUploadConfigAsync(archiveConfig, TargetOf(source), "Custom");
        var pendingUpload = await AddUploadAsync(uploadConfig, UploadState.Pending, source.Id);
        var waitingUpload = await AddUploadAsync(
            uploadConfig: uploadConfig,
            uploadState: UploadState.WaitingForArchive,
            hosterRegistrationId: source.Id
        );
        var pendingUploadWithFiles = await AddUploadAsync(
            uploadConfig: uploadConfig,
            uploadState: UploadState.Pending,
            hosterRegistrationId: source.Id,
            withUploadedFile: true
        );
        var runningUpload = await AddUploadAsync(uploadConfig, UploadState.Uploading, source.Id);
        var completedUpload = await AddUploadAsync(
            uploadConfig: uploadConfig,
            uploadState: UploadState.Completed,
            hosterRegistrationId: source.Id,
            withUploadedFile: true
        );

        // Act
        var result = await service.ExecuteAsync(
            new HosterReplacementRequest([TargetOf(source)], TargetOf(replacement))
        );

        // Assert
        result.UnstartedUploadCount.ShouldBe(2);
        result.UploadCountStayingOnSourceHoster.ShouldBe(2);

        var uploads = await ReadDbContext.Uploads.ToDictionaryAsync(upload => upload.Id);

        foreach (var movedUpload in new[] { pendingUpload, waitingUpload })
        {
            uploads[movedUpload.Id].HosterRegistrationId.ShouldBeNull();
            uploads[movedUpload.Id].HosterFolderId.ShouldBeNull();
        }

        foreach (
            var untouchedUpload in new[] { pendingUploadWithFiles, runningUpload, completedUpload }
        )
        {
            uploads[untouchedUpload.Id].HosterRegistrationId.ShouldBe(source.Id);
            uploads[untouchedUpload.Id].HosterFolderId.ShouldBe("old-folder");
        }
    }

    [TestCase(ReplacementHosterClassName, false)]
    [TestCase(PremiumReplacementHosterClassName, true)]
    public async Task ExecuteAsync_ReplacementHosterPremiumSupport_ResetsPremiumFlagOnlyWhenUnsupported(
        string replacementHosterClassName,
        bool expectedPremiumOnlyDownload
    )
    {
        // Arrange
        var source = await AddRegistrationAsync("Old account");
        var replacement = await AddRegistrationAsync("New account", replacementHosterClassName);
        var release = await AddReleaseAsync("Release");
        var archiveConfig = await AddArchiveConfigAsync(release, "Archive");
        var uploadConfig = await AddUploadConfigAsync(
            archiveConfig: archiveConfig,
            uploadTarget: TargetOf(source),
            name: "Custom",
            premiumOnlyDownload: true
        );
        var pendingUpload = await AddUploadAsync(
            uploadConfig: uploadConfig,
            uploadState: UploadState.Pending,
            hosterRegistrationId: source.Id,
            premiumOnlyDownload: true
        );
        var completedUpload = await AddUploadAsync(
            uploadConfig: uploadConfig,
            uploadState: UploadState.Completed,
            hosterRegistrationId: source.Id,
            withUploadedFile: true,
            premiumOnlyDownload: true
        );
        var releaseTemplate = await AddReleaseTemplateAsync("Template");
        var template = await AddUploadConfigTemplateAsync(
            archiveConfigTemplate: await AddArchiveConfigTemplateAsync(releaseTemplate, "Archive"),
            uploadTarget: TargetOf(source),
            name: "Custom",
            premiumOnlyDownload: true
        );

        // Act
        await service.ExecuteAsync(
            new HosterReplacementRequest([TargetOf(source)], TargetOf(replacement))
        );

        // Assert
        (
            await ReadDbContext.UploadConfigs.SingleAsync(c => c.Id == uploadConfig.Id)
        ).PremiumOnlyDownload.ShouldBe(expectedPremiumOnlyDownload);
        (
            await ReadDbContext.UploadConfigTemplates.SingleAsync(t => t.Id == template.Id)
        ).PremiumOnlyDownload.ShouldBe(expectedPremiumOnlyDownload);
        (
            await ReadDbContext.Uploads.SingleAsync(u => u.Id == pendingUpload.Id)
        ).PremiumOnlyDownload.ShouldBe(expectedPremiumOnlyDownload);
        (
            await ReadDbContext.Uploads.SingleAsync(u => u.Id == completedUpload.Id)
        ).PremiumOnlyDownload.ShouldBeTrue();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ExecuteAsync_ArchiveConfigAlreadyOnReplacementHoster_SkipsItButReplacesOtherArchiveConfig(
        bool existingConfigUsesPool
    )
    {
        // Arrange
        var source = await AddRegistrationAsync("Old account");
        var replacement = await AddRegistrationAsync("New account", ReplacementHosterClassName);
        var otherAccount = await AddRegistrationAsync("Other account", ReplacementHosterClassName);
        var existingTarget = existingConfigUsesPool
            ? TargetOf(await AddPoolAsync("Pool", ReplacementHosterClassName, otherAccount))
            : TargetOf(otherAccount);
        var release = await AddReleaseAsync("Release");
        var conflictingArchiveConfig = await AddArchiveConfigAsync(release, "Conflicting");
        var freeArchiveConfig = await AddArchiveConfigAsync(release, "Free");
        await AddUploadConfigAsync(conflictingArchiveConfig, existingTarget, "Existing");
        var conflictingConfig = await AddUploadConfigAsync(
            archiveConfig: conflictingArchiveConfig,
            uploadTarget: TargetOf(source),
            name: "Conflicting config"
        );
        var freeConfig = await AddUploadConfigAsync(
            archiveConfig: freeArchiveConfig,
            uploadTarget: TargetOf(source),
            name: "Free config"
        );

        // Act
        var result = await service.ExecuteAsync(
            new HosterReplacementRequest([TargetOf(source)], TargetOf(replacement))
        );

        // Assert
        result.UploadConfigCount.ShouldBe(1);
        var skippedConfig = result.SkippedUploadConfigs.ShouldHaveSingleItem();
        skippedConfig.ShouldBe(
            new SkippedUploadConfigReadModel(
                UploadConfigId: conflictingConfig.Id,
                ReleaseId: release.Id,
                ReleaseName: release.Name,
                UploadConfigName: "Conflicting config",
                SkipReason: HosterReplacementSkipReason.DuplicateOnReplacementHoster
            )
        );

        (
            await ReadDbContext.UploadConfigs.SingleAsync(c => c.Id == conflictingConfig.Id)
        ).HosterRegistrationId.ShouldBe(source.Id);
        (
            await ReadDbContext.UploadConfigs.SingleAsync(c => c.Id == freeConfig.Id)
        ).HosterRegistrationId.ShouldBe(replacement.Id);
    }

    [Test]
    public async Task ExecuteAsync_TwoSourcesInOneGroup_ReplacesLowestIdAndSkipsOther()
    {
        // Arrange
        var firstSource = await AddRegistrationAsync("First account");
        var secondSource = await AddRegistrationAsync("Second account");
        var replacement = await AddRegistrationAsync("New account", ReplacementHosterClassName);
        var release = await AddReleaseAsync("Release");
        var archiveConfig = await AddArchiveConfigAsync(release, "Archive");
        var lowestIdConfig = await AddUploadConfigAsync(
            archiveConfig: archiveConfig,
            uploadTarget: TargetOf(secondSource),
            name: "Second account"
        );
        var higherIdConfig = await AddUploadConfigAsync(
            archiveConfig: archiveConfig,
            uploadTarget: TargetOf(firstSource),
            name: "First account"
        );

        // Act
        var result = await service.ExecuteAsync(
            new HosterReplacementRequest(
                [TargetOf(firstSource), TargetOf(secondSource)],
                TargetOf(replacement)
            )
        );

        // Assert
        result.UploadConfigCount.ShouldBe(1);
        var skippedConfig = result.SkippedUploadConfigs.ShouldHaveSingleItem();
        skippedConfig.UploadConfigId.ShouldBe(higherIdConfig.Id);
        skippedConfig.SkipReason.ShouldBe(HosterReplacementSkipReason.DuplicateOnReplacementHoster);

        var replacedConfig = await ReadDbContext.UploadConfigs.SingleAsync(c =>
            c.Id == lowestIdConfig.Id
        );
        replacedConfig.HosterRegistrationId.ShouldBe(replacement.Id);
        replacedConfig.Name.ShouldBe("New account");
        (
            await ReadDbContext.UploadConfigs.SingleAsync(c => c.Id == higherIdConfig.Id)
        ).HosterRegistrationId.ShouldBe(firstSource.Id);
    }

    [Test]
    public async Task ExecuteAsync_ConflictInOneCollectionSlotMember_SkipsAllMembersOfSlot()
    {
        // Arrange
        var source = await AddRegistrationAsync("Old account");
        var replacement = await AddRegistrationAsync("New account", ReplacementHosterClassName);
        var slot = await AddCollectionUploadSlotAsync();
        var conflictingArchiveConfig = await AddArchiveConfigAsync(
            await AddReleaseAsync("Conflicting release"),
            "Archive"
        );
        await AddUploadConfigAsync(conflictingArchiveConfig, TargetOf(replacement), "Existing");
        var conflictingMember = await AddUploadConfigAsync(
            archiveConfig: conflictingArchiveConfig,
            uploadTarget: TargetOf(source),
            name: "Conflicting member",
            collectionUploadSlotId: slot.Id
        );
        var otherMember = await AddUploadConfigAsync(
            archiveConfig: await AddArchiveConfigAsync(
                await AddReleaseAsync("Other release"),
                "Archive"
            ),
            uploadTarget: TargetOf(source),
            name: "Other member",
            collectionUploadSlotId: slot.Id
        );
        var configOutsideSlot = await AddUploadConfigAsync(
            archiveConfig: await AddArchiveConfigAsync(
                await AddReleaseAsync("Release outside slot"),
                "Archive"
            ),
            uploadTarget: TargetOf(source),
            name: "Config outside slot"
        );

        // Act
        var result = await service.ExecuteAsync(
            new HosterReplacementRequest([TargetOf(source)], TargetOf(replacement))
        );

        // Assert
        result.UploadConfigCount.ShouldBe(1);
        result
            .SkippedUploadConfigs.ToDictionary(config => config.UploadConfigId, c => c.SkipReason)
            .ShouldBe(
                new Dictionary<int, HosterReplacementSkipReason>
                {
                    [conflictingMember.Id] =
                        HosterReplacementSkipReason.DuplicateOnReplacementHoster,
                    [otherMember.Id] =
                        HosterReplacementSkipReason.CollectionUploadSlotPartiallySkipped,
                },
                ignoreOrder: true
            );

        (
            await ReadDbContext.UploadConfigs.SingleAsync(c => c.Id == otherMember.Id)
        ).HosterRegistrationId.ShouldBe(source.Id);
        (
            await ReadDbContext.UploadConfigs.SingleAsync(c => c.Id == configOutsideSlot.Id)
        ).HosterRegistrationId.ShouldBe(replacement.Id);
    }

    [Test]
    public async Task ExecuteAsync_TemplateAlreadyOnReplacementHoster_SkipsItWithTargetNameAsDisplayName()
    {
        // Arrange
        var source = await AddRegistrationAsync("Old account");
        var replacement = await AddRegistrationAsync("New account", ReplacementHosterClassName);
        var releaseTemplate = await AddReleaseTemplateAsync("Template");
        var conflictingArchiveConfigTemplate = await AddArchiveConfigTemplateAsync(
            releaseTemplate,
            "Conflicting"
        );
        await AddUploadConfigTemplateAsync(
            archiveConfigTemplate: conflictingArchiveConfigTemplate,
            uploadTarget: TargetOf(replacement),
            name: "Existing"
        );
        var conflictingTemplate = await AddUploadConfigTemplateAsync(
            archiveConfigTemplate: conflictingArchiveConfigTemplate,
            uploadTarget: TargetOf(source),
            name: null
        );
        var freeTemplate = await AddUploadConfigTemplateAsync(
            archiveConfigTemplate: await AddArchiveConfigTemplateAsync(releaseTemplate, "Free"),
            uploadTarget: TargetOf(source),
            name: "Custom"
        );

        // Act
        var result = await service.ExecuteAsync(
            new HosterReplacementRequest([TargetOf(source)], TargetOf(replacement))
        );

        // Assert
        result.UploadConfigTemplateCount.ShouldBe(1);
        result
            .SkippedUploadConfigTemplates.ShouldHaveSingleItem()
            .ShouldBe(
                new SkippedUploadConfigTemplateReadModel(
                    UploadConfigTemplateId: conflictingTemplate.Id,
                    ReleaseTemplateId: releaseTemplate.Id,
                    ReleaseTemplateName: "Template",
                    UploadConfigTemplateDisplayName: "Old account",
                    SkipReason: HosterReplacementSkipReason.DuplicateOnReplacementHoster
                )
            );

        (
            await ReadDbContext.UploadConfigTemplates.SingleAsync(t =>
                t.Id == conflictingTemplate.Id
            )
        ).HosterRegistrationId.ShouldBe(source.Id);
        (
            await ReadDbContext.UploadConfigTemplates.SingleAsync(t => t.Id == freeTemplate.Id)
        ).HosterRegistrationId.ShouldBe(replacement.Id);
    }

    [Test]
    public async Task GetPreviewAsync_ReplaceableData_ReturnsCountsAndWritesNothing()
    {
        // Arrange
        var source = await AddRegistrationAsync("Old account");
        var replacement = await AddRegistrationAsync("New account", ReplacementHosterClassName);
        var release = await AddReleaseAsync("Release");
        var archiveConfig = await AddArchiveConfigAsync(release, "Archive");
        var uploadConfig = await AddUploadConfigAsync(
            archiveConfig: archiveConfig,
            uploadTarget: TargetOf(source),
            name: "Old account",
            premiumOnlyDownload: true
        );
        var pendingUpload = await AddUploadAsync(uploadConfig, UploadState.Pending, source.Id);
        await AddUploadAsync(uploadConfig, UploadState.CancellationRequested, source.Id);
        var releaseTemplate = await AddReleaseTemplateAsync("Template");
        var template = await AddUploadConfigTemplateAsync(
            archiveConfigTemplate: await AddArchiveConfigTemplateAsync(releaseTemplate, "Archive"),
            uploadTarget: TargetOf(source),
            name: "Old account"
        );

        // Act
        var result = await service.GetPreviewAsync(
            new HosterReplacementRequest([TargetOf(source)], TargetOf(replacement))
        );

        // Assert
        result.UploadConfigCount.ShouldBe(1);
        result.UploadConfigTemplateCount.ShouldBe(1);
        result.UnstartedUploadCount.ShouldBe(1);
        result.UploadCountStayingOnSourceHoster.ShouldBe(1);

        var unchangedConfig = await ReadDbContext.UploadConfigs.SingleAsync(c =>
            c.Id == uploadConfig.Id
        );
        unchangedConfig.HosterRegistrationId.ShouldBe(source.Id);
        unchangedConfig.Name.ShouldBe("Old account");
        unchangedConfig.PremiumOnlyDownload.ShouldBeTrue();
        (
            await ReadDbContext.UploadConfigTemplates.SingleAsync(t => t.Id == template.Id)
        ).HosterRegistrationId.ShouldBe(source.Id);
        (
            await ReadDbContext.Uploads.SingleAsync(u => u.Id == pendingUpload.Id)
        ).HosterRegistrationId.ShouldBe(source.Id);
    }

    [TestCase(InvalidHosterReplacementRequest.EmptySources)]
    [TestCase(InvalidHosterReplacementRequest.DuplicateSources)]
    [TestCase(InvalidHosterReplacementRequest.SourcesOfDifferentHosters)]
    [TestCase(InvalidHosterReplacementRequest.ReplacementIsSource)]
    [TestCase(InvalidHosterReplacementRequest.InactiveReplacementAccount)]
    [TestCase(InvalidHosterReplacementRequest.ReplacementPoolWithoutUsableMember)]
    [TestCase(InvalidHosterReplacementRequest.ReplacementOfOtherHosterKind)]
    [TestCase(InvalidHosterReplacementRequest.DeadSourceHosterWithReplacementOfSameHoster)]
    public async Task ExecuteAsync_InvalidRequest_ThrowsInvalidOperationException(
        InvalidHosterReplacementRequest invalidRequest
    )
    {
        // Arrange
        var request = await CreateInvalidRequestAsync(invalidRequest);

        // Act
        var action = () => service.ExecuteAsync(request);

        // Assert
        await Should.ThrowAsync<InvalidOperationException>(action);
    }

    [Test]
    public async Task ExecuteAsync_SourceAccountInUnselectedPool_ReturnsPoolHintsAndKeepsThatPoolConfig()
    {
        // Arrange
        var source = await AddRegistrationAsync("Old account");
        var replacement = await AddRegistrationAsync("New account", ReplacementHosterClassName);
        var unselectedPool = await AddPoolAsync("Unselected", SourceHosterClassName, source);
        var unusedPool = await AddPoolAsync("Unused", SourceHosterClassName, source);
        var poolWithSkippedRow = await AddPoolAsync("Skipped", SourceHosterClassName, source);
        var release = await AddReleaseAsync("Release");
        var archiveConfig = await AddArchiveConfigAsync(release, "Archive");
        await AddUploadConfigAsync(archiveConfig, TargetOf(replacement), "Existing");
        await AddUploadConfigAsync(archiveConfig, TargetOf(poolWithSkippedRow), "Skipped");
        var unusedPoolArchiveConfig = await AddArchiveConfigAsync(release, "Unused pool archive");
        await AddUploadConfigAsync(unusedPoolArchiveConfig, TargetOf(unusedPool), "Unused pool");
        var unselectedPoolConfig = await AddUploadConfigAsync(
            archiveConfig: await AddArchiveConfigAsync(release, "Unselected pool archive"),
            uploadTarget: TargetOf(unselectedPool),
            name: "Unselected pool"
        );

        // Act
        var result = await service.ExecuteAsync(
            new HosterReplacementRequest(
                [TargetOf(source), TargetOf(unusedPool), TargetOf(poolWithSkippedRow)],
                TargetOf(replacement)
            )
        );

        // Assert
        result
            .SourceAccountsInUnselectedPools.ShouldHaveSingleItem()
            .ShouldBe(
                new SourceAccountInUnselectedPoolReadModel(
                    HosterAccountPoolId: unselectedPool.Id,
                    HosterAccountPoolName: "Unselected",
                    HosterRegistrationId: source.Id,
                    HosterRegistrationName: "Old account"
                )
            );
        result
            .SourcePoolsBecomingUnused.ShouldHaveSingleItem()
            .ShouldBe(new SourcePoolBecomingUnusedReadModel(unusedPool.Id, "Unused"));

        (
            await ReadDbContext.UploadConfigs.SingleAsync(c => c.Id == unselectedPoolConfig.Id)
        ).HosterAccountPoolId.ShouldBe(unselectedPool.Id);
    }

    [Test]
    public async Task GetPreviewAsync_NoArchiveNoReleaseFolderAndOnlyUnusableMirrors_CountsUploadConfig()
    {
        // Arrange
        var source = await AddRegistrationAsync("Old account", useForMirrorDownloads: true);
        var replacement = await AddRegistrationAsync("New account", ReplacementHosterClassName);
        var archiveConfig = await AddArchiveConfigAsync(
            await AddReleaseAsync("Release", ReleaseType.Unmanaged, releaseFolderPath: null),
            "Archive"
        );
        await AddArchiveAsync(archiveConfig, ArchiveState.Deleted);
        var uploadConfig = await AddUploadConfigAsync(archiveConfig, TargetOf(source), "Custom");
        await AddCompletedOnlineUploadAsync(uploadConfig, source);
        await AddCompletedOnlineUploadOnNewConfigAsync(
            archiveConfig,
            await AddRegistrationAsync(
                name: "Hoster without download",
                hosterClassName: PremiumReplacementHosterClassName,
                useForMirrorDownloads: true
            )
        );
        await AddCompletedOnlineUploadOnNewConfigAsync(
            archiveConfig,
            await AddRegistrationAsync(
                name: "Inactive mirror",
                hosterClassName: MirrorHosterClassName,
                isActive: false,
                useForMirrorDownloads: true
            )
        );
        await AddCompletedOnlineUploadOnNewConfigAsync(
            archiveConfig,
            await AddRegistrationAsync("Mirror downloads disabled", MirrorHosterClassName)
        );

        // Act
        var result = await service.GetPreviewAsync(
            new HosterReplacementRequest([TargetOf(source)], TargetOf(replacement))
        );

        // Assert
        result.UploadConfigCountWithoutArchiveSource.ShouldBe(1);
    }

    [TestCase(ReleaseType.Unmanaged, null, ArchiveState.Deleted, true)]
    [TestCase(ReleaseType.Unmanaged, null, ArchiveState.Created, false)]
    [TestCase(ReleaseType.Managed, "/tmp/release", ArchiveState.Deleted, false)]
    public async Task GetPreviewAsync_ArchiveSourceLeft_DoesNotCountUploadConfig(
        ReleaseType releaseType,
        string? releaseFolderPath,
        ArchiveState archiveState,
        bool hasMirrorOnOtherHoster
    )
    {
        // Arrange
        var source = await AddRegistrationAsync("Old account");
        var replacement = await AddRegistrationAsync("New account", ReplacementHosterClassName);
        var archiveConfig = await AddArchiveConfigAsync(
            await AddReleaseAsync("Release", releaseType, releaseFolderPath),
            "Archive"
        );
        await AddArchiveAsync(archiveConfig, archiveState);
        await AddUploadConfigAsync(archiveConfig, TargetOf(source), "Custom");

        if (hasMirrorOnOtherHoster)
        {
            await AddCompletedOnlineUploadOnNewConfigAsync(
                archiveConfig,
                await AddRegistrationAsync(
                    name: "Mirror",
                    hosterClassName: MirrorHosterClassName,
                    useForMirrorDownloads: true
                )
            );
        }

        // Act
        var result = await service.GetPreviewAsync(
            new HosterReplacementRequest([TargetOf(source)], TargetOf(replacement))
        );

        // Assert
        result.UploadConfigCount.ShouldBe(1);
        result.UploadConfigCountWithoutArchiveSource.ShouldBe(0);
    }

    [Test]
    public async Task ExecuteAsync_VideoHosterToVideoHoster_ReplacesVideoConfigWithoutArchiveSourceCount()
    {
        // Arrange
        var source = await AddRegistrationAsync("Old account", VideoHosterClassName);
        var replacement = await AddRegistrationAsync("New account", OtherVideoHosterClassName);
        var release = await AddReleaseAsync(
            name: "Release",
            releaseType: ReleaseType.Unmanaged,
            releaseFolderPath: null
        );
        var uploadConfig = new UploadConfig
        {
            ReleaseId = release.Id,
            UploadSource = UploadSource.VideoFiles,
            HosterRegistrationId = source.Id,
            Name = "Old account",
        };
        DbContext.Add(uploadConfig);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await service.ExecuteAsync(
            new HosterReplacementRequest([TargetOf(source)], TargetOf(replacement))
        );

        // Assert
        result.UploadConfigCount.ShouldBe(1);
        result.UploadConfigCountWithoutArchiveSource.ShouldBe(0);
        (
            await ReadDbContext.UploadConfigs.SingleAsync(c => c.Id == uploadConfig.Id)
        ).HosterRegistrationId.ShouldBe(replacement.Id);
    }

    [Test]
    public async Task ExecuteAsync_MarkSourceHosterAsDead_DeactivatesAccountsAndPoolMembersAndMarksUploadsOffline()
    {
        // Arrange
        var source = await AddRegistrationAsync("Old account");
        var poolMember = await AddRegistrationAsync("Pool member");
        var unselectedAccount = await AddRegistrationAsync("Unselected account");
        var sourcePool = await AddPoolAsync("Old pool", SourceHosterClassName, poolMember);
        var replacement = await AddRegistrationAsync("New account", ReplacementHosterClassName);
        var earlierCheck = LocalNow.AddDays(-1);
        var earlierNotFullyOnlineSince = LocalNow.AddDays(-5);

        var onlineRelease = await AddReleaseAsync("Online release");
        var onlineConfig = await AddUploadConfigAsync(
            archiveConfig: await AddArchiveConfigAsync(onlineRelease, "Archive"),
            uploadTarget: TargetOf(source),
            name: "Custom"
        );
        var onlineUpload = await AddCompletedUploadWithFilesAsync(
            uploadConfig: onlineConfig,
            hosterRegistrationId: source.Id,
            uploadOnlineState: OnlineState.Online,
            files: [(OnlineState.Online, null)]
        );
        var runningUpload = await AddUploadAsync(onlineConfig, UploadState.Uploading, source.Id);

        var partiallyOnlineRelease = await AddReleaseAsync("Partially online release");
        var partiallyOnlineConfig = await AddUploadConfigAsync(
            archiveConfig: await AddArchiveConfigAsync(partiallyOnlineRelease, "Archive"),
            uploadTarget: TargetOf(sourcePool),
            name: "Custom"
        );
        var partiallyOnlineUpload = await AddCompletedUploadWithFilesAsync(
            uploadConfig: partiallyOnlineConfig,
            hosterRegistrationId: poolMember.Id,
            uploadOnlineState: OnlineState.PartiallyOnline,
            files:
            [
                (OnlineState.Online, LocalNow.AddHours(-2)),
                (OnlineState.Offline, earlierCheck),
            ],
            notFullyOnlineSince: earlierNotFullyOnlineSince
        );

        var unselectedRelease = await AddReleaseAsync("Unselected release");
        var unselectedConfig = await AddUploadConfigAsync(
            archiveConfig: await AddArchiveConfigAsync(unselectedRelease, "Archive"),
            uploadTarget: TargetOf(unselectedAccount),
            name: "Custom"
        );
        var unselectedUpload = await AddCompletedOnlineUploadAsync(
            unselectedConfig,
            unselectedAccount
        );

        (await GetReleaseOnlineStateAsync(onlineRelease)).ShouldBe(OnlineState.Online);

        var request = new HosterReplacementRequest(
            SourceUploadTargets: [TargetOf(source), TargetOf(sourcePool)],
            ReplacementUploadTarget: TargetOf(replacement),
            MarkSourceHosterAsDead: true
        );

        // Act
        var preview = await service.GetPreviewAsync(request);
        var result = await service.ExecuteAsync(request);

        // Assert
        preview.AccountCountToDeactivate.ShouldBe(2);
        preview.UploadCountToMarkOffline.ShouldBe(2);
        result.AccountCountToDeactivate.ShouldBe(2);
        result.UploadCountToMarkOffline.ShouldBe(2);

        var registrations = await ReadDbContext.HosterRegistrations.ToDictionaryAsync(r => r.Id);
        registrations[source.Id].IsActive.ShouldBeFalse();
        registrations[poolMember.Id].IsActive.ShouldBeFalse();
        registrations[unselectedAccount.Id].IsActive.ShouldBeTrue();
        registrations[replacement.Id].IsActive.ShouldBeTrue();

        var uploads = await ReadDbContext
            .Uploads.Include(upload => upload.UploadedFiles)
            .ToDictionaryAsync(upload => upload.Id);

        var markedOnlineUpload = uploads[onlineUpload.Id];
        markedOnlineUpload.OnlineState.ShouldBe(OnlineState.Offline);
        markedOnlineUpload.NotFullyOnlineSince.ShouldBe(LocalNow);
        markedOnlineUpload.FullyOfflineSince.ShouldBe(LocalNow);
        markedOnlineUpload.OldestUploadedFileCheckedAt.ShouldBe(LocalNow);
        var markedFile = markedOnlineUpload.UploadedFiles.ShouldHaveSingleItem();
        markedFile.OnlineState.ShouldBe(OnlineState.Offline);
        markedFile.CheckedAt.ShouldBe(LocalNow);

        var markedPartiallyOnlineUpload = uploads[partiallyOnlineUpload.Id];
        markedPartiallyOnlineUpload.OnlineState.ShouldBe(OnlineState.Offline);
        markedPartiallyOnlineUpload.NotFullyOnlineSince.ShouldBe(earlierNotFullyOnlineSince);
        markedPartiallyOnlineUpload.FullyOfflineSince.ShouldBe(LocalNow);
        markedPartiallyOnlineUpload.OldestUploadedFileCheckedAt.ShouldBe(earlierCheck);
        markedPartiallyOnlineUpload.UploadedFiles.ShouldAllBe(file =>
            file.OnlineState == OnlineState.Offline
        );
        markedPartiallyOnlineUpload
            .UploadedFiles.Select(file => file.CheckedAt)
            .ShouldBe([LocalNow, earlierCheck], ignoreOrder: true);

        uploads[runningUpload.Id].OnlineState.ShouldBe(OnlineState.Unknown);
        uploads[unselectedUpload.Id].OnlineState.ShouldBe(OnlineState.Online);

        (await GetReleaseOnlineStateAsync(onlineRelease)).ShouldBe(OnlineState.Offline);
        (await GetReleaseOnlineStateAsync(partiallyOnlineRelease)).ShouldBe(OnlineState.Offline);
        (await GetReleaseOnlineStateAsync(unselectedRelease)).ShouldBe(OnlineState.Online);
    }

    [Test]
    public async Task ExecuteAsync_MarkSourceHosterAsDead_AlsoMarksUploadsOfSkippedConfigsOffline()
    {
        // Arrange
        var source = await AddRegistrationAsync("Old account");
        var replacement = await AddRegistrationAsync("New account", ReplacementHosterClassName);
        var release = await AddReleaseAsync("Release");
        var archiveConfig = await AddArchiveConfigAsync(release, "Archive");
        await AddUploadConfigAsync(archiveConfig, TargetOf(replacement), "Existing");
        var skippedConfig = await AddUploadConfigAsync(archiveConfig, TargetOf(source), "Skipped");
        var uploadOfSkippedConfig = await AddCompletedOnlineUploadAsync(skippedConfig, source);

        // Act
        var result = await service.ExecuteAsync(
            new HosterReplacementRequest(
                SourceUploadTargets: [TargetOf(source)],
                ReplacementUploadTarget: TargetOf(replacement),
                MarkSourceHosterAsDead: true
            )
        );

        // Assert
        result.SkippedUploadConfigs.ShouldHaveSingleItem();
        result.UploadCountToMarkOffline.ShouldBe(1);
        (
            await ReadDbContext.UploadConfigs.SingleAsync(c => c.Id == skippedConfig.Id)
        ).HosterRegistrationId.ShouldBe(source.Id);
        (
            await ReadDbContext.Uploads.SingleAsync(u => u.Id == uploadOfSkippedConfig.Id)
        ).OnlineState.ShouldBe(OnlineState.Offline);
    }

    [Test]
    public async Task ExecuteAsync_MarkSourceHosterAsDead_AutomaticReuploadRunsOnReplacementOnceDue()
    {
        // Arrange
        var source = await AddRegistrationAsync("Old account");
        var replacement = await AddRegistrationAsync("New account", ReplacementHosterClassName);
        var group = await GetReleaseGroupAsync();
        group.EnableAutomaticReuploads = true;
        var release = await AddReleaseAsync("Release");
        var uploadConfig = await AddUploadConfigAsync(
            archiveConfig: await AddArchiveConfigAsync(release, "Archive"),
            uploadTarget: TargetOf(source),
            name: "Custom"
        );
        var offlineUpload = await AddCompletedOnlineUploadAsync(uploadConfig, source);

        await service.ExecuteAsync(
            new HosterReplacementRequest(
                SourceUploadTargets: [TargetOf(source)],
                ReplacementUploadTarget: TargetOf(replacement),
                MarkSourceHosterAsDead: true
            )
        );

        var uploadCreationService = CreateUploadCreationService();

        // Act
        await uploadCreationService.CreateAutomaticReuploadsForOfflineUploadsAsync(
            LocalNow.AddHours(23),
            CancellationToken.None
        );
        var uploadCountBeforeDue = await ReadDbContext.Uploads.CountAsync();

        await uploadCreationService.CreateAutomaticReuploadsForOfflineUploadsAsync(
            LocalNow.AddHours(25),
            CancellationToken.None
        );

        // Assert
        uploadCountBeforeDue.ShouldBe(1);
        var reupload = await ReadDbContext.Uploads.SingleAsync(u => u.Id != offlineUpload.Id);
        reupload.UploadConfigId.ShouldBe(uploadConfig.Id);
        reupload.HosterRegistrationId.ShouldBe(replacement.Id);
        reupload.UploadState.ShouldBe(UploadState.WaitingForArchive);
    }

    [Test]
    public async Task ExecuteAsync_WithoutMarkSourceHosterAsDead_ChangesNoOnlineStatesAndNoIsActive()
    {
        // Arrange
        var source = await AddRegistrationAsync("Old account");
        var replacement = await AddRegistrationAsync("New account", ReplacementHosterClassName);
        var release = await AddReleaseAsync("Release");
        var uploadConfig = await AddUploadConfigAsync(
            archiveConfig: await AddArchiveConfigAsync(release, "Archive"),
            uploadTarget: TargetOf(source),
            name: "Custom"
        );
        var upload = await AddCompletedOnlineUploadAsync(uploadConfig, source);

        // Act
        var result = await service.ExecuteAsync(
            new HosterReplacementRequest([TargetOf(source)], TargetOf(replacement))
        );

        // Assert
        result.AccountCountToDeactivate.ShouldBe(0);
        result.UploadCountToMarkOffline.ShouldBe(0);
        (
            await ReadDbContext.HosterRegistrations.SingleAsync(r => r.Id == source.Id)
        ).IsActive.ShouldBeTrue();

        var unchangedUpload = await ReadDbContext
            .Uploads.Include(u => u.UploadedFiles)
            .SingleAsync(u => u.Id == upload.Id);
        unchangedUpload.OnlineState.ShouldBe(OnlineState.Online);
        unchangedUpload.NotFullyOnlineSince.ShouldBeNull();
        unchangedUpload.UploadedFiles.Single().OnlineState.ShouldBe(OnlineState.Online);
        (await GetReleaseOnlineStateAsync(release)).ShouldBe(OnlineState.Online);
    }

    [Test]
    public async Task ExecuteAsync_DeleteDuplicateUploadConfigs_DeletesDuplicatesWithTheirUploadsAndUpdatesReleaseOnlineState()
    {
        // Arrange
        var sourceAccount = await AddRegistrationAsync("Old account");
        var sourcePool = await AddPoolAsync("Old pool", SourceHosterClassName, sourceAccount);
        var replacement = await AddRegistrationAsync("New account", ReplacementHosterClassName);
        var release = await AddReleaseAsync("Release");
        var archiveConfig = await AddArchiveConfigAsync(release, "Archive");
        var existingConfig = await AddUploadConfigAsync(
            archiveConfig: archiveConfig,
            uploadTarget: TargetOf(replacement),
            name: "Existing"
        );
        await AddCompletedOnlineUploadAsync(existingConfig, replacement);
        var duplicateConfig = await AddUploadConfigAsync(
            archiveConfig: archiveConfig,
            uploadTarget: TargetOf(sourcePool),
            name: "Duplicate"
        );
        var uploadOfDuplicateConfig = await AddCompletedUploadWithFilesAsync(
            uploadConfig: duplicateConfig,
            hosterRegistrationId: sourceAccount.Id,
            uploadOnlineState: OnlineState.Offline,
            files: [(OnlineState.Offline, LocalNow)]
        );
        await AddLinkCrypterContainerAndNotificationAsync(release, uploadOfDuplicateConfig);
        var releaseTemplate = await AddReleaseTemplateAsync("Template");
        var archiveConfigTemplate = await AddArchiveConfigTemplateAsync(releaseTemplate, "Archive");
        var existingTemplate = await AddUploadConfigTemplateAsync(
            archiveConfigTemplate: archiveConfigTemplate,
            uploadTarget: TargetOf(replacement),
            name: "Existing"
        );
        var duplicateTemplate = await AddUploadConfigTemplateAsync(
            archiveConfigTemplate: archiveConfigTemplate,
            uploadTarget: TargetOf(sourcePool),
            name: null
        );
        (await GetReleaseOnlineStateAsync(release)).ShouldBe(OnlineState.PartiallyOnline);
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await service.ExecuteAsync(
            new HosterReplacementRequest(
                SourceUploadTargets: [TargetOf(sourcePool)],
                ReplacementUploadTarget: TargetOf(replacement),
                DeleteDuplicateUploadConfigs: true
            )
        );

        // Assert
        result.SkippedUploadConfigs.ShouldBeEmpty();
        result.SkippedUploadConfigTemplates.ShouldBeEmpty();
        result
            .DeletedUploadConfigs.ShouldHaveSingleItem()
            .ShouldBe(
                new SkippedUploadConfigReadModel(
                    UploadConfigId: duplicateConfig.Id,
                    ReleaseId: release.Id,
                    ReleaseName: release.Name,
                    UploadConfigName: "Duplicate",
                    SkipReason: HosterReplacementSkipReason.DuplicateOnReplacementHoster
                )
            );
        result
            .DeletedUploadConfigTemplates.ShouldHaveSingleItem()
            .UploadConfigTemplateId.ShouldBe(duplicateTemplate.Id);
        result
            .SourcePoolsBecomingUnused.ShouldHaveSingleItem()
            .HosterAccountPoolId.ShouldBe(sourcePool.Id);

        (await ReadDbContext.UploadConfigs.Select(c => c.Id).ToListAsync()).ShouldBe([
            existingConfig.Id,
        ]);
        (await ReadDbContext.UploadConfigTemplates.Select(t => t.Id).ToListAsync()).ShouldBe([
            existingTemplate.Id,
        ]);
        (
            await ReadDbContext.Uploads.AnyAsync(u => u.Id == uploadOfDuplicateConfig.Id)
        ).ShouldBeFalse();
        (
            await ReadDbContext.UploadedFiles.AnyAsync(f =>
                f.UploadId == uploadOfDuplicateConfig.Id
            )
        ).ShouldBeFalse();
        (await ReadDbContext.LinkCrypterContainers.AnyAsync()).ShouldBeFalse();
        (await ReadDbContext.Notifications.AnyAsync()).ShouldBeFalse();
        (await GetReleaseOnlineStateAsync(release)).ShouldBe(OnlineState.Online);
    }

    [Test]
    public async Task ExecuteAsync_DeleteDuplicateUploadConfigsWithTwoSourcesInOneGroup_ReplacesLowestIdAndDeletesOther()
    {
        // Arrange
        var firstSource = await AddRegistrationAsync("First account");
        var secondSource = await AddRegistrationAsync("Second account");
        var replacement = await AddRegistrationAsync("New account", ReplacementHosterClassName);
        var archiveConfig = await AddArchiveConfigAsync(
            await AddReleaseAsync("Release"),
            "Archive"
        );
        var lowestIdConfig = await AddUploadConfigAsync(
            archiveConfig: archiveConfig,
            uploadTarget: TargetOf(secondSource),
            name: "Second account"
        );
        var higherIdConfig = await AddUploadConfigAsync(
            archiveConfig: archiveConfig,
            uploadTarget: TargetOf(firstSource),
            name: "First account"
        );
        var archiveConfigTemplate = await AddArchiveConfigTemplateAsync(
            await AddReleaseTemplateAsync("Template"),
            "Archive"
        );
        var lowestIdTemplate = await AddUploadConfigTemplateAsync(
            archiveConfigTemplate: archiveConfigTemplate,
            uploadTarget: TargetOf(secondSource),
            name: null
        );
        var higherIdTemplate = await AddUploadConfigTemplateAsync(
            archiveConfigTemplate: archiveConfigTemplate,
            uploadTarget: TargetOf(firstSource),
            name: null
        );

        // Act
        var result = await service.ExecuteAsync(
            new HosterReplacementRequest(
                SourceUploadTargets: [TargetOf(firstSource), TargetOf(secondSource)],
                ReplacementUploadTarget: TargetOf(replacement),
                DeleteDuplicateUploadConfigs: true
            )
        );

        // Assert
        result.UploadConfigCount.ShouldBe(1);
        result.UploadConfigTemplateCount.ShouldBe(1);
        result.SkippedUploadConfigs.ShouldBeEmpty();
        result.SkippedUploadConfigTemplates.ShouldBeEmpty();
        result
            .DeletedUploadConfigs.ShouldHaveSingleItem()
            .UploadConfigId.ShouldBe(higherIdConfig.Id);
        result
            .DeletedUploadConfigTemplates.ShouldHaveSingleItem()
            .UploadConfigTemplateId.ShouldBe(higherIdTemplate.Id);

        var remainingConfig = await ReadDbContext.UploadConfigs.SingleAsync();
        remainingConfig.Id.ShouldBe(lowestIdConfig.Id);
        remainingConfig.HosterRegistrationId.ShouldBe(replacement.Id);
        var remainingTemplate = await ReadDbContext.UploadConfigTemplates.SingleAsync();
        remainingTemplate.Id.ShouldBe(lowestIdTemplate.Id);
        remainingTemplate.HosterRegistrationId.ShouldBe(replacement.Id);
    }

    [TestCase(UploadState.Uploading)]
    [TestCase(UploadState.CancellationRequested)]
    public async Task ExecuteAsync_DeleteDuplicateUploadConfigs_KeepsSlotMemberAndConfigWithRunningUpload(
        UploadState runningUploadState
    )
    {
        // Arrange
        var source = await AddRegistrationAsync("Old account");
        var replacement = await AddRegistrationAsync("New account", ReplacementHosterClassName);
        var slot = await AddCollectionUploadSlotAsync();
        var slotArchiveConfig = await AddArchiveConfigAsync(
            await AddReleaseAsync("Slot release"),
            "Archive"
        );
        await AddUploadConfigAsync(slotArchiveConfig, TargetOf(replacement), "Existing");
        var duplicateSlotMember = await AddUploadConfigAsync(
            archiveConfig: slotArchiveConfig,
            uploadTarget: TargetOf(source),
            name: "Duplicate slot member",
            collectionUploadSlotId: slot.Id
        );
        var otherSlotMember = await AddUploadConfigAsync(
            archiveConfig: await AddArchiveConfigAsync(
                await AddReleaseAsync("Other slot release"),
                "Archive"
            ),
            uploadTarget: TargetOf(source),
            name: "Other slot member",
            collectionUploadSlotId: slot.Id
        );
        var runningArchiveConfig = await AddArchiveConfigAsync(
            await AddReleaseAsync("Running release"),
            "Archive"
        );
        await AddUploadConfigAsync(runningArchiveConfig, TargetOf(replacement), "Existing");
        var configWithRunningUpload = await AddUploadConfigAsync(
            archiveConfig: runningArchiveConfig,
            uploadTarget: TargetOf(source),
            name: "Running"
        );
        await AddUploadAsync(configWithRunningUpload, runningUploadState, source.Id);

        // Act
        var result = await service.ExecuteAsync(
            new HosterReplacementRequest(
                SourceUploadTargets: [TargetOf(source)],
                ReplacementUploadTarget: TargetOf(replacement),
                DeleteDuplicateUploadConfigs: true
            )
        );

        // Assert
        result.UploadConfigCount.ShouldBe(0);
        result.DeletedUploadConfigs.ShouldBeEmpty();
        result
            .SkippedUploadConfigs.ToDictionary(config => config.UploadConfigId, c => c.SkipReason)
            .ShouldBe(
                new Dictionary<int, HosterReplacementSkipReason>
                {
                    [duplicateSlotMember.Id] =
                        HosterReplacementSkipReason.DeletionBlockedByCollectionUploadSlot,
                    [otherSlotMember.Id] =
                        HosterReplacementSkipReason.CollectionUploadSlotPartiallySkipped,
                    [configWithRunningUpload.Id] =
                        HosterReplacementSkipReason.DeletionBlockedByRunningUpload,
                },
                ignoreOrder: true
            );

        (
            await ReadDbContext
                .UploadConfigs.Where(c => c.HosterRegistrationId == source.Id)
                .Select(c => c.Id)
                .ToListAsync()
        ).ShouldBe(
            [duplicateSlotMember.Id, otherSlotMember.Id, configWithRunningUpload.Id],
            ignoreOrder: true
        );
    }

    [Test]
    public async Task ExecuteAsync_DeleteDuplicateUploadConfigsAndMarkSourceHosterAsDead_DoesNotCountUploadsOfDeletedConfigsAsMarkedOffline()
    {
        // Arrange
        var source = await AddRegistrationAsync("Old account");
        var replacement = await AddRegistrationAsync("New account", ReplacementHosterClassName);
        var duplicateArchiveConfig = await AddArchiveConfigAsync(
            await AddReleaseAsync("Duplicate release"),
            "Archive"
        );
        await AddUploadConfigAsync(duplicateArchiveConfig, TargetOf(replacement), "Existing");
        var duplicateConfig = await AddUploadConfigAsync(
            archiveConfig: duplicateArchiveConfig,
            uploadTarget: TargetOf(source),
            name: "Duplicate"
        );
        await AddCompletedOnlineUploadAsync(duplicateConfig, source);
        var replacedConfig = await AddUploadConfigAsync(
            archiveConfig: await AddArchiveConfigAsync(
                await AddReleaseAsync("Replaced release"),
                "Archive"
            ),
            uploadTarget: TargetOf(source),
            name: "Replaced"
        );
        var uploadOfReplacedConfig = await AddCompletedOnlineUploadAsync(replacedConfig, source);
        var request = new HosterReplacementRequest(
            SourceUploadTargets: [TargetOf(source)],
            ReplacementUploadTarget: TargetOf(replacement),
            MarkSourceHosterAsDead: true,
            DeleteDuplicateUploadConfigs: true
        );

        // Act
        var preview = await service.GetPreviewAsync(request);
        var result = await service.ExecuteAsync(request);

        // Assert
        preview
            .DeletedUploadConfigs.ShouldHaveSingleItem()
            .UploadConfigId.ShouldBe(duplicateConfig.Id);
        preview.UploadCountToMarkOffline.ShouldBe(1);
        result.UploadCountToMarkOffline.ShouldBe(1);
        result.AccountCountToDeactivate.ShouldBe(1);

        (
            await ReadDbContext.UploadConfigs.AnyAsync(c => c.Id == duplicateConfig.Id)
        ).ShouldBeFalse();
        var remainingUploadOfSource = await ReadDbContext.Uploads.SingleAsync(u =>
            u.HosterRegistrationId == source.Id
        );
        remainingUploadOfSource.Id.ShouldBe(uploadOfReplacedConfig.Id);
        remainingUploadOfSource.OnlineState.ShouldBe(OnlineState.Offline);
    }

    [Test]
    public async Task GetPreviewAsync_ReplacementPoolContainsSourceAccount_DoesNotListItAsUnselectedPool()
    {
        // Arrange
        var source = await AddRegistrationAsync("Old account");
        var replacementPool = await AddPoolAsync("New pool", SourceHosterClassName, source);
        var unselectedPool = await AddPoolAsync("Unselected", SourceHosterClassName, source);
        var otherAccount = await AddRegistrationAsync("Other account");
        DbContext.Add(
            new HosterAccountPoolMember
            {
                HosterAccountPoolId = replacementPool.Id,
                HosterRegistrationId = otherAccount.Id,
                Priority = 2,
            }
        );
        await DbContext.SaveChangesAsync();

        // Act
        var result = await service.GetPreviewAsync(
            new HosterReplacementRequest([TargetOf(otherAccount)], TargetOf(replacementPool))
        );
        var resultWithSourceInBothPools = await service.GetPreviewAsync(
            new HosterReplacementRequest([TargetOf(source)], TargetOf(replacementPool))
        );

        // Assert
        result.SourceAccountsInUnselectedPools.ShouldBeEmpty();
        resultWithSourceInBothPools
            .SourceAccountsInUnselectedPools.ShouldHaveSingleItem()
            .HosterAccountPoolId.ShouldBe(unselectedPool.Id);
    }

    [Test]
    public async Task GetSourceChoicesAsync_AccountsAndPoolsOfHoster_ReturnsThemWithUsageCounts()
    {
        // Arrange
        var activeAccount = await AddRegistrationAsync("Active account");
        var inactiveAccount = await AddRegistrationAsync("Inactive account", isActive: false);
        await AddRegistrationAsync("Other hoster account", ReplacementHosterClassName);
        var pool = await AddPoolAsync("Pool", SourceHosterClassName, activeAccount);
        var poolWithoutActiveMember = await AddPoolAsync(
            name: "Pool without active member",
            hosterClassName: SourceHosterClassName,
            member: inactiveAccount
        );
        var release = await AddReleaseAsync("Release");
        await AddUploadConfigAsync(
            archiveConfig: await AddArchiveConfigAsync(release, "First"),
            uploadTarget: TargetOf(activeAccount),
            name: "First"
        );
        await AddUploadConfigAsync(
            archiveConfig: await AddArchiveConfigAsync(release, "Second"),
            uploadTarget: TargetOf(activeAccount),
            name: "Second"
        );
        await AddUploadConfigAsync(
            archiveConfig: await AddArchiveConfigAsync(release, "Third"),
            uploadTarget: TargetOf(pool),
            name: "Third"
        );
        var releaseTemplate = await AddReleaseTemplateAsync("Template");
        await AddUploadConfigTemplateAsync(
            archiveConfigTemplate: await AddArchiveConfigTemplateAsync(releaseTemplate, "First"),
            uploadTarget: TargetOf(inactiveAccount),
            name: null
        );
        await AddUploadConfigTemplateAsync(
            archiveConfigTemplate: await AddArchiveConfigTemplateAsync(releaseTemplate, "Second"),
            uploadTarget: TargetOf(pool),
            name: null
        );

        // Act
        var result = await service.GetSourceChoicesAsync(SourceHosterClassName);

        // Assert
        result.ShouldBe([
            new HosterReplacementSourceChoiceReadModel(
                UploadTarget: TargetOf(activeAccount),
                Name: "Active account",
                IsActive: true,
                UploadConfigCount: 2,
                UploadConfigTemplateCount: 0
            ),
            new HosterReplacementSourceChoiceReadModel(
                UploadTarget: TargetOf(inactiveAccount),
                Name: "Inactive account",
                IsActive: false,
                UploadConfigCount: 0,
                UploadConfigTemplateCount: 1
            ),
            new HosterReplacementSourceChoiceReadModel(
                UploadTarget: TargetOf(pool),
                Name: "Pool",
                IsActive: true,
                UploadConfigCount: 1,
                UploadConfigTemplateCount: 1
            ),
            new HosterReplacementSourceChoiceReadModel(
                UploadTarget: TargetOf(poolWithoutActiveMember),
                Name: "Pool without active member",
                IsActive: false,
                UploadConfigCount: 0,
                UploadConfigTemplateCount: 0
            ),
        ]);
        result.Count(choice => choice.IsHosterAccountPool).ShouldBe(2);
    }

    private static UploadTarget TargetOf(HosterRegistration registration) =>
        UploadTarget.ForHosterRegistration(registration.Id);

    private static UploadTarget TargetOf(HosterAccountPool pool) =>
        UploadTarget.ForHosterAccountPool(pool.Id);

    private async Task<HosterReplacementRequest> CreateInvalidRequestAsync(
        InvalidHosterReplacementRequest invalidRequest
    )
    {
        var source = TargetOf(await AddRegistrationAsync("Old account"));
        var replacement = TargetOf(
            await AddRegistrationAsync("New account", ReplacementHosterClassName)
        );

        return invalidRequest switch
        {
            InvalidHosterReplacementRequest.EmptySources => new HosterReplacementRequest(
                [],
                replacement
            ),
            InvalidHosterReplacementRequest.DuplicateSources => new HosterReplacementRequest(
                [source, source],
                replacement
            ),
            InvalidHosterReplacementRequest.SourcesOfDifferentHosters =>
                new HosterReplacementRequest(
                    [source, TargetOf(await AddRegistrationAsync("Other", MirrorHosterClassName))],
                    replacement
                ),
            InvalidHosterReplacementRequest.ReplacementIsSource => new HosterReplacementRequest(
                [source],
                source
            ),
            InvalidHosterReplacementRequest.InactiveReplacementAccount =>
                new HosterReplacementRequest(
                    [source],
                    TargetOf(
                        await AddRegistrationAsync(
                            name: "Inactive",
                            hosterClassName: ReplacementHosterClassName,
                            isActive: false
                        )
                    )
                ),
            InvalidHosterReplacementRequest.ReplacementPoolWithoutUsableMember =>
                new HosterReplacementRequest(
                    [source],
                    TargetOf(
                        await AddPoolAsync(
                            name: "Pool",
                            hosterClassName: ReplacementHosterClassName,
                            member: await AddRegistrationAsync(
                                name: "Inactive",
                                hosterClassName: ReplacementHosterClassName,
                                isActive: false
                            )
                        )
                    )
                ),
            InvalidHosterReplacementRequest.ReplacementOfOtherHosterKind =>
                new HosterReplacementRequest(
                    [source],
                    TargetOf(await AddRegistrationAsync("Video", VideoHosterClassName))
                ),
            InvalidHosterReplacementRequest.DeadSourceHosterWithReplacementOfSameHoster =>
                new HosterReplacementRequest(
                    SourceUploadTargets: [source],
                    ReplacementUploadTarget: TargetOf(
                        await AddRegistrationAsync("Other account of same hoster")
                    ),
                    MarkSourceHosterAsDead: true
                ),
            _ => throw new ArgumentOutOfRangeException(nameof(invalidRequest)),
        };
    }

    private async Task<HosterRegistration> AddRegistrationAsync(
        string name,
        string hosterClassName = SourceHosterClassName,
        bool isActive = true,
        bool useForMirrorDownloads = false
    )
    {
        var registration = new HosterRegistration
        {
            Name = name,
            SerializedConfig = "{}",
            HosterClassName = hosterClassName,
            IsActive = isActive,
            UseForMirrorDownloads = useForMirrorDownloads,
        };

        DbContext.Add(registration);
        await DbContext.SaveChangesAsync();

        return registration;
    }

    private async Task<HosterAccountPool> AddPoolAsync(
        string name,
        string hosterClassName,
        HosterRegistration member
    )
    {
        var pool = new HosterAccountPool
        {
            Name = name,
            HosterClassName = hosterClassName,
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
            Name = "Replacement group",
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
        };

        DbContext.Add(releaseGroup);
        await DbContext.SaveChangesAsync();

        return releaseGroup;
    }

    private async Task<Release> AddReleaseAsync(
        string name,
        ReleaseType releaseType = ReleaseType.Managed,
        string? releaseFolderPath = "/tmp/release"
    )
    {
        var release = new Release
        {
            Name = name,
            ReleaseType = releaseType,
            ReleaseContentType = ReleaseContentType.Movie,
            ReleaseFolderPath = releaseFolderPath,
            ReleaseGroup = await GetReleaseGroupAsync(),
        };

        DbContext.Add(release);
        await DbContext.SaveChangesAsync();

        return release;
    }

    private async Task<ArchiveConfig> AddArchiveConfigAsync(Release release, string name)
    {
        var archiveConfig = new ArchiveConfig
        {
            ReleaseId = release.Id,
            Name = name,
            ArchiveFilesBasePath = "/tmp/archives",
            ArchiverName = "zip",
            ArchiveFileSizeMb = 512,
        };

        DbContext.Add(archiveConfig);
        await DbContext.SaveChangesAsync();

        return archiveConfig;
    }

    private async Task AddArchiveAsync(ArchiveConfig archiveConfig, ArchiveState archiveState)
    {
        DbContext.Add(
            new Archive
            {
                ArchiveConfigId = archiveConfig.Id,
                ArchiveFolderPath = "/tmp/archives/archive",
                ArchiveState = archiveState,
                CreatedAt = DateTime.UtcNow,
            }
        );
        await DbContext.SaveChangesAsync();
    }

    private async Task<CollectionUploadSlot> AddCollectionUploadSlotAsync()
    {
        var slot = new CollectionUploadSlot
        {
            ReleaseCollection = new ReleaseCollection
            {
                ReleaseGroup = await GetReleaseGroupAsync(),
                Key = "collection",
                Name = "Collection",
                CreatedAt = DateTime.UtcNow,
            },
            Key = "slot",
            Name = "Slot",
        };

        DbContext.Add(slot);
        await DbContext.SaveChangesAsync();

        return slot;
    }

    private async Task<UploadConfig> AddUploadConfigAsync(
        ArchiveConfig archiveConfig,
        UploadTarget uploadTarget,
        string name,
        bool premiumOnlyDownload = false,
        int? collectionUploadSlotId = null
    )
    {
        var uploadConfig = new UploadConfig
        {
            ReleaseId = archiveConfig.ReleaseId,
            UploadSource = UploadSource.ArchiveFiles,
            ArchiveConfigId = archiveConfig.Id,
            CollectionUploadSlotId = collectionUploadSlotId,
            HosterRegistrationId = uploadTarget.HosterRegistrationId,
            HosterAccountPoolId = uploadTarget.HosterAccountPoolId,
            Name = name,
            PremiumOnlyDownload = premiumOnlyDownload,
        };

        DbContext.Add(uploadConfig);
        await DbContext.SaveChangesAsync();

        return uploadConfig;
    }

    private async Task<Upload> AddUploadAsync(
        UploadConfig uploadConfig,
        UploadState uploadState,
        int hosterRegistrationId,
        bool withUploadedFile = false,
        bool premiumOnlyDownload = false
    )
    {
        var upload = new Upload
        {
            UploadConfigId = uploadConfig.Id,
            HosterRegistrationId = hosterRegistrationId,
            HosterFolderId = "old-folder",
            UploadState = uploadState,
            OnlineState = withUploadedFile ? OnlineState.Online : OnlineState.Unknown,
            PremiumOnlyDownload = premiumOnlyDownload,
            CreatedAt = DateTime.UtcNow,
            UploadedFiles = [],
        };

        if (withUploadedFile)
        {
            upload.UploadedFiles.Add(
                new UploadedFile
                {
                    VideoFileRelativePath = "file.bin",
                    HosterFileLink = $"https://hoster.test/file/{Guid.NewGuid()}",
                    OnlineState = OnlineState.Online,
                    CreatedAt = DateTime.UtcNow,
                }
            );
        }

        DbContext.Add(upload);
        await DbContext.SaveChangesAsync();

        return upload;
    }

    private async Task<Upload> AddCompletedUploadWithFilesAsync(
        UploadConfig uploadConfig,
        int hosterRegistrationId,
        OnlineState uploadOnlineState,
        IReadOnlyList<(OnlineState OnlineState, DateTime? CheckedAt)> files,
        DateTime? notFullyOnlineSince = null
    )
    {
        var upload = new Upload
        {
            UploadConfigId = uploadConfig.Id,
            HosterRegistrationId = hosterRegistrationId,
            UploadState = UploadState.Completed,
            OnlineState = uploadOnlineState,
            NotFullyOnlineSince = notFullyOnlineSince,
            CreatedAt = LocalNow.AddDays(-10),
            UploadedFiles = files
                .Select(file => new UploadedFile
                {
                    VideoFileRelativePath = "file.bin",
                    HosterFileLink = $"https://hoster.test/file/{Guid.NewGuid()}",
                    OnlineState = file.OnlineState,
                    CheckedAt = file.CheckedAt,
                    CreatedAt = LocalNow.AddDays(-10),
                })
                .ToList(),
        };

        DbContext.Add(upload);
        await DbContext.SaveChangesAsync();

        return upload;
    }

    private async Task AddLinkCrypterContainerAndNotificationAsync(Release release, Upload upload)
    {
        DbContext.Add(
            new LinkCrypterContainer
            {
                Scope = LinkCrypterContainerScope.Release,
                UploadId = upload.Id,
                ReleaseId = release.Id,
                LinkCrypterRegistration = new LinkCrypterRegistration
                {
                    Name = "Crypter",
                    LinkCrypterClassName = "Crypter",
                    SerializedConfig = "{}",
                    IsActive = true,
                },
                ContainerUrl = "https://crypter.test/container",
                State = LinkCrypterContainerState.Created,
                CreatedAt = LocalNow,
            }
        );
        DbContext.Add(
            new Notification
            {
                UploadId = upload.Id,
                NotificationSeverity = NotificationSeverity.Error,
                NotificationKind = NotificationKind.Legacy,
                Message = "Upload failed",
                CreatedAt = LocalNow,
            }
        );
        await DbContext.SaveChangesAsync();
    }

    private async Task<OnlineState> GetReleaseOnlineStateAsync(Release release)
    {
        var releaseFromDatabase = await ReadDbContext.Releases.SingleAsync(r => r.Id == release.Id);

        return releaseFromDatabase.OnlineState;
    }

    private UploadCreationService CreateUploadCreationService()
    {
        return new UploadCreationService(
            uploadStateRepository: new UploadStateRepository(CreateDbContext()),
            timeProvider: timeProvider,
            configuration: Mock.Of<IApplicationConfigurationProvider>(),
            notificationService: Mock.Of<INotificationService>(),
            logger: NullLogger<UploadCreationService>.Instance,
            qualityGateEvaluator: new QualityGateEvaluator([], new FileSystemService())
        );
    }

    private Task<Upload> AddCompletedOnlineUploadAsync(
        UploadConfig uploadConfig,
        HosterRegistration registration
    )
    {
        return AddUploadAsync(
            uploadConfig: uploadConfig,
            uploadState: UploadState.Completed,
            hosterRegistrationId: registration.Id,
            withUploadedFile: true
        );
    }

    private async Task AddCompletedOnlineUploadOnNewConfigAsync(
        ArchiveConfig archiveConfig,
        HosterRegistration registration
    )
    {
        var uploadConfig = await AddUploadConfigAsync(
            archiveConfig: archiveConfig,
            uploadTarget: TargetOf(registration),
            name: registration.Name
        );

        await AddCompletedOnlineUploadAsync(uploadConfig, registration);
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
        string name
    )
    {
        var archiveConfigTemplate = new ArchiveConfigTemplate
        {
            ReleaseTemplateId = releaseTemplate.Id,
            Name = name,
            ArchiveFilesBasePath = "/tmp/archives",
            ArchiverName = "zip",
            ArchiveFileSizeMb = 512,
        };

        DbContext.Add(archiveConfigTemplate);
        await DbContext.SaveChangesAsync();

        return archiveConfigTemplate;
    }

    private async Task<UploadConfigTemplate> AddUploadConfigTemplateAsync(
        ArchiveConfigTemplate archiveConfigTemplate,
        UploadTarget uploadTarget,
        string? name,
        bool premiumOnlyDownload = false
    )
    {
        var uploadConfigTemplate = new UploadConfigTemplate
        {
            ReleaseTemplateId = archiveConfigTemplate.ReleaseTemplateId,
            UploadSource = UploadSource.ArchiveFiles,
            ArchiveConfigTemplateId = archiveConfigTemplate.Id,
            HosterRegistrationId = uploadTarget.HosterRegistrationId,
            HosterAccountPoolId = uploadTarget.HosterAccountPoolId,
            Name = name,
            PremiumOnlyDownload = premiumOnlyDownload,
        };

        DbContext.Add(uploadConfigTemplate);
        await DbContext.SaveChangesAsync();

        return uploadConfigTemplate;
    }
}
