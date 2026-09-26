using Bearcat.Abstractions;
using Bearcat.Abstractions.Archiver;
using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Media;
using Bearcat.Abstractions.MediaMetadataDatabase;
using Bearcat.Abstractions.NfoDatabase;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.ArchiveRetention;
using Bearcat.Domain.Shared.MediaMetadataResolution;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.Creation;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.FolderUsage;
using Bearcat.Domain.UseCases.ManageReleaseCollections;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.UseCases.ManageReleases.Exceptions;
using Bearcat.Domain.UseCases.ManageReleases.ReleaseInfoResolution;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.FileSystem;
using Bearcat.Infrastructure.Security;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageReleases;

public class ReleaseServiceTest : BearcatIntegrationTest
{
    private BearcatDbContext dbContext = null!;
    private ReleaseService service = null!;
    private string tempRootPath = null!;

    [SetUp]
    public void Setup()
    {
        dbContext = Database.CreateDbContext();
        tempRootPath = Path.Combine(Path.GetTempPath(), $"bearcat-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRootPath);
        var timeProvider = CreateTimeProvider();
        var archiverFactory = new Mock<IArchiverFactory>();
        archiverFactory
            .Setup(f => f.GetArchivers())
            .Returns([new ArchiverDto("RAR", "RarArchiver", ".rar")]);
        service = new ReleaseService(
            new ReleaseWriteRepository(dbContext),
            timeProvider,
            archiverFactory.Object,
            new FileSystemService(),
            new ReleaseFolderUsageRepository(dbContext),
            CreateReleaseFromFolderCreationService(dbContext, archiverFactory.Object, timeProvider),
            new MirrorCoverageEvaluator(Mock.Of<IHosterFactory>()),
            new LocalArchiveDeleter(Mock.Of<IFileSystemService>())
        );
    }

    [TearDown]
    public async Task DisposeDbContextAsync()
    {
        await dbContext.DisposeAsync();

        if (Directory.Exists(tempRootPath))
        {
            Directory.Delete(tempRootPath, recursive: true);
        }
    }

    [Test]
    public async Task CreateAsync_ValidRelease_PersistsReleaseAndReturnsId()
    {
        // Arrange
        var releaseGroup = await AddReleaseGroupAsync("Managed releases");

        // Act
        var result = await service.CreateAsync(
            "Bearcat.Release.001",
            "/tmp/release",
            ReleaseType.Managed,
            ReleaseContentType.Movie,
            releaseGroup.Id,
            null,
            false,
            CancellationToken.None
        );

        // Assert
        var release = await dbContext.Releases.SingleAsync();

        result.ShouldBeGreaterThan(0);
        release.ShouldNotBeNull();
        release.Id.ShouldBe(result);
        release.Name.ShouldBe("Bearcat.Release.001");
        release.CreatedAt.ShouldBeGreaterThan(default);
        release.ReleaseFolderPath.ShouldBe("/tmp/release");
        release.ReleaseType.ShouldBe(ReleaseType.Managed);
        release.ReleaseContentType.ShouldBe(ReleaseContentType.Movie);
        release.ReleaseGroupId.ShouldBe(releaseGroup.Id);
    }

    [Test]
    public async Task UpdateAsync_ChangesReleaseContentType()
    {
        // Arrange
        var releaseGroup = await AddReleaseGroupAsync("Content type group");
        var release = await AddReleaseAsync(releaseGroup.Id);

        // Act
        await service.UpdateAsync(
            release.Id,
            "Bearcat.Release.001",
            release.ReleaseFolderPath,
            ReleaseContentType.TvShowEpisode,
            releaseGroup.Id,
            null,
            false,
            CancellationToken.None
        );

        // Assert
        dbContext.ChangeTracker.Clear();
        var result = await dbContext.Releases.SingleAsync(r => r.Id == release.Id);
        result.ReleaseContentType.ShouldBe(ReleaseContentType.TvShowEpisode);
    }

    [Test]
    public async Task UpdateAsync_ReleaseExists_UpdatesNameAndReleaseGroup()
    {
        // Arrange
        var firstGroup = await AddReleaseGroupAsync("First group");
        var secondGroup = await AddReleaseGroupAsync("Second group");
        var release = await AddReleaseAsync(firstGroup.Id);

        // Act
        await service.UpdateAsync(
            release.Id,
            "Bearcat.Release.Updated",
            "/tmp/release-updated",
            ReleaseContentType.Movie,
            secondGroup.Id,
            null,
            false,
            CancellationToken.None
        );

        // Assert
        var result = await dbContext.Releases.SingleAsync();

        result.ShouldNotBeNull();
        result.Id.ShouldBe(release.Id);
        result.Name.ShouldBe("Bearcat.Release.Updated");
        result.ReleaseGroupId.ShouldBe(secondGroup.Id);
        result.ReleaseFolderPath.ShouldBe("/tmp/release-updated");
    }

    [Test]
    public async Task UpdateReleaseGroupAsync_ReleaseIdsAreEmpty_DoesNotChangeReleases()
    {
        // Arrange
        var firstGroup = await AddReleaseGroupAsync("First group");
        var secondGroup = await AddReleaseGroupAsync("Second group");
        var release = await AddReleaseAsync(firstGroup.Id);

        // Act
        await service.UpdateReleaseGroupAsync([], secondGroup.Id, CancellationToken.None);

        // Assert
        var result = await dbContext.Releases.SingleAsync();

        result.ShouldNotBeNull();
        result.Id.ShouldBe(release.Id);
        result.ReleaseGroupId.ShouldBe(firstGroup.Id);
    }

    [Test]
    public async Task UpdateReleaseGroupAsync_ReleasesExist_UpdatesReleaseGroups()
    {
        // Arrange
        var firstGroup = await AddReleaseGroupAsync("First group");
        var secondGroup = await AddReleaseGroupAsync("Second group");
        var firstRelease = await AddReleaseAsync(firstGroup.Id, "Bearcat.Release.001");
        var secondRelease = await AddReleaseAsync(firstGroup.Id, "Bearcat.Release.002");

        // Act
        await service.UpdateReleaseGroupAsync(
            [firstRelease.Id, secondRelease.Id],
            secondGroup.Id,
            CancellationToken.None
        );

        // Assert
        var result = await dbContext.Releases.OrderBy(r => r.Id).ToListAsync();

        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);
        result.ShouldAllBe(r => r.ReleaseGroupId == secondGroup.Id);
    }

    [TestCase("DE", "de")]
    [TestCase("", null)]
    public async Task UpdatePrimaryLanguageAsync_ReleasesExist_UpdatesPrimaryLanguages(
        string primaryLanguageCode,
        string? expectedLanguageCode
    )
    {
        // Arrange
        var releaseGroup = await AddReleaseGroupAsync("Language group");
        var firstRelease = await AddReleaseAsync(releaseGroup.Id, "Bearcat.Release.001");
        var secondRelease = await AddReleaseAsync(releaseGroup.Id, "Bearcat.Release.002");
        firstRelease.PrimaryLanguageCode = "en";
        secondRelease.PrimaryLanguageCode = "en";
        await dbContext.SaveChangesAsync();

        // Act
        await service.UpdatePrimaryLanguageAsync(
            [firstRelease.Id, secondRelease.Id],
            primaryLanguageCode,
            CancellationToken.None
        );

        // Assert
        dbContext.ChangeTracker.Clear();
        var releases = await dbContext.Releases.OrderBy(release => release.Id).ToListAsync();
        releases.ShouldAllBe(release => release.PrimaryLanguageCode == expectedLanguageCode);
    }

    [Test]
    public async Task DeleteAsync_ReleaseExists_RemovesRelease()
    {
        // Arrange
        var releaseGroup = await AddReleaseGroupAsync("Managed releases");
        var release = await AddReleaseAsync(releaseGroup.Id);

        // Act
        await service.DeleteAsync(release.Id, CancellationToken.None);

        // Assert
        var result = await dbContext.Releases.AnyAsync();

        result.ShouldBeFalse();
    }

    [Test]
    public async Task CreateFromTemplateAsync_ValidTemplate_PersistsReleaseWithConfigs()
    {
        // Arrange
        var seed = await AddReleaseTemplateAsync();
        var releaseFolderPath = CreateTemplateReleaseFolder("Bearcat.Release.Template");

        // Act
        var result = await service.CreateFromTemplateAsync(
            seed.ReleaseTemplateId,
            releaseFolderPath,
            null,
            null,
            CancellationToken.None
        );

        // Assert
        var release = await dbContext
            .Releases.AsSplitQuery()
            .Include(r => r.ArchiveConfigs)
            .Include(r => r.UploadConfigs)
                .ThenInclude(u => u.LinkCrypters)
            .SingleAsync(r => r.Id == result);

        release.Name.ShouldBe("Bearcat.Release.Template");
        release.CreatedAt.ShouldBeGreaterThan(default);
        release.ReleaseFolderPath.ShouldBe(releaseFolderPath);
        release.ReleaseType.ShouldBe(ReleaseType.Managed);
        release.ReleaseContentType.ShouldBe(ReleaseContentType.TvShowEpisode);
        release.ReleaseGroupId.ShouldBe(seed.ReleaseGroupId);

        var archiveConfig = release.ArchiveConfigs.Single();
        archiveConfig.Name.ShouldBe("RAR Forum A");
        archiveConfig.ArchiveFilesBasePath.ShouldBe("/tmp/archives");
        archiveConfig.ArchiverName.ShouldBe("rar");
        archiveConfig.ArchivePassword.ShouldBe("archive-secret");
        archiveConfig.ArchiveFileSizeMb.ShouldBe(1024);
        archiveConfig.ArchiveNamePrefix.ShouldBe(release.Name);

        var uploadConfig = release.UploadConfigs.Single();
        uploadConfig.Name.ShouldBe("Primary hoster");
        uploadConfig.HosterRegistrationId.ShouldBe(seed.HosterRegistrationId);
        uploadConfig.ArchiveConfigId.ShouldBe(archiveConfig.Id);
        uploadConfig.PremiumOnlyDownload.ShouldBeTrue();

        var linkCrypter = uploadConfig.LinkCrypters.Single();
        linkCrypter.LinkCrypterRegistrationId.ShouldBe(seed.LinkCrypterRegistrationId);
        linkCrypter.Password.ShouldBe("container-secret");
    }

    [Test]
    public async Task CreateFromTemplateAsync_TemplateWithAdditionalArchiveContents_AssignsSameContentsToArchiveConfig()
    {
        // Arrange
        var seed = await AddReleaseTemplateAsync();
        var additionalArchivePath = new AdditionalArchiveContent
        {
            Name = "Premium ad folder",
            Type = AdditionalArchiveContentType.Path,
            SourcePath = "/data/ads/premium",
        };
        var additionalArchiveTextFile = new AdditionalArchiveContent
        {
            Name = "Premium ad text",
            Type = AdditionalArchiveContentType.TextFile,
            FileName = "buy premium via me.txt",
            TextContent = "Buy premium via my link.",
        };
        var archiveConfigTemplate = await dbContext.ArchiveConfigTemplates.SingleAsync();
        archiveConfigTemplate.AdditionalArchiveContents =
        [
            additionalArchivePath,
            additionalArchiveTextFile,
        ];
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var releaseFolderPath = CreateTemplateReleaseFolder("Bearcat.Release.Template");

        // Act
        var result = await service.CreateFromTemplateAsync(
            seed.ReleaseTemplateId,
            releaseFolderPath,
            null,
            null,
            CancellationToken.None
        );

        // Assert
        var verificationDbContext = CreateDbContext();
        var archiveConfig = await verificationDbContext
            .ArchiveConfigs.Include(config => config.AdditionalArchiveContents)
            .SingleAsync(config => config.ReleaseId == result);

        archiveConfig
            .AdditionalArchiveContents.Select(content => content.Id)
            .Order()
            .ShouldBe(new[] { additionalArchivePath.Id, additionalArchiveTextFile.Id }.Order());
        (await verificationDbContext.AdditionalArchiveContents.CountAsync()).ShouldBe(2);
    }

    [Test]
    public async Task CreateFromTemplateAsync_TemplateUsesCollectionSlots_AssignsReleaseAndUploadSlot()
    {
        // Arrange
        var seed = await AddReleaseTemplateAsync();
        var releaseTemplate = await dbContext
            .ReleaseTemplates.Include(template => template.UploadConfigTemplates)
            .SingleAsync(template => template.Id == seed.ReleaseTemplateId);

        releaseTemplate.ReleaseCollectionDetectionMode =
            ReleaseCollectionDetectionMode.SeriesEpisodePattern;
        releaseTemplate.UploadConfigTemplates.Single().CollectionUploadSlotKey =
            "forum-a-rg-passworded";
        releaseTemplate.UploadConfigTemplates.Single().CollectionUploadSlotName =
            "Forum A Rapidgator passworded";
        releaseTemplate.UploadConfigTemplates.Single().CollectionUploadSlotIsRequired = true;
        releaseTemplate.UploadConfigTemplates.Single().CollectionUploadSlotPasswordPolicy =
            CollectionUploadSlotPasswordPolicy.MustMatchAcrossReleases;
        await dbContext.SaveChangesAsync();
        var releaseFolderPath = CreateTemplateReleaseFolder(
            "Hostage.S01E01.German.AC3.DL.1080p.Web.x265-FuN.mkv"
        );

        // Act
        var result = await service.CreateFromTemplateAsync(
            seed.ReleaseTemplateId,
            releaseFolderPath,
            null,
            null,
            CancellationToken.None
        );

        // Assert
        var release = await dbContext
            .Releases.AsSplitQuery()
            .Include(release => release.ReleaseCollection)
                .ThenInclude(collection => collection!.UploadSlots)
            .Include(release => release.UploadConfigs)
                .ThenInclude(uploadConfig => uploadConfig.CollectionUploadSlot)
            .SingleAsync(release => release.Id == result);

        release.ReleaseCollection.ShouldNotBeNull();
        release.ReleaseCollection.Name.ShouldBe("Hostage.S01.German.AC3.DL.1080p.Web.x265-FuN.mkv");
        release.ReleaseContentType.ShouldBe(ReleaseContentType.TvShowEpisode);
        release.ReleaseCollection.ReleaseContentType.ShouldBe(ReleaseContentType.TvShowEpisode);
        release.ReleaseCollection.UploadSlots.Count.ShouldBe(1);

        var uploadSlot = release.ReleaseCollection.UploadSlots.Single();
        uploadSlot.Key.ShouldBe("forum-a-rg-passworded");
        uploadSlot.Name.ShouldBe("Forum A Rapidgator passworded");
        uploadSlot.IsRequired.ShouldBeTrue();
        uploadSlot.PasswordPolicy.ShouldBe(
            CollectionUploadSlotPasswordPolicy.MustMatchAcrossReleases
        );

        release.UploadConfigs.Single().CollectionUploadSlotId.ShouldBe(uploadSlot.Id);
    }

    [Test]
    public async Task CreateFromTemplateAsync_TemplateWithCollectionImageConfig_MaterializesDeduplicatedConfig()
    {
        // Arrange
        var seed = await AddReleaseTemplateAsync();
        var imageHosterRegistration = new ImageHosterRegistration
        {
            Name = "ImgBB",
            ImageHosterClassName = "ImgBb",
            SerializedConfig = "{}",
            IsActive = true,
        };
        dbContext.ImageHosterRegistrations.Add(imageHosterRegistration);
        await dbContext.SaveChangesAsync();

        var releaseTemplate = await dbContext.ReleaseTemplates.SingleAsync(template =>
            template.Id == seed.ReleaseTemplateId
        );
        releaseTemplate.ReleaseCollectionDetectionMode =
            ReleaseCollectionDetectionMode.SeriesEpisodePattern;
        releaseTemplate.CollectionImageUploadConfigTemplates =
        [
            new CollectionImageUploadConfigTemplate
            {
                ImageHosterRegistrationId = imageHosterRegistration.Id,
                Name = "Series cover",
            },
        ];
        await dbContext.SaveChangesAsync();
        var firstEpisodeFolderPath = CreateTemplateReleaseFolder(
            "Hostage.S01E01.German.AC3.DL.1080p.Web.x265-FuN.mkv"
        );
        var secondEpisodeFolderPath = CreateTemplateReleaseFolder(
            "Hostage.S01E02.German.AC3.DL.1080p.Web.x265-FuN.mkv"
        );

        // Act
        await service.CreateFromTemplateAsync(
            seed.ReleaseTemplateId,
            firstEpisodeFolderPath,
            null,
            null,
            CancellationToken.None
        );
        await service.CreateFromTemplateAsync(
            seed.ReleaseTemplateId,
            secondEpisodeFolderPath,
            null,
            null,
            CancellationToken.None
        );

        // Assert
        var collection = await dbContext
            .ReleaseCollections.Include(releaseCollection => releaseCollection.ImageUploadConfigs)
            .SingleAsync(releaseCollection =>
                releaseCollection.ReleaseGroupId == seed.ReleaseGroupId
            );

        var config = collection.ImageUploadConfigs.ShouldHaveSingleItem();
        config.Name.ShouldBe("Series cover");
        config.ImageHosterRegistrationId.ShouldBe(imageHosterRegistration.Id);
        config.ReleaseId.ShouldBeNull();
        config.ReleaseCollectionId.ShouldBe(collection.Id);
    }

    [Test]
    public async Task CreateFromTemplateAsync_NameAndLanguageGiven_UsesNameAndNormalizedLanguageWithoutNotification()
    {
        // Arrange
        var seed = await AddReleaseTemplateAsync();
        var releaseFolderPath = CreateTemplateReleaseFolder("Bearcat.Release.1080p");

        // Act
        var result = await service.CreateFromTemplateAsync(
            releaseTemplateId: seed.ReleaseTemplateId,
            releaseFolderPath: $"  {releaseFolderPath}{Path.DirectorySeparatorChar}  ",
            name: "Custom.Release.Name",
            primaryLanguageCode: " DE ",
            cancellationToken: CancellationToken.None
        );

        // Assert
        dbContext.ChangeTracker.Clear();
        var release = await dbContext
            .Releases.Include(release => release.ArchiveConfigs)
            .SingleAsync(release => release.Id == result);

        release.Name.ShouldBe("Custom.Release.Name");
        release.ReleaseFolderPath.ShouldBe(releaseFolderPath);
        release.PrimaryLanguageCode.ShouldBe("de");
        release.MediaMetadataExtractedAt.ShouldNotBeNull();
        release.ArchiveConfigs.Single().ArchiveNamePrefix.ShouldBe("Custom.Release.Name");
        (await dbContext.Notifications.AnyAsync()).ShouldBeFalse();
    }

    [Test]
    public async Task CreateFromTemplateAsync_UnmanagedTemplate_CreatesReleaseNamedAfterFolderWithFixedArchive()
    {
        // Arrange
        var seed = await AddReleaseTemplateAsync(ReleaseType.Unmanaged);
        var releaseFolderPath = CreateTemplateReleaseFolder("Bearcat.Release.Unmanaged");
        await File.WriteAllTextAsync(Path.Combine(releaseFolderPath, "archive.part1.rar"), "1");
        await File.WriteAllTextAsync(Path.Combine(releaseFolderPath, "archive.part2.rar"), "2");

        // Act
        var result = await service.CreateFromTemplateAsync(
            seed.ReleaseTemplateId,
            releaseFolderPath,
            null,
            null,
            CancellationToken.None
        );

        // Assert
        dbContext.ChangeTracker.Clear();
        var release = await dbContext
            .Releases.AsSplitQuery()
            .Include(release => release.ArchiveConfigs)
                .ThenInclude(config => config.Archives)
                    .ThenInclude(archive => archive.ArchiveFiles)
            .SingleAsync(release => release.Id == result);
        var archiveConfig = release.ArchiveConfigs.Single();
        var archive = archiveConfig.Archives.Single();

        release.Name.ShouldBe("Bearcat.Release.Unmanaged");
        release.ReleaseType.ShouldBe(ReleaseType.Unmanaged);
        release.ReleaseFolderPath.ShouldBeNull();
        release.PrimaryLanguageCode.ShouldBeNull();
        archiveConfig.ArchiveFilesBasePath.ShouldBe(releaseFolderPath);
        archive.ArchiveFolderPath.ShouldBe(releaseFolderPath);
        archive.ArchiveState.ShouldBe(ArchiveState.Created);
        archive.ArchiveFiles.Count.ShouldBe(2);
        (await dbContext.Notifications.AnyAsync()).ShouldBeFalse();
    }

    [Test]
    public async Task CreateFromTemplateAsync_FolderDoesNotExist_ThrowsReleaseFolderNotFoundException()
    {
        // Arrange
        var seed = await AddReleaseTemplateAsync();
        var missingFolderPath = Path.Combine(tempRootPath, "Missing.Release.1080p");

        // Act
        var exception = await Should.ThrowAsync<ReleaseFolderNotFoundException>(() =>
            service.CreateFromTemplateAsync(
                seed.ReleaseTemplateId,
                missingFolderPath,
                null,
                null,
                CancellationToken.None
            )
        );

        // Assert
        exception.FolderPath.ShouldBe(missingFolderPath);
        (await dbContext.Releases.AnyAsync()).ShouldBeFalse();
    }

    [Test]
    public async Task CreateFromTemplateAsync_TemplateDoesNotExist_ThrowsReleaseTemplateNotFoundException()
    {
        // Arrange
        var releaseFolderPath = CreateTemplateReleaseFolder("Bearcat.Release.1080p");

        // Act
        var exception = await Should.ThrowAsync<ReleaseTemplateNotFoundException>(() =>
            service.CreateFromTemplateAsync(
                4711,
                releaseFolderPath,
                null,
                null,
                CancellationToken.None
            )
        );

        // Assert
        exception.ReleaseTemplateId.ShouldBe(4711);
        (await dbContext.Releases.AnyAsync()).ShouldBeFalse();
    }

    [Test]
    public async Task CreateFromTemplateAsync_FolderIsReleaseFolderOfRelease_ThrowsReleaseFolderAlreadyInUseException()
    {
        // Arrange
        var seed = await AddReleaseTemplateAsync();
        var releaseFolderPath = CreateTemplateReleaseFolder("Existing.Release.1080p");
        dbContext.Releases.Add(
            new Release
            {
                Name = "Existing.Release.1080p",
                ReleaseType = ReleaseType.Managed,
                ReleaseGroupId = seed.ReleaseGroupId,
                ReleaseFolderPath = releaseFolderPath,
            }
        );
        await dbContext.SaveChangesAsync();

        // Act
        var exception = await Should.ThrowAsync<ReleaseFolderAlreadyInUseException>(() =>
            service.CreateFromTemplateAsync(
                seed.ReleaseTemplateId,
                $"{releaseFolderPath}{Path.DirectorySeparatorChar}",
                null,
                null,
                CancellationToken.None
            )
        );

        // Assert
        exception.UsageKind.ShouldBe(ReleaseFolderUsageKind.ReleaseFolder);
        exception.FolderPath.ShouldBe(releaseFolderPath);
        (await dbContext.Releases.CountAsync()).ShouldBe(1);
    }

    [Test]
    public async Task CreateFromTemplateAsync_FolderIsArchiveFolderOfUnmanagedRelease_ThrowsReleaseFolderAlreadyInUseException()
    {
        // Arrange
        var seed = await AddReleaseTemplateAsync(ReleaseType.Unmanaged);
        var releaseFolderPath = CreateTemplateReleaseFolder("Bearcat.Release.Unmanaged");
        await File.WriteAllTextAsync(Path.Combine(releaseFolderPath, "archive.rar"), "1");
        await service.CreateFromTemplateAsync(
            seed.ReleaseTemplateId,
            releaseFolderPath,
            null,
            null,
            CancellationToken.None
        );

        // Act
        var exception = await Should.ThrowAsync<ReleaseFolderAlreadyInUseException>(() =>
            service.CreateFromTemplateAsync(
                seed.ReleaseTemplateId,
                releaseFolderPath,
                null,
                null,
                CancellationToken.None
            )
        );

        // Assert
        exception.UsageKind.ShouldBe(ReleaseFolderUsageKind.UnmanagedArchiveFolder);
        (await dbContext.Releases.CountAsync()).ShouldBe(1);
    }

    [Test]
    public async Task CreateFromTemplateAsync_FolderIsTargetOfRemoteDownload_ThrowsReleaseFolderAlreadyInUseException()
    {
        // Arrange
        var seed = await AddReleaseTemplateAsync();
        var releaseFolderPath = CreateTemplateReleaseFolder("Remote.Release.1080p");
        dbContext.RemoteSourceDownloads.Add(
            new RemoteSourceDownload
            {
                SourceName = "Main FTP",
                RemoteFolderPath = "/incoming/Remote.Release.1080p",
                FolderName = "Remote.Release.1080p",
                LocalFolderPath = releaseFolderPath,
                State = RemoteSourceDownloadState.Downloading,
            }
        );
        await dbContext.SaveChangesAsync();

        // Act
        var exception = await Should.ThrowAsync<ReleaseFolderAlreadyInUseException>(() =>
            service.CreateFromTemplateAsync(
                seed.ReleaseTemplateId,
                releaseFolderPath,
                null,
                null,
                CancellationToken.None
            )
        );

        // Assert
        exception.UsageKind.ShouldBe(ReleaseFolderUsageKind.RemoteDownloadFolder);
        (await dbContext.Releases.AnyAsync()).ShouldBeFalse();
    }

    [Test]
    public async Task CreateAsync_UnmanagedRelease_CreatesFixedArchiveConfigAndArchive()
    {
        // Arrange
        var releaseGroup = await AddReleaseGroupAsync("Unmanaged releases");
        var releaseFolderPath = CreateReleaseFolderWithArchives("Bearcat.Release.Unmanaged");

        // Act
        var result = await service.CreateAsync(
            "Bearcat.Release.Unmanaged",
            releaseFolderPath,
            ReleaseType.Unmanaged,
            ReleaseContentType.Movie,
            releaseGroup.Id,
            null,
            false,
            CancellationToken.None
        );

        // Assert
        var release = await dbContext
            .Releases.AsSplitQuery()
            .Include(r => r.ArchiveConfigs)
                .ThenInclude(c => c.Archives)
                    .ThenInclude(a => a.ArchiveFiles)
            .SingleAsync(r => r.Id == result);
        var archiveConfig = release.ArchiveConfigs.Single();
        var archive = archiveConfig.Archives.Single();

        release.ReleaseFolderPath.ShouldBeNull();
        archiveConfig.ArchiveFilesBasePath.ShouldBe(releaseFolderPath);
        archiveConfig.ArchiverName.ShouldBe("RarArchiver");
        archiveConfig.ArchiveFileSizeMb.ShouldBe(0);
        archive.ArchiveFolderPath.ShouldBe(releaseFolderPath);
        archive.ArchiveState.ShouldBe(ArchiveState.Created);
        archive.ArchiveFileSizeMb.ShouldBe(0);
        archive
            .ArchiveFiles.Select(f => Path.GetFileName(f.FullFileName))
            .ShouldBe([
                "Bearcat.Release.Unmanaged.part1.rar",
                "Bearcat.Release.Unmanaged.part2.rar",
            ]);
    }

    [Test]
    public async Task ConvertToUnmanagedAsync_AllArchivesCreated_SetsUnmanagedAndNullsFolderPath()
    {
        // Arrange
        var releaseGroup = await AddReleaseGroupAsync("Convert group");
        var release = await AddManagedReleaseWithArchiveAsync(
            releaseGroup.Id,
            releaseFolderPath: "/data/releases/Bearcat.Release",
            archiveFolderPath: "/data/archives/abc",
            archiveState: ArchiveState.Created
        );

        // Act
        await service.ConvertToUnmanagedAsync(release.Id, CancellationToken.None);

        // Assert
        dbContext.ChangeTracker.Clear();
        var result = await dbContext
            .Releases.Include(r => r.ArchiveConfigs)
                .ThenInclude(c => c.Archives)
            .SingleAsync(r => r.Id == release.Id);

        result.ReleaseType.ShouldBe(ReleaseType.Unmanaged);
        result.ReleaseFolderPath.ShouldBeNull();
        result
            .ArchiveConfigs.Single()
            .Archives.Single()
            .ArchiveFolderPath.ShouldBe("/data/archives/abc");
    }

    [Test]
    public async Task ConvertToUnmanagedAsync_ArchiveNotCreated_ThrowsInvalidOperationException()
    {
        // Arrange
        var releaseGroup = await AddReleaseGroupAsync("Convert group");
        var release = await AddManagedReleaseWithArchiveAsync(
            releaseGroup.Id,
            releaseFolderPath: "/data/releases/Bearcat.Release",
            archiveFolderPath: "/data/archives/abc",
            archiveState: ArchiveState.Creating
        );

        // Act
        var result = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await service.ConvertToUnmanagedAsync(release.Id, CancellationToken.None)
        );

        // Assert
        result.Message.ShouldBe(
            "All archive configs must have a created archive before the release can be converted to unmanaged."
        );
        dbContext.ChangeTracker.Clear();
        var unchanged = await dbContext.Releases.SingleAsync(r => r.Id == release.Id);
        unchanged.ReleaseType.ShouldBe(ReleaseType.Managed);
        unchanged.ReleaseFolderPath.ShouldBe("/data/releases/Bearcat.Release");
    }

    [Test]
    public async Task ConvertToUnmanagedAsync_UnmanagedRelease_ThrowsInvalidOperationException()
    {
        // Arrange
        var releaseGroup = await AddReleaseGroupAsync("Convert group");
        var releaseFolderPath = CreateReleaseFolderWithArchives("Bearcat.Release.Unmanaged");
        var releaseId = await service.CreateAsync(
            "Bearcat.Release.Unmanaged",
            releaseFolderPath,
            ReleaseType.Unmanaged,
            ReleaseContentType.Movie,
            releaseGroup.Id,
            null,
            false,
            CancellationToken.None
        );

        // Act
        var result = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await service.ConvertToUnmanagedAsync(releaseId, CancellationToken.None)
        );

        // Assert
        result.Message.ShouldBe("Only managed releases can be converted to unmanaged.");
    }

    [Test]
    public async Task ConvertToManagedAsync_UnmanagedRelease_SetsManagedAndAssignsFolder()
    {
        // Arrange
        var releaseGroup = await AddReleaseGroupAsync("Convert group");
        var archiveFolderPath = CreateReleaseFolderWithArchives("Bearcat.Release.Unmanaged");
        var releaseId = await service.CreateAsync(
            "Bearcat.Release.Unmanaged",
            archiveFolderPath,
            ReleaseType.Unmanaged,
            ReleaseContentType.Movie,
            releaseGroup.Id,
            null,
            false,
            CancellationToken.None
        );

        // Act
        await service.ConvertToManagedAsync(
            releaseId,
            "/data/releases/Bearcat.Release",
            CancellationToken.None
        );

        // Assert
        dbContext.ChangeTracker.Clear();
        var result = await dbContext
            .Releases.AsSplitQuery()
            .Include(r => r.ArchiveConfigs)
                .ThenInclude(c => c.Archives)
            .SingleAsync(r => r.Id == releaseId);

        result.ReleaseType.ShouldBe(ReleaseType.Managed);
        result.ReleaseFolderPath.ShouldBe("/data/releases/Bearcat.Release");
        result
            .ArchiveConfigs.Single()
            .Archives.Single()
            .ArchiveFolderPath.ShouldBe(archiveFolderPath);
    }

    [Test]
    public async Task ConvertToManagedAsync_ManagedRelease_ThrowsInvalidOperationException()
    {
        // Arrange
        var releaseGroup = await AddReleaseGroupAsync("Convert group");
        var release = await AddManagedReleaseWithArchiveAsync(
            releaseGroup.Id,
            releaseFolderPath: "/data/releases/Bearcat.Release",
            archiveFolderPath: "/data/archives/abc",
            archiveState: ArchiveState.Created
        );

        // Act
        var result = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await service.ConvertToManagedAsync(
                release.Id,
                "/data/releases/Bearcat.Release",
                CancellationToken.None
            )
        );

        // Assert
        result.Message.ShouldBe("Only unmanaged releases can be converted to managed.");
    }

    [Test]
    public async Task ConvertToManagedAsync_BlankFolder_ThrowsInvalidOperationException()
    {
        // Arrange
        var releaseGroup = await AddReleaseGroupAsync("Convert group");
        var archiveFolderPath = CreateReleaseFolderWithArchives("Bearcat.Release.Unmanaged");
        var releaseId = await service.CreateAsync(
            "Bearcat.Release.Unmanaged",
            archiveFolderPath,
            ReleaseType.Unmanaged,
            ReleaseContentType.Movie,
            releaseGroup.Id,
            null,
            false,
            CancellationToken.None
        );

        // Act
        var result = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await service.ConvertToManagedAsync(releaseId, "  ", CancellationToken.None)
        );

        // Assert
        result.Message.ShouldBe("A release folder must be assigned when converting to managed.");
        dbContext.ChangeTracker.Clear();
        var unchanged = await dbContext.Releases.SingleAsync(r => r.Id == releaseId);
        unchanged.ReleaseType.ShouldBe(ReleaseType.Unmanaged);
        unchanged.ReleaseFolderPath.ShouldBeNull();
    }

    [Test]
    public async Task GetUnmanagedConversionPreviewAsync_ArchiveInsideReleaseFolder_ReportsWarningAndCanConvert()
    {
        // Arrange
        var releaseGroup = await AddReleaseGroupAsync("Convert group");
        var release = await AddManagedReleaseWithArchiveAsync(
            releaseGroup.Id,
            releaseFolderPath: "/data/releases/Bearcat.Release",
            archiveFolderPath: "/data/releases/Bearcat.Release/archives",
            archiveState: ArchiveState.Created
        );

        // Act
        var preview = await service.GetUnmanagedConversionPreviewAsync(
            release.Id,
            CancellationToken.None
        );

        // Assert
        preview.CanConvert.ShouldBeTrue();
        preview.ArchivesInsideReleaseFolder.ShouldBeTrue();
        preview.ReleaseFolderPath.ShouldBe("/data/releases/Bearcat.Release");
    }

    [Test]
    public async Task GetUnmanagedConversionPreviewAsync_ArchiveOutsideReleaseFolder_NoWarning()
    {
        // Arrange
        var releaseGroup = await AddReleaseGroupAsync("Convert group");
        var release = await AddManagedReleaseWithArchiveAsync(
            releaseGroup.Id,
            releaseFolderPath: "/data/releases/Bearcat.Release",
            archiveFolderPath: "/data/archives/abc",
            archiveState: ArchiveState.Created
        );

        // Act
        var preview = await service.GetUnmanagedConversionPreviewAsync(
            release.Id,
            CancellationToken.None
        );

        // Assert
        preview.CanConvert.ShouldBeTrue();
        preview.ArchivesInsideReleaseFolder.ShouldBeFalse();
    }

    [Test]
    public async Task MarkUploadsPostedAsync_UploadsAlreadyPosted_UpdatesUploadsPostedAt()
    {
        // Arrange
        var uploadsPostedAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var releaseGroup = await AddReleaseGroupAsync("Managed releases");
        var release = await AddReleaseAsync(releaseGroup.Id);
        release.UploadsPostedAt = uploadsPostedAt;
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        // Act
        await service.MarkUploadsPostedAsync(release.Id, CancellationToken.None);

        // Assert
        dbContext.ChangeTracker.Clear();
        var updatedRelease = await dbContext.Releases.SingleAsync();
        updatedRelease.UploadsPostedAt.ShouldNotBeNull();
        updatedRelease.UploadsPostedAt.Value.ShouldBeGreaterThan(uploadsPostedAt);
    }

    private async Task<Release> AddManagedReleaseWithArchiveAsync(
        int releaseGroupId,
        string releaseFolderPath,
        string archiveFolderPath,
        ArchiveState archiveState
    )
    {
        var release = new Release
        {
            Name = "Bearcat.Release.Managed",
            CreatedAt = DateTime.UtcNow,
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = releaseFolderPath,
            ReleaseGroupId = releaseGroupId,
            ArchiveConfigs =
            [
                new ArchiveConfig
                {
                    Name = "RAR",
                    ArchiveFilesBasePath = archiveFolderPath,
                    ArchiverName = "RarArchiver",
                    ArchiveNamePrefix = "Bearcat.Release.Managed",
                    ArchivePassword = null,
                    ArchiveFileSizeMb = 0,
                    UploadConfigs = [],
                    Archives =
                    [
                        new Archive
                        {
                            ArchiveFolderPath = archiveFolderPath,
                            CreatedAt = DateTime.UtcNow,
                            ArchiveState = archiveState,
                            ArchiveFileSizeMb = 0,
                            ArchiveFiles =
                            [
                                new ArchiveFile
                                {
                                    FullFileName = Path.Combine(
                                        archiveFolderPath,
                                        "Bearcat.Release.Managed.part1.rar"
                                    ),
                                },
                            ],
                            Uploads = [],
                            ErrorMessages = [],
                            Notifications = [],
                        },
                    ],
                },
            ],
        };

        dbContext.Releases.Add(release);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        return release;
    }

    private async Task<ReleaseGroup> AddReleaseGroupAsync(string name)
    {
        var releaseGroup = new ReleaseGroup
        {
            Name = name,
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
        };

        dbContext.ReleaseGroups.Add(releaseGroup);
        await dbContext.SaveChangesAsync();

        return releaseGroup;
    }

    private async Task<Release> AddReleaseAsync(
        int releaseGroupId,
        string name = "Bearcat.Release.001"
    )
    {
        var release = new Release
        {
            Name = name,
            CreatedAt = DateTime.UtcNow,
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = "/tmp/release",
            ReleaseGroupId = releaseGroupId,
        };

        dbContext.Releases.Add(release);
        await dbContext.SaveChangesAsync();

        return release;
    }

    private async Task<ReleaseTemplateSeed> AddReleaseTemplateAsync(
        ReleaseType releaseType = ReleaseType.Managed
    )
    {
        var releaseGroup = await AddReleaseGroupAsync("Template group");
        var hosterRegistration = new HosterRegistration
        {
            Name = "Primary hoster",
            SerializedConfig = "{}",
            HosterClassName = "TestHoster",
            IsActive = true,
        };
        var linkCrypterRegistration = new LinkCrypterRegistration
        {
            Name = "Main crypter",
            LinkCrypterClassName = "TestCrypter",
            SerializedConfig = "{}",
            IsActive = true,
        };
        var releaseTemplate = new ReleaseTemplate
        {
            Name = "Managed template",
            ReleaseType = releaseType,
            ReleaseContentType = ReleaseContentType.TvShowEpisode,
            ReleaseGroup = releaseGroup,
            ArchiveConfigTemplates =
            [
                new ArchiveConfigTemplate
                {
                    Name = "RAR Forum A",
                    ArchiveFilesBasePath = "/tmp/archives",
                    ArchiverName = "rar",
                    ArchivePassword = "archive-secret",
                    ArchiveFileSizeMb = 1024,
                    UseReleaseNameAsArchiveName = true,
                },
            ],
        };
        releaseTemplate.UploadConfigTemplates =
        [
            new UploadConfigTemplate
            {
                ReleaseTemplate = releaseTemplate,
                ArchiveConfigTemplate = releaseTemplate.ArchiveConfigTemplates.Single(),
                HosterRegistration = hosterRegistration,
                Name = null,
                PremiumOnlyDownload = true,
                LinkCrypterTemplates =
                [
                    new UploadConfigLinkCrypterTemplate
                    {
                        LinkCrypterRegistration = linkCrypterRegistration,
                        Password = "container-secret",
                    },
                ],
            },
        ];

        dbContext.ReleaseTemplates.Add(releaseTemplate);
        await dbContext.SaveChangesAsync();

        return new ReleaseTemplateSeed(
            releaseTemplate.Id,
            releaseGroup.Id,
            hosterRegistration.Id,
            linkCrypterRegistration.Id
        );
    }

    private string CreateTemplateReleaseFolder(string folderName)
    {
        return Directory.CreateDirectory(Path.Combine(tempRootPath, folderName)).FullName;
    }

    private static ReleaseFromFolderCreationService CreateReleaseFromFolderCreationService(
        BearcatDbContext dbContext,
        IArchiverFactory archiverFactory,
        TimeProvider timeProvider
    )
    {
        var nfoDatabaseFactory = new Mock<INfoDatabaseFactory>(MockBehavior.Strict);
        nfoDatabaseFactory
            .Setup(factory => factory.GetByClassName())
            .Returns(new Dictionary<string, INfoDatabase>());
        var releaseInfoRepository = new ReleaseInfoRepository(
            dbContext,
            dbContext,
            NoOpSecretProtector.Instance
        );
        var classificationService = new ReleaseClassificationService(
            new ReleaseClassificationRepository(dbContext),
            timeProvider,
            NullLogger<ReleaseClassificationService>.Instance
        );
        var mediaMetadataExtractor = new Mock<IMediaMetadataExtractor>();
        mediaMetadataExtractor
            .Setup(extractor =>
                extractor.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((MediaProbeResult?)null);

        return new ReleaseFromFolderCreationService(
            releaseInfoResolutionService: new ReleaseInfoResolutionService(
                releaseInfoRepository,
                nfoDatabaseFactory.Object,
                new ReleaseNfoResolver(
                    nfoDatabaseFactory.Object,
                    NullLogger<ReleaseNfoResolver>.Instance
                ),
                new ReleaseInfoResolver(
                    releaseInfoRepository,
                    nfoDatabaseFactory.Object,
                    NullLogger<ReleaseInfoResolver>.Instance
                ),
                new ReleaseMetadataResolver(
                    new MediaMetadataResolver(
                        new MediaMetadataResolverRepository(
                            dbContext,
                            NoOpSecretProtector.Instance
                        ),
                        new Mock<IMediaMetadataDatabaseFactory>(MockBehavior.Strict).Object,
                        NullLogger<MediaMetadataResolver>.Instance
                    ),
                    nfoDatabaseFactory.Object,
                    NullLogger<ReleaseMetadataResolver>.Instance
                ),
                classificationService,
                NullLogger<ReleaseInfoResolutionService>.Instance,
                timeProvider
            ),
            mediaMetadataService: new MediaMetadataService(
                new MediaMetadataRepository(dbContext),
                mediaMetadataExtractor.Object,
                new FileSystemService(),
                classificationService,
                timeProvider,
                NullLogger<MediaMetadataService>.Instance
            ),
            archiverFactory: archiverFactory,
            releaseCollectionAssigner: new ReleaseCollectionAssignmentService(
                new ReleaseCollectionRepository(
                    dbRead: dbContext,
                    dbWrite: dbContext,
                    metadataDatabaseFactory: Mock.Of<IMediaMetadataDatabaseFactory>()
                ),
                timeProvider
            )
        );
    }

    private static string CreateReleaseFolderWithArchives(string releaseName)
    {
        return CreateReleaseFolderWithFiles($"{releaseName}.part1.rar", $"{releaseName}.part2.rar");
    }

    private static string CreateReleaseFolderWithFiles(params string[] fileNames)
    {
        var releaseFolderPath = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "release-service-test",
            Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(releaseFolderPath);

        foreach (var fileName in fileNames)
        {
            File.WriteAllText(Path.Combine(releaseFolderPath, fileName), fileName);
        }

        return releaseFolderPath;
    }

    private sealed record ReleaseTemplateSeed(
        int ReleaseTemplateId,
        int ReleaseGroupId,
        int HosterRegistrationId,
        int LinkCrypterRegistrationId
    );

    private static TimeProvider CreateTimeProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["LocalTimezone"] = "UTC" })
            .Build();

        return new TimeProvider(configuration);
    }
}
