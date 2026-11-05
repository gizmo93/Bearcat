using Bearcat.Abstractions.Hoster;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageUploadConfigs.Dto;
using Bearcat.Domain.UseCases.ReplaceHosters;
using Bearcat.Domain.UseCases.ReplaceHosters.Dto;
using Bearcat.Domain.UseCases.ReplaceHosters.ReadModels;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;

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

    private HosterReplacementService service = null!;
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

        service = new HosterReplacementService(
            repository: new HosterReplacementRepository(ReadDbContext, DbContext),
            hosterFactory: hosterFactoryMock.Object,
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
