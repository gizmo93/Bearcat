using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Results;
using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Abstractions.LinkCrypter.Results;
using Bearcat.Abstractions.Media;
using Bearcat.Abstractions.MediaMetadataDatabase;
using Bearcat.Abstractions.NfoDatabase;
using Bearcat.Domain.Entities;
using Bearcat.Domain.IntegrationTest.Shared.UnmanagedReleases;
using Bearcat.Domain.Shared;
using Bearcat.Domain.Shared.CollectionAssignment;
using Bearcat.Domain.Shared.MediaMetadataResolution;
using Bearcat.Domain.Shared.UnmanagedReleases;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.Creation;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Dto;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Importing;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Preview;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Reading;
using Bearcat.Domain.UseCases.ManageNotifications;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.UseCases.ManageReleases.ReleaseInfoResolution;
using Bearcat.Domain.UseCases.ManageReleaseTemplates;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.FileSystem;
using Bearcat.Infrastructure.Security;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ImportLinkCrypterContainers;

public class LinkCrypterImportProcessingServiceTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private const string LinkCrypterClassName = "TestCrypter";
    private const string FirstHosterClassName = "FirstHoster";
    private const string SecondHosterClassName = "SecondHoster";
    private const string ThirdHosterClassName = "ThirdHoster";
    private const string ReleaseName = "Bearcat.Movie.2026.German.1080p.WEB.x264-GRP";
    private const string OtherReleaseName = "Bearcat.Other.2026.German.1080p.WEB.x264-GRP";
    private const string ThirdReleaseName = "Bearcat.Third.2026.German.1080p.WEB.x264-GRP";
    private const string FirstArchiveFileName = "Bearcat.Movie.part1.rar";
    private const string SecondArchiveFileName = "Bearcat.Movie.part2.rar";

    private static readonly DateTime StartTime = new(
        2026,
        11,
        2,
        12,
        0,
        0,
        DateTimeKind.Unspecified
    );

    private readonly Dictionary<string, ReadContainerLinksResult> linksPerContainerUrl = new();
    private readonly Dictionary<string, HostedFileInfo> fileInfosPerLink = new();
    private readonly List<ListedContainer> listedContainers = [];

    private string tempRootPath = null!;
    private string workingDirectoryPath = null!;
    private string archiveFilesBasePath = null!;
    private Mock<ILinkCrypterWithContainerReading> linkCrypterMock = null!;
    private Mock<ILinkCrypterFactory> linkCrypterFactoryMock = null!;
    private Dictionary<string, Mock<IHosterWithFileInfo>> hosterMocksByClassName = null!;
    private Mock<IHosterFactory> hosterFactoryMock = null!;

    [SetUp]
    public void Setup()
    {
        tempRootPath = Path.Combine(Path.GetTempPath(), $"bearcat-tests-{Guid.NewGuid():N}");
        workingDirectoryPath = Directory
            .CreateDirectory(Path.Combine(tempRootPath, "working"))
            .FullName;
        archiveFilesBasePath = Directory
            .CreateDirectory(Path.Combine(tempRootPath, "restored-archives"))
            .FullName;

        linkCrypterMock = new Mock<ILinkCrypterWithContainerReading>();
        linkCrypterMock
            .Setup(crypter => crypter.DeserializeConfig(It.IsAny<string>()))
            .Returns(Mock.Of<ILinkCrypterConfig>());
        linkCrypterMock
            .Setup(crypter =>
                crypter.ListContainersAsync(
                    It.IsAny<ILinkCrypterConfig>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(() => new ListContainersResult(true, listedContainers.ToList(), []));
        linkCrypterMock
            .Setup(crypter =>
                crypter.ReadContainerLinksAsync(
                    It.IsAny<ILinkCrypterConfig>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                (ILinkCrypterConfig _, string containerUrl, string? _, CancellationToken _) =>
                    linksPerContainerUrl[containerUrl]
            );
        linkCrypterFactoryMock = new Mock<ILinkCrypterFactory>();
        linkCrypterFactoryMock
            .Setup(factory => factory.Get(LinkCrypterClassName))
            .Returns(linkCrypterMock.Object);

        hosterMocksByClassName = new Dictionary<string, Mock<IHosterWithFileInfo>>
        {
            [FirstHosterClassName] = CreateHosterMock(),
            [SecondHosterClassName] = CreateHosterMock(),
            [ThirdHosterClassName] = CreateHosterMock(),
        };
        var hostersByHostName = new Dictionary<string, IHoster>
        {
            ["first.test"] = hosterMocksByClassName[FirstHosterClassName].Object,
            ["second.test"] = hosterMocksByClassName[SecondHosterClassName].Object,
            ["third.test"] = hosterMocksByClassName[ThirdHosterClassName].Object,
        };
        hosterFactoryMock = new Mock<IHosterFactory>();
        hosterFactoryMock
            .Setup(factory => factory.GetHostersByName())
            .Returns(
                hosterMocksByClassName.ToDictionary(
                    entry => entry.Key,
                    entry => (IHoster)entry.Value.Object
                )
            );
        hosterFactoryMock
            .Setup(factory => factory.GetByName(It.IsAny<string>()))
            .Returns((string hosterClassName) => hosterMocksByClassName[hosterClassName].Object);
        hosterFactoryMock
            .Setup(factory => factory.FindHosterByFileLink(It.IsAny<string>()))
            .Returns(
                (string fileLink) => hostersByHostName.GetValueOrDefault(new Uri(fileLink).Host)
            );
    }

    [TearDown]
    public void DeleteTempFolder()
    {
        if (Directory.Exists(tempRootPath))
        {
            Directory.Delete(tempRootPath, recursive: true);
        }
    }

    [Test]
    public async Task ProcessAsync_ContainersRead_MarksKnownContainersAndWaitsForHosterRegistrations()
    {
        // Arrange
        var seed = await AddReleaseTemplateAsync(ReleaseType.Unmanaged);
        await AddExistingLinkCrypterContainerAsync(seed, "https://crypter.test/existing");
        AddListedContainer(
            "https://crypter.test/new",
            ReleaseName,
            [OnlineArchiveLink("https://first.test/a1", FirstArchiveFileName)],
            [OnlineArchiveLink("https://second.test/b1", FirstArchiveFileName)]
        );
        AddListedContainer("https://crypter.test/existing", "Other.Release", []);
        var linkCrypterImportId = await StartImportAsync(seed);

        // Act
        await ProcessAsync();

        // Assert
        var linkCrypterImport = await LoadImportAsync(linkCrypterImportId);
        linkCrypterImport.State.ShouldBe(LinkCrypterImportState.WaitingForHosterRegistrations);
        linkCrypterImport
            .Containers.OrderBy(container => container.Id)
            .Select(container => container.Status)
            .ShouldBe([
                LinkCrypterImportContainerStatus.WaitingForFileCheck,
                LinkCrypterImportContainerStatus.AlreadyInBearcat,
            ]);
        linkCrypterImport
            .HosterRegistrations.OrderBy(choice => choice.HosterClassName)
            .Select(choice => (choice.HosterClassName, choice.HosterRegistrationId))
            .ShouldBe([
                (FirstHosterClassName, seed.FirstHosterRegistrationId),
                (SecondHosterClassName, seed.SecondHosterRegistrationId),
            ]);
        VerifyFileInfosWereNeverRequested(FirstHosterClassName);
        VerifyFileInfosWereNeverRequested(SecondHosterClassName);
    }

    [Test]
    public async Task ProcessAsync_FileCheckFails_ReturnsToHosterRegistrationChoiceWithError()
    {
        // Arrange
        var seed = await AddReleaseTemplateAsync(ReleaseType.Unmanaged);
        AddListedContainer(
            "https://crypter.test/container",
            ReleaseName,
            [OnlineArchiveLink("https://first.test/a1", FirstArchiveFileName)]
        );
        hosterMocksByClassName[FirstHosterClassName]
            .Setup(hoster =>
                hoster.GetFileInfosAsync(
                    It.IsAny<IHosterConfig>(),
                    It.IsAny<IReadOnlyList<string>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new FileInfoResult(
                    IsSuccess: false,
                    ErrorMessages: ["Quota exceeded"],
                    FileInfoPerFileLink: new Dictionary<string, HostedFileInfo>()
                )
            );
        var linkCrypterImportId = await StartImportAsync(seed);
        await ProcessAsync();
        await ChooseDefaultHosterRegistrationsAsync(linkCrypterImportId, seed);

        // Act
        await ProcessAsync();

        // Assert
        var linkCrypterImport = await LoadImportAsync(linkCrypterImportId);
        linkCrypterImport.State.ShouldBe(LinkCrypterImportState.WaitingForHosterRegistrations);
        linkCrypterImport.ErrorMessages.ShouldHaveSingleItem().ShouldContain("Quota exceeded");
        linkCrypterImport
            .Containers.ShouldHaveSingleItem()
            .Status.ShouldBe(LinkCrypterImportContainerStatus.WaitingForFileCheck);
    }

    [Test]
    public async Task ProcessAsync_FilesChecked_DecidesMirrorsAndNotifiesReadyForReview()
    {
        // Arrange
        var seed = await AddReleaseTemplateAsync(ReleaseType.Unmanaged);
        AddListedContainer(
            "https://crypter.test/first",
            ReleaseName,
            [
                OnlineArchiveLink("https://first.test/a1", FirstArchiveFileName),
                OnlineArchiveLink("https://first.test/a2", SecondArchiveFileName),
            ],
            [
                OnlineArchiveLink("https://second.test/b1", FirstArchiveFileName),
                OfflineLink("https://second.test/b2"),
            ],
            ["https://unknown.test/c1"],
            ["https://third.test/d1"]
        );
        AddListedContainer(
            "https://crypter.test/second",
            OtherReleaseName,
            [
                OnlineArchiveLink("https://first.test/e1", FirstArchiveFileName),
                OnlineArchiveLink("https://first.test/e2", SecondArchiveFileName),
            ],
            [OnlineArchiveLink("https://second.test/f1", FirstArchiveFileName)]
        );
        var linkCrypterImportId = await StartImportAsync(seed);
        await ProcessAsync();
        await ChooseDefaultHosterRegistrationsAsync(linkCrypterImportId, seed);

        // Act
        await ProcessAsync();

        // Assert
        var linkCrypterImport = await LoadImportAsync(linkCrypterImportId);
        linkCrypterImport.State.ShouldBe(LinkCrypterImportState.ReadyForReview);
        var containers = linkCrypterImport.Containers.OrderBy(container => container.Id).ToList();
        containers.ShouldAllBe(container =>
            container.Status == LinkCrypterImportContainerStatus.Importable && container.IsSelected
        );
        GetMirrorDecisions(containers[0])
            .ShouldBe([
                (LinkCrypterImportMirrorDecision.Adopt, null),
                (
                    LinkCrypterImportMirrorDecision.UploadByBearcat,
                    LinkCrypterImportUploadByBearcatReason.SomeFilesOffline
                ),
                (LinkCrypterImportMirrorDecision.UnsupportedHoster, null),
                (LinkCrypterImportMirrorDecision.HosterNotInRelease, null),
            ]);
        GetMirrorDecisions(containers[1])
            .ShouldBe([
                (LinkCrypterImportMirrorDecision.Adopt, null),
                (
                    LinkCrypterImportMirrorDecision.UploadByBearcat,
                    LinkCrypterImportUploadByBearcatReason.FileNamesDifferFromFirstAdoptedMirror
                ),
            ]);
        VerifyFileInfosWereNeverRequested(ThirdHosterClassName);
        (await CreateDbContext().Notifications.SingleAsync()).NotificationKind.ShouldBe(
            NotificationKind.LinkCrypterImportReadyForReview
        );
    }

    [Test]
    public async Task ProcessAsync_OnlyFilesThatAreNoArchiveParts_IsNotImportableWithoutReleaseFolder()
    {
        // Arrange
        var seed = await AddReleaseTemplateAsync(ReleaseType.Unmanaged);
        AddListedContainer(
            "https://crypter.test/container",
            ReleaseName,
            [OnlineArchiveLink("https://first.test/a1", "Bearcat.Movie.mkv")]
        );
        var linkCrypterImportId = await StartImportAsync(seed);
        await ProcessAsync();
        await ChooseDefaultHosterRegistrationsAsync(linkCrypterImportId, seed);

        // Act
        await ProcessAsync();

        // Assert
        var container = (await LoadImportAsync(linkCrypterImportId)).Containers.Single();
        container.Status.ShouldBe(LinkCrypterImportContainerStatus.NotImportable);
        container.IsSelected.ShouldBeFalse();
        container.NotImportableReason.ShouldBe(
            LinkCrypterImportNotImportableReason.NoAdoptedMirrorAndNoReleaseFolder
        );
        GetMirrorDecisions(container)
            .ShouldBe([
                (
                    LinkCrypterImportMirrorDecision.UploadByBearcat,
                    LinkCrypterImportUploadByBearcatReason.FileNamesAreNotArchiveParts
                ),
            ]);
    }

    [Test]
    public async Task ProcessAsync_TwoContainersWithSameNameForDifferentHosters_ImportsBothIntoOneRelease()
    {
        // Arrange
        var seed = await AddReleaseTemplateAsync(ReleaseType.Unmanaged);
        AddListedContainer(
            "https://crypter.test/first",
            ReleaseName,
            [
                OnlineArchiveLink("https://first.test/a1", FirstArchiveFileName),
                OnlineArchiveLink("https://first.test/a2", SecondArchiveFileName),
            ]
        );
        AddListedContainer(
            "https://crypter.test/second",
            $" {ReleaseName.ToLowerInvariant()} ",
            [
                OnlineArchiveLink("https://second.test/b1", FirstArchiveFileName),
                OnlineArchiveLink("https://second.test/b2", SecondArchiveFileName),
            ]
        );

        // Act
        var linkCrypterImportId = await RunWholeImportAsync(seed);

        // Assert
        var release = await LoadReleaseAsync(ReleaseName);
        var containers = (await LoadImportAsync(linkCrypterImportId))
            .Containers.OrderBy(container => container.Id)
            .ToList();
        containers
            .Select(container =>
                (
                    container.Status,
                    container.ImportedReleaseId,
                    LinkCrypterImportContainerPreview.FromJson(container.PreviewJson).ReleaseAction
                )
            )
            .ShouldBe([
                (
                    LinkCrypterImportContainerStatus.Imported,
                    release.Id,
                    LinkCrypterImportReleaseAction.CreateRelease
                ),
                (
                    LinkCrypterImportContainerStatus.Imported,
                    release.Id,
                    LinkCrypterImportReleaseAction.AttachToReleaseOfEarlierContainer
                ),
            ]);
        var archive = release.ArchiveConfigs.ShouldHaveSingleItem().Archives.ShouldHaveSingleItem();
        release.UploadConfigs.Count.ShouldBe(2);
        foreach (var uploadConfig in release.UploadConfigs)
        {
            var upload = uploadConfig.Uploads.ShouldHaveSingleItem();
            upload.ArchiveId.ShouldBe(archive.Id);
            var linkCrypter = uploadConfig.LinkCrypters.ShouldHaveSingleItem();
            linkCrypter.ContainerScope.ShouldBe(LinkCrypterContainerScope.Release);
            linkCrypter.LinkCrypterContainers.ShouldHaveSingleItem().UploadId.ShouldBe(upload.Id);
        }
        release.LinkCrypterContainers.ShouldBeEmpty();
    }

    [Test]
    public async Task ProcessAsync_ContainerOfSecondCrypterForExistingRelease_LinksUploadWithSameLinksAndAddsOtherHosterWithoutSourceUpload()
    {
        // Arrange
        var seed = await AddReleaseTemplateAsync(ReleaseType.Unmanaged);
        AddListedContainer(
            "https://crypter.test/first",
            ReleaseName,
            [
                OnlineArchiveLink("https://first.test/a1", FirstArchiveFileName),
                OnlineArchiveLink("https://first.test/a2", SecondArchiveFileName),
            ],
            [
                OnlineArchiveLink("https://second.test/b1", FirstArchiveFileName),
                OnlineArchiveLink("https://second.test/b2", SecondArchiveFileName),
            ]
        );
        await RunWholeImportAsync(seed);
        var secondLinkCrypterRegistrationId = await AddLinkCrypterRegistrationAsync();
        listedContainers.Clear();
        AddListedContainer(
            "https://crypter.test/second-crypter",
            ReleaseName,
            ["https://first.test/a1", "https://first.test/a2"],
            [
                OnlineArchiveLink("https://second.test/c1", FirstArchiveFileName),
                OnlineArchiveLink("https://second.test/c2", SecondArchiveFileName),
            ]
        );

        // Act
        var linkCrypterImportId = await RunWholeImportAsync(seed, secondLinkCrypterRegistrationId);

        // Assert
        var release = await LoadReleaseAsync(ReleaseName);
        var importContainer = (await LoadImportAsync(linkCrypterImportId)).Containers.Single();
        importContainer.Status.ShouldBe(LinkCrypterImportContainerStatus.Imported);
        importContainer.ImportedReleaseId.ShouldBe(release.Id);
        LinkCrypterImportContainerPreview
            .FromJson(importContainer.PreviewJson)
            .ReleaseAction.ShouldBe(LinkCrypterImportReleaseAction.AttachToExistingRelease);
        var firstUpload = release
            .UploadConfigs.Single(uploadConfig =>
                uploadConfig.HosterRegistrationId == seed.FirstHosterRegistrationId
            )
            .Uploads.ShouldHaveSingleItem();
        release.UploadConfigs.SelectMany(uploadConfig => uploadConfig.Uploads).Count().ShouldBe(2);
        release
            .UploadConfigs.SelectMany(uploadConfig => uploadConfig.LinkCrypters)
            .Where(linkCrypter =>
                linkCrypter.LinkCrypterRegistrationId == secondLinkCrypterRegistrationId
            )
            .Select(linkCrypter => linkCrypter.ContainerScope)
            .ShouldBe([
                LinkCrypterContainerScope.ReleaseAllHosters,
                LinkCrypterContainerScope.ReleaseAllHosters,
            ]);
        var container = release.LinkCrypterContainers.Single(container =>
            container.LinkCrypterRegistrationId == secondLinkCrypterRegistrationId
        );
        container.ContainerUrl.ShouldBe("https://crypter.test/second-crypter");
        container.SourceUploads.Select(source => source.UploadId).ShouldBe([firstUpload.Id]);
    }

    [Test]
    public async Task ProcessAsync_ReleaseHasContainerOfSameCrypterForHoster_FailsThatContainerAndImportsTheOthers()
    {
        // Arrange
        var seed = await AddReleaseTemplateAsync(ReleaseType.Unmanaged);
        AddListedContainer(
            "https://crypter.test/first",
            ReleaseName,
            [OnlineArchiveLink("https://first.test/a1", FirstArchiveFileName)]
        );
        await RunWholeImportAsync(seed);
        listedContainers.Clear();
        AddListedContainer(
            "https://crypter.test/again",
            ReleaseName,
            [OnlineArchiveLink("https://first.test/b1", FirstArchiveFileName)]
        );
        AddListedContainer(
            "https://crypter.test/other",
            OtherReleaseName,
            [OnlineArchiveLink("https://second.test/c1", FirstArchiveFileName)]
        );

        // Act
        var linkCrypterImportId = await RunWholeImportAsync(seed);

        // Assert
        var containers = (await LoadImportAsync(linkCrypterImportId))
            .Containers.OrderBy(container => container.Id)
            .ToList();
        containers
            .Select(container => container.Status)
            .ShouldBe([
                LinkCrypterImportContainerStatus.ImportFailed,
                LinkCrypterImportContainerStatus.Imported,
            ]);
        containers[0].ErrorMessages.ShouldHaveSingleItem().ShouldContain(ReleaseName);
        var release = await LoadReleaseAsync(ReleaseName);
        release
            .UploadConfigs.SelectMany(uploadConfig => uploadConfig.Uploads)
            .ShouldHaveSingleItem()
            .UploadedFiles.ShouldHaveSingleItem()
            .HosterFileLink.ShouldBe("https://first.test/a1");
        release
            .UploadConfigs.SelectMany(uploadConfig => uploadConfig.LinkCrypters)
            .SelectMany(linkCrypter => linkCrypter.LinkCrypterContainers)
            .ShouldHaveSingleItem()
            .ContainerUrl.ShouldBe("https://crypter.test/first");
    }

    [Test]
    public async Task ProcessAsync_SingleHosterContainerWithReleaseFolder_ImportsManagedReleaseWithReleaseContainer()
    {
        // Arrange
        var seed = await AddReleaseTemplateAsync(ReleaseType.Managed);
        var releaseFolderPath = Directory
            .CreateDirectory(Path.Combine(workingDirectoryPath, ReleaseName))
            .FullName;
        AddListedContainer(
            "https://crypter.test/container",
            ReleaseName,
            [
                OnlineArchiveLink("https://first.test/a1", FirstArchiveFileName),
                OnlineArchiveLink("https://first.test/a2", SecondArchiveFileName),
            ]
        );

        // Act
        var linkCrypterImportId = await RunWholeImportAsync(
            seed: seed,
            primaryLanguageCode: " DE "
        );

        // Assert
        var linkCrypterImport = await LoadImportAsync(linkCrypterImportId);
        linkCrypterImport.State.ShouldBe(LinkCrypterImportState.Completed);
        linkCrypterImport
            .Containers.Single()
            .Status.ShouldBe(LinkCrypterImportContainerStatus.Imported);

        var release = await LoadReleaseAsync(ReleaseName);
        release.ReleaseType.ShouldBe(ReleaseType.Managed);
        release.ReleaseFolderPath.ShouldBe(releaseFolderPath);
        release.PrimaryLanguageCode.ShouldBe("de");
        release.UploadsPostedAt.ShouldBe(StartTime);
        var archive = release.ArchiveConfigs.ShouldHaveSingleItem().Archives.ShouldHaveSingleItem();
        archive.ArchiveState.ShouldBe(ArchiveState.Deleted);

        var firstUploadConfig = release.UploadConfigs.Single(uploadConfig =>
            uploadConfig.HosterRegistrationId == seed.FirstHosterRegistrationId
        );
        var upload = firstUploadConfig.Uploads.ShouldHaveSingleItem();
        upload.UploadState.ShouldBe(UploadState.Completed);
        upload.OnlineState.ShouldBe(OnlineState.Online);
        upload.UploadedFiles.ShouldAllBe(file => file.OnlineState == OnlineState.Online);
        upload.UploadedAt.ShouldBe(StartTime);
        upload.HosterRegistrationId.ShouldBe(seed.FirstHosterRegistrationId);
        upload.ArchiveId.ShouldBe(archive.Id);
        upload
            .UploadedFiles.OrderBy(file => file.HosterFileLink)
            .Select(file => (file.HosterFileLink, Path.GetFileName(file.ArchiveFile!.FullFileName)))
            .ShouldBe([
                ("https://first.test/a1", FirstArchiveFileName),
                ("https://first.test/a2", SecondArchiveFileName),
            ]);
        release
            .UploadConfigs.Single(uploadConfig =>
                uploadConfig.HosterRegistrationId == seed.SecondHosterRegistrationId
            )
            .Uploads.ShouldBeEmpty();

        var linkCrypter = firstUploadConfig.LinkCrypters.ShouldHaveSingleItem();
        linkCrypter.ContainerScope.ShouldBe(LinkCrypterContainerScope.Release);
        var container = linkCrypter.LinkCrypterContainers.ShouldHaveSingleItem();
        container.Scope.ShouldBe(LinkCrypterContainerScope.Release);
        container.UploadId.ShouldBe(upload.Id);
        container.ContainerUrl.ShouldBe("https://crypter.test/container");
        container.ExternalReference.ShouldBe("reference-https://crypter.test/container");
        container.State.ShouldBe(LinkCrypterContainerState.Created);
        container.Password.ShouldBe("template-secret");
    }

    [Test]
    public async Task ProcessAsync_MultiHosterContainerWithoutReleaseFolder_ImportsUnmanagedReleaseWithDeletedArchiveAndAllHostersContainer()
    {
        // Arrange
        var seed = await AddReleaseTemplateAsync(ReleaseType.Unmanaged);
        await AddThirdHosterUploadConfigTemplateAsync(seed);
        AddListedContainer(
            "https://crypter.test/container",
            ReleaseName,
            [
                OnlineArchiveLink("https://first.test/a1", FirstArchiveFileName),
                OnlineArchiveLink("https://first.test/a2", SecondArchiveFileName),
            ],
            [
                OnlineArchiveLink("https://second.test/b1", FirstArchiveFileName),
                OnlineArchiveLink("https://second.test/b2", SecondArchiveFileName),
            ]
        );

        // Act
        await RunWholeImportAsync(seed);

        // Assert
        var release = await LoadReleaseAsync(ReleaseName);
        release.ReleaseType.ShouldBe(ReleaseType.Unmanaged);
        release.ReleaseFolderPath.ShouldBeNull();
        release.ReleaseInfoCheckedAt.ShouldBeNull();
        var archiveConfig = release.ArchiveConfigs.ShouldHaveSingleItem();
        archiveConfig.ArchiveFilesBasePath.ShouldBe(archiveFilesBasePath);
        var archive = archiveConfig.Archives.ShouldHaveSingleItem();
        archive.ArchiveState.ShouldBe(ArchiveState.Deleted);
        archive
            .ArchiveFiles.Select(file => Path.GetFileName(file.FullFileName))
            .Order()
            .ShouldBe([FirstArchiveFileName, SecondArchiveFileName]);

        var uploads = release
            .UploadConfigs.SelectMany(uploadConfig => uploadConfig.Uploads)
            .ToList();
        uploads.Count.ShouldBe(2);
        uploads.ShouldAllBe(upload => upload.ArchiveId == archive.Id);
        release
            .UploadConfigs.SelectMany(uploadConfig => uploadConfig.LinkCrypters)
            .Select(linkCrypter => (linkCrypter.ContainerScope, linkCrypter.Password))
            .ShouldBe(
                [
                    (LinkCrypterContainerScope.ReleaseAllHosters, "template-secret"),
                    (LinkCrypterContainerScope.ReleaseAllHosters, null),
                    (LinkCrypterContainerScope.ReleaseAllHosters, null),
                ],
                ignoreOrder: true
            );
        var container = release.LinkCrypterContainers.ShouldHaveSingleItem();
        container.Scope.ShouldBe(LinkCrypterContainerScope.ReleaseAllHosters);
        container.ContainerUrl.ShouldBe("https://crypter.test/container");
        container.Password.ShouldBe("template-secret");
        container
            .SourceUploads.Select(source => source.UploadId)
            .Order()
            .ShouldBe(uploads.Select(upload => upload.Id).Order());
    }

    [Test]
    public async Task ProcessAsync_ArchivePartsInSubfolderOfArchiveStorageFolder_ImportsUnmanagedReleaseWithThisArchive()
    {
        // Arrange
        var seed = await AddReleaseTemplateAsync(ReleaseType.Unmanaged);
        var archiveStorageFolderPath = Directory
            .CreateDirectory(Path.Combine(tempRootPath, "storage"))
            .FullName;
        var archiveFolderPath = CreateArchivePartFiles(
            Path.Combine(archiveStorageFolderPath, ReleaseName)
        );
        var archiveStorageFolderId = await AddArchiveStorageFolderAsync(archiveStorageFolderPath);
        AddListedContainer(
            "https://crypter.test/container",
            ReleaseName,
            [
                OnlineArchiveLink("https://first.test/a1", FirstArchiveFileName),
                OnlineArchiveLink("https://first.test/a2", SecondArchiveFileName),
            ]
        );

        // Act
        var linkCrypterImportId = await RunWholeImportAsync(seed);

        // Assert
        var container = (await LoadImportAsync(linkCrypterImportId)).Containers.Single();
        container.Status.ShouldBe(LinkCrypterImportContainerStatus.Imported);
        LinkCrypterImportContainerPreview
            .FromJson(container.PreviewJson)
            .ArchiveFolderPath.ShouldBe(archiveFolderPath);

        var release = await LoadReleaseAsync(ReleaseName);
        release.ReleaseType.ShouldBe(ReleaseType.Unmanaged);
        var archiveConfig = release.ArchiveConfigs.ShouldHaveSingleItem();
        archiveConfig.ArchiveFilesBasePath.ShouldBe(archiveFolderPath);
        var archive = archiveConfig.Archives.ShouldHaveSingleItem();
        archive.ArchiveState.ShouldBe(ArchiveState.Created);
        archive.ArchiveFolderPath.ShouldBe(archiveFolderPath);
        archive.ArchiveStorageFolderId.ShouldBe(archiveStorageFolderId);

        var upload = release
            .UploadConfigs.SelectMany(uploadConfig => uploadConfig.Uploads)
            .Single();
        upload.ArchiveId.ShouldBe(archive.Id);
        upload
            .UploadedFiles.OrderBy(file => file.HosterFileLink)
            .Select(file => (file.HosterFileLink, file.ArchiveFile!.FullFileName))
            .ShouldBe([
                ("https://first.test/a1", Path.Combine(archiveFolderPath, FirstArchiveFileName)),
                ("https://first.test/a2", Path.Combine(archiveFolderPath, SecondArchiveFileName)),
            ]);
    }

    [Test]
    public async Task ProcessAsync_ManagedReleaseWithArchivePartsInSubfolderOfArchiveStorageFolder_AssignsArchiveToThisStorageFolder()
    {
        // Arrange
        var seed = await AddReleaseTemplateAsync(ReleaseType.Managed);
        var releaseFolderPath = Directory
            .CreateDirectory(Path.Combine(workingDirectoryPath, ReleaseName))
            .FullName;
        var archiveStorageFolderPath = Directory
            .CreateDirectory(Path.Combine(tempRootPath, "storage"))
            .FullName;
        var archiveFolderPath = CreateArchivePartFiles(
            Path.Combine(archiveStorageFolderPath, "Archives")
        );
        await AddArchiveStorageFolderAsync(Path.Combine(tempRootPath, "other-storage"));
        var archiveStorageFolderId = await AddArchiveStorageFolderAsync(archiveStorageFolderPath);
        AddListedContainer(
            "https://crypter.test/container",
            ReleaseName,
            [
                OnlineArchiveLink("https://first.test/a1", FirstArchiveFileName),
                OnlineArchiveLink("https://first.test/a2", SecondArchiveFileName),
            ]
        );

        // Act
        await RunWholeImportAsync(seed);

        // Assert
        var release = await LoadReleaseAsync(ReleaseName);
        release.ReleaseType.ShouldBe(ReleaseType.Managed);
        release.ReleaseFolderPath.ShouldBe(releaseFolderPath);
        var archive = release.ArchiveConfigs.ShouldHaveSingleItem().Archives.ShouldHaveSingleItem();
        archive.ArchiveState.ShouldBe(ArchiveState.Created);
        archive.ArchiveFolderPath.ShouldBe(archiveFolderPath);
        archive.ArchiveStorageFolderId.ShouldBe(archiveStorageFolderId);
    }

    [Test]
    public async Task ProcessAsync_TwoReleasesWithSameArchiveFileNames_ChoosesDifferentArchiveFolders()
    {
        // Arrange
        var seed = await AddReleaseTemplateAsync(ReleaseType.Unmanaged);
        var firstArchiveFolderPath = CreateArchivePartFiles(
            Path.Combine(workingDirectoryPath, "A-first")
        );
        var secondArchiveFolderPath = CreateArchivePartFiles(
            Path.Combine(workingDirectoryPath, "B-second")
        );
        AddListedContainer(
            "https://crypter.test/first",
            ReleaseName,
            [
                OnlineArchiveLink("https://first.test/a1", FirstArchiveFileName),
                OnlineArchiveLink("https://first.test/a2", SecondArchiveFileName),
            ]
        );
        AddListedContainer(
            "https://crypter.test/second",
            OtherReleaseName,
            [
                OnlineArchiveLink("https://first.test/b1", FirstArchiveFileName),
                OnlineArchiveLink("https://first.test/b2", SecondArchiveFileName),
            ]
        );

        // Act
        await RunWholeImportAsync(seed);

        // Assert
        (await LoadReleaseAsync(ReleaseName))
            .ArchiveConfigs.ShouldHaveSingleItem()
            .Archives.ShouldHaveSingleItem()
            .ArchiveFolderPath.ShouldBe(firstArchiveFolderPath);
        (await LoadReleaseAsync(OtherReleaseName))
            .ArchiveConfigs.ShouldHaveSingleItem()
            .Archives.ShouldHaveSingleItem()
            .ArchiveFolderPath.ShouldBe(secondArchiveFolderPath);
    }

    [Test]
    public async Task ProcessAsync_ArchiveFolderUsedByOtherRelease_ChoosesNextFolderWithSameArchiveParts()
    {
        // Arrange
        var seed = await AddReleaseTemplateAsync(ReleaseType.Unmanaged);
        var usedArchiveFolderPath = CreateArchivePartFiles(
            Path.Combine(workingDirectoryPath, "A-used")
        );
        var unusedArchiveFolderPath = CreateArchivePartFiles(
            Path.Combine(workingDirectoryPath, "B-unused")
        );
        var dbContext = CreateDbContext();
        dbContext.Releases.Add(
            new Release
            {
                Name = OtherReleaseName,
                ReleaseType = ReleaseType.Managed,
                ReleaseFolderPath = usedArchiveFolderPath,
                ReleaseGroupId = seed.ReleaseGroupId,
                CreatedAt = StartTime,
                UploadConfigs = [],
                ArchiveConfigs = [],
            }
        );
        await dbContext.SaveChangesAsync();
        AddListedContainer(
            "https://crypter.test/container",
            ReleaseName,
            [
                OnlineArchiveLink("https://first.test/a1", FirstArchiveFileName),
                OnlineArchiveLink("https://first.test/a2", SecondArchiveFileName),
            ]
        );
        var linkCrypterImportId = await StartImportAsync(seed);
        await ProcessAsync();
        await ChooseDefaultHosterRegistrationsAsync(linkCrypterImportId, seed);

        // Act
        await ProcessAsync();

        // Assert
        var container = (await LoadImportAsync(linkCrypterImportId)).Containers.Single();
        container.Status.ShouldBe(LinkCrypterImportContainerStatus.Importable);
        LinkCrypterImportContainerPreview
            .FromJson(container.PreviewJson)
            .ArchiveFolderPath.ShouldBe(unusedArchiveFolderPath);
    }

    [Test]
    public async Task ProcessAsync_SomeContainersFailToImport_ImportsTheOthersAndNotifiesFailure()
    {
        // Arrange
        var seed = await AddReleaseTemplateAsync(ReleaseType.Unmanaged);
        AddListedContainer(
            "https://crypter.test/missing-account",
            ReleaseName,
            [OnlineArchiveLink("https://first.test/a1", FirstArchiveFileName)]
        );
        AddListedContainer(
            "https://crypter.test/release-created-meanwhile",
            OtherReleaseName,
            [OnlineArchiveLink("https://second.test/b1", FirstArchiveFileName)]
        );
        AddListedContainer(
            "https://crypter.test/importable",
            ThirdReleaseName,
            [OnlineArchiveLink("https://second.test/c1", FirstArchiveFileName)]
        );
        var linkCrypterImportId = await StartImportAsync(seed);
        await ProcessAsync();
        await ChooseDefaultHosterRegistrationsAsync(linkCrypterImportId, seed);
        await ProcessAsync();
        await RemoveChosenHosterRegistrationAsync(linkCrypterImportId, FirstHosterClassName);
        await AddReleaseAsync(seed, OtherReleaseName);
        await CreateLinkCrypterImportService(CreateDbContext())
            .StartImportAsync(linkCrypterImportId);

        // Act
        await ProcessAsync();

        // Assert
        var linkCrypterImport = await LoadImportAsync(linkCrypterImportId);
        linkCrypterImport.State.ShouldBe(LinkCrypterImportState.Completed);
        linkCrypterImport.ProcessedContainerCount.ShouldBe(3);
        var containers = linkCrypterImport.Containers.OrderBy(container => container.Id).ToList();
        containers
            .Select(container => container.Status)
            .ShouldBe([
                LinkCrypterImportContainerStatus.ImportFailed,
                LinkCrypterImportContainerStatus.ImportFailed,
                LinkCrypterImportContainerStatus.Imported,
            ]);
        containers[0].ErrorMessages.ShouldHaveSingleItem();
        containers[1]
            .ErrorMessages.ShouldBe([
                $"No mirror of the container belongs to a hoster of release {OtherReleaseName}.",
            ]);
        containers[2].ImportedReleaseId.ShouldBe((await LoadReleaseAsync(ThirdReleaseName)).Id);
        (
            await CreateDbContext().Releases.CountAsync(release => release.Name == ReleaseName)
        ).ShouldBe(0);
        (
            await CreateDbContext()
                .Notifications.OrderByDescending(notification => notification.Id)
                .FirstAsync()
        ).NotificationKind.ShouldBe(NotificationKind.LinkCrypterImportFailed);
    }

    [Test]
    public async Task RenameReleaseAsync_ToReleaseNameOfOtherContainer_ImportsBothContainersIntoOneRelease()
    {
        // Arrange
        var seed = await AddReleaseTemplateAsync(ReleaseType.Unmanaged);
        AddListedContainer(
            "https://crypter.test/first",
            ReleaseName,
            [
                OnlineArchiveLink("https://first.test/a1", FirstArchiveFileName),
                OnlineArchiveLink("https://first.test/a2", SecondArchiveFileName),
            ]
        );
        AddListedContainer(
            "https://crypter.test/second",
            "Bearcat Movie 2026 Second Hoster",
            [
                OnlineArchiveLink("https://second.test/b1", FirstArchiveFileName),
                OnlineArchiveLink("https://second.test/b2", SecondArchiveFileName),
            ]
        );
        var linkCrypterImportId = await StartImportAsync(seed);
        await ProcessAsync();
        await ChooseDefaultHosterRegistrationsAsync(linkCrypterImportId, seed);
        await ProcessAsync();

        // Act
        await CreateLinkCrypterImportService(CreateDbContext())
            .RenameReleaseAsync(
                linkCrypterImportId: linkCrypterImportId,
                currentReleaseName: "bearcat movie 2026 second hoster",
                newReleaseName: $" {ReleaseName} "
            );
        await CreateLinkCrypterImportService(CreateDbContext())
            .StartImportAsync(linkCrypterImportId);
        await ProcessAsync();

        // Assert
        var release = await LoadReleaseAsync(ReleaseName);
        var containers = (await LoadImportAsync(linkCrypterImportId))
            .Containers.OrderBy(container => container.Id)
            .ToList();
        containers
            .Select(container =>
                (
                    container.ReleaseName,
                    container.Status,
                    container.ImportedReleaseId,
                    LinkCrypterImportContainerPreview.FromJson(container.PreviewJson).ReleaseAction
                )
            )
            .ShouldBe([
                (
                    ReleaseName,
                    LinkCrypterImportContainerStatus.Imported,
                    release.Id,
                    LinkCrypterImportReleaseAction.CreateRelease
                ),
                (
                    ReleaseName,
                    LinkCrypterImportContainerStatus.Imported,
                    release.Id,
                    LinkCrypterImportReleaseAction.AttachToReleaseOfEarlierContainer
                ),
            ]);
        release.UploadConfigs.SelectMany(uploadConfig => uploadConfig.Uploads).Count().ShouldBe(2);
    }

    [Test]
    public async Task RenameReleaseAsync_ToNameOfExistingRelease_AttachesContainerToExistingRelease()
    {
        // Arrange
        var seed = await AddReleaseTemplateAsync(ReleaseType.Unmanaged);
        AddListedContainer(
            "https://crypter.test/first",
            ReleaseName,
            [
                OnlineArchiveLink("https://first.test/a1", FirstArchiveFileName),
                OnlineArchiveLink("https://first.test/a2", SecondArchiveFileName),
            ]
        );
        await RunWholeImportAsync(seed);
        var release = await LoadReleaseAsync(ReleaseName);
        listedContainers.Clear();
        AddListedContainer(
            "https://crypter.test/second",
            "Wrong title",
            [
                OnlineArchiveLink("https://second.test/b1", FirstArchiveFileName),
                OnlineArchiveLink("https://second.test/b2", SecondArchiveFileName),
            ]
        );
        var linkCrypterImportId = await StartImportAsync(seed);
        await ProcessAsync();
        await ChooseDefaultHosterRegistrationsAsync(linkCrypterImportId, seed);
        await ProcessAsync();

        // Act
        await CreateLinkCrypterImportService(CreateDbContext())
            .RenameReleaseAsync(
                linkCrypterImportId: linkCrypterImportId,
                currentReleaseName: "Wrong title",
                newReleaseName: ReleaseName
            );

        // Assert
        var container = (await LoadImportAsync(linkCrypterImportId)).Containers.Single();
        container.ReleaseName.ShouldBe(ReleaseName);
        container.Status.ShouldBe(LinkCrypterImportContainerStatus.Importable);
        container.IsSelected.ShouldBeTrue();
        var preview = LinkCrypterImportContainerPreview.FromJson(container.PreviewJson);
        preview.ReleaseAction.ShouldBe(LinkCrypterImportReleaseAction.AttachToExistingRelease);
        preview.ExistingReleaseId.ShouldBe(release.Id);
    }

    [Test]
    public async Task SetReleaseSelectionAsync_Select_SelectsOnlyImportableContainersOfRelease()
    {
        // Arrange
        var seed = await AddReleaseTemplateAsync(ReleaseType.Unmanaged);
        AddListedContainer(
            "https://crypter.test/first",
            ReleaseName,
            [OnlineArchiveLink("https://first.test/a1", FirstArchiveFileName)]
        );
        AddListedContainer(
            "https://crypter.test/second",
            ReleaseName.ToLowerInvariant(),
            [OnlineArchiveLink("https://second.test/b1", FirstArchiveFileName)]
        );
        AddListedContainer(
            "https://crypter.test/unknown",
            ReleaseName,
            ["https://unknown.test/c1"]
        );
        AddListedContainer(
            "https://crypter.test/other",
            OtherReleaseName,
            [OnlineArchiveLink("https://first.test/d1", FirstArchiveFileName)]
        );
        var linkCrypterImportId = await StartImportAsync(seed);
        await ProcessAsync();
        await ChooseDefaultHosterRegistrationsAsync(linkCrypterImportId, seed);
        await ProcessAsync();
        await CreateLinkCrypterImportService(CreateDbContext())
            .SetSelectionOfAllImportableContainersAsync(linkCrypterImportId, isSelected: false);

        // Act
        await CreateLinkCrypterImportService(CreateDbContext())
            .SetReleaseSelectionAsync(linkCrypterImportId, ReleaseName, isSelected: true);

        // Assert
        (await LoadImportAsync(linkCrypterImportId))
            .Containers.OrderBy(container => container.Id)
            .Select(container => (container.Status, container.IsSelected))
            .ShouldBe([
                (LinkCrypterImportContainerStatus.Importable, true),
                (LinkCrypterImportContainerStatus.Importable, true),
                (LinkCrypterImportContainerStatus.NotImportable, false),
                (LinkCrypterImportContainerStatus.Importable, false),
            ]);
    }

    private async Task<int> RunWholeImportAsync(
        TemplateSeed seed,
        int? linkCrypterRegistrationId = null,
        string? primaryLanguageCode = null
    )
    {
        var linkCrypterImportId = await StartImportAsync(
            seed: seed,
            linkCrypterRegistrationId: linkCrypterRegistrationId,
            primaryLanguageCode: primaryLanguageCode
        );
        await ProcessAsync();
        await ChooseDefaultHosterRegistrationsAsync(linkCrypterImportId, seed);
        await ProcessAsync();
        await CreateLinkCrypterImportService(CreateDbContext())
            .StartImportAsync(linkCrypterImportId);
        await ProcessAsync();

        return linkCrypterImportId;
    }

    private async Task<int> StartImportAsync(
        TemplateSeed seed,
        int? linkCrypterRegistrationId = null,
        string? primaryLanguageCode = null
    )
    {
        return await CreateLinkCrypterImportService(CreateDbContext())
            .StartAsync(
                linkCrypterRegistrationId: linkCrypterRegistrationId
                    ?? seed.LinkCrypterRegistrationId,
                releaseTemplateId: seed.ReleaseTemplateId,
                archiveFilesBasePathForReleasesWithoutLocalFiles: archiveFilesBasePath,
                primaryLanguageCode: primaryLanguageCode
            );
    }

    private async Task ChooseDefaultHosterRegistrationsAsync(
        int linkCrypterImportId,
        TemplateSeed seed
    )
    {
        await CreateLinkCrypterImportService(CreateDbContext())
            .SetHosterRegistrationsAsync(
                linkCrypterImportId,
                [
                    new LinkCrypterImportHosterRegistrationChoice(
                        FirstHosterClassName,
                        seed.FirstHosterRegistrationId
                    ),
                    new LinkCrypterImportHosterRegistrationChoice(
                        SecondHosterClassName,
                        seed.SecondHosterRegistrationId
                    ),
                ]
            );
    }

    private async Task ProcessAsync()
    {
        var dbContext = CreateDbContext();
        var timeProvider = new ControllableTimeProvider(StartTime);
        var repository = new LinkCrypterImportRepository(dbContext);
        var releaseWriteRepository = new ReleaseWriteRepository(dbContext);
        var notificationService = new NotificationService(
            new NotificationRepository(dbContext),
            timeProvider,
            CreateNotificationConfigurationProvider()
        );

        var service = new LinkCrypterImportProcessingService(
            repository,
            new LinkCrypterImportContainerReadingService(
                repository,
                releaseWriteRepository,
                linkCrypterFactoryMock.Object,
                hosterFactoryMock.Object,
                NoOpSecretProtector.Instance,
                notificationService,
                timeProvider,
                NullLogger<LinkCrypterImportContainerReadingService>.Instance
            ),
            CreatePreviewService(dbContext, timeProvider),
            new LinkCrypterImportContainerImportService(
                repository,
                releaseWriteRepository,
                CreateReleaseFromFolderCreationService(dbContext, timeProvider),
                new UnmanagedReleaseArchiveInitializationService(new RealArchiverFactory()),
                new FileSystemService(),
                notificationService,
                timeProvider,
                NullLogger<LinkCrypterImportContainerImportService>.Instance
            ),
            notificationService,
            timeProvider,
            NullLogger<LinkCrypterImportProcessingService>.Instance
        );

        await service.ProcessAsync(CancellationToken.None);
    }

    private LinkCrypterImportService CreateLinkCrypterImportService(BearcatDbContext dbContext)
    {
        var timeProvider = new ControllableTimeProvider(StartTime);

        return new LinkCrypterImportService(
            new LinkCrypterImportRepository(dbContext),
            CreatePreviewService(dbContext, timeProvider),
            linkCrypterFactoryMock.Object,
            new FileSystemService(),
            timeProvider
        );
    }

    private LinkCrypterImportPreviewService CreatePreviewService(
        BearcatDbContext dbContext,
        ControllableTimeProvider timeProvider
    )
    {
        return new LinkCrypterImportPreviewService(
            new LinkCrypterImportRepository(dbContext),
            new ReleaseWriteRepository(dbContext),
            new ReleaseFolderUsageRepository(dbContext),
            hosterFactoryMock.Object,
            NoOpSecretProtector.Instance,
            new FileSystemService(),
            Options.Create(
                new WorkingDirectoriesConfig { WorkingDirectories = [workingDirectoryPath] }
            ),
            new NotificationService(
                new NotificationRepository(dbContext),
                timeProvider,
                CreateNotificationConfigurationProvider()
            ),
            timeProvider,
            NullLogger<LinkCrypterImportPreviewService>.Instance
        );
    }

    private static ReleaseFromFolderCreationService CreateReleaseFromFolderCreationService(
        BearcatDbContext dbContext,
        ControllableTimeProvider timeProvider
    )
    {
        var nfoDatabaseFactory = new Mock<INfoDatabaseFactory>();
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
            new ReleaseInfoResolutionService(
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
                        new Mock<IMediaMetadataDatabaseFactory>().Object,
                        NullLogger<MediaMetadataResolver>.Instance
                    ),
                    nfoDatabaseFactory.Object,
                    NullLogger<ReleaseMetadataResolver>.Instance
                ),
                classificationService,
                NullLogger<ReleaseInfoResolutionService>.Instance,
                timeProvider
            ),
            new MediaMetadataService(
                new MediaMetadataRepository(dbContext),
                mediaMetadataExtractor.Object,
                new FileSystemService(),
                classificationService,
                timeProvider,
                NullLogger<MediaMetadataService>.Instance
            ),
            new UnmanagedReleaseArchiveInitializationService(new RealArchiverFactory()),
            Mock.Of<IReleaseCollectionAssigner>()
        );
    }

    private async Task<int> AddArchiveStorageFolderAsync(string archiveStorageFolderPath)
    {
        var archiveStorageFolder = new ArchiveStorageFolder
        {
            Name = Path.GetFileName(archiveStorageFolderPath),
            Path = archiveStorageFolderPath,
            IsActive = true,
        };
        var dbContext = CreateDbContext();
        dbContext.ArchiveStorageFolders.Add(archiveStorageFolder);
        await dbContext.SaveChangesAsync();

        return archiveStorageFolder.Id;
    }

    private static string CreateArchivePartFiles(string folderPath)
    {
        Directory.CreateDirectory(folderPath);
        File.WriteAllText(Path.Combine(folderPath, FirstArchiveFileName), "first");
        File.WriteAllText(Path.Combine(folderPath, SecondArchiveFileName), "second");

        return folderPath;
    }

    private Mock<IHosterWithFileInfo> CreateHosterMock()
    {
        var hosterMock = new Mock<IHosterWithFileInfo>();
        hosterMock
            .Setup(hoster => hoster.ConvertToOwnFileLinkFormat(It.IsAny<string>()))
            .Returns((string fileLink) => fileLink);
        hosterMock
            .Setup(hoster => hoster.DeserializeHosterConfig(It.IsAny<string>()))
            .Returns(Mock.Of<IHosterConfig>());
        hosterMock
            .Setup(hoster =>
                hoster.GetFileInfosAsync(
                    It.IsAny<IHosterConfig>(),
                    It.IsAny<IReadOnlyList<string>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                (IHosterConfig _, IReadOnlyList<string> fileLinks, CancellationToken _) =>
                    new FileInfoResult(
                        IsSuccess: true,
                        ErrorMessages: [],
                        FileInfoPerFileLink: fileLinks
                            .Where(fileInfosPerLink.ContainsKey)
                            .ToDictionary(
                                fileLink => fileLink,
                                fileLink => fileInfosPerLink[fileLink]
                            )
                    )
            );

        return hosterMock;
    }

    private void VerifyFileInfosWereNeverRequested(string hosterClassName)
    {
        hosterMocksByClassName[hosterClassName]
            .Verify(
                hoster =>
                    hoster.GetFileInfosAsync(
                        It.IsAny<IHosterConfig>(),
                        It.IsAny<IReadOnlyList<string>>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never
            );
    }

    private string OnlineArchiveLink(string link, string fileName)
    {
        fileInfosPerLink[link] = new HostedFileInfo(
            IsOnline: true,
            FileName: fileName,
            SizeBytes: 100
        );
        return link;
    }

    private string OfflineLink(string link)
    {
        fileInfosPerLink[link] = new HostedFileInfo(
            IsOnline: false,
            FileName: null,
            SizeBytes: null
        );
        return link;
    }

    private void AddListedContainer(
        string containerUrl,
        string containerName,
        params IReadOnlyList<string>[] linksPerMirror
    )
    {
        listedContainers.Add(
            new ListedContainer(
                ContainerId: containerUrl,
                ContainerUrl: containerUrl,
                ContainerName: containerName,
                ExternalReference: $"reference-{containerUrl}"
            )
        );
        linksPerContainerUrl[containerUrl] = new ReadContainerLinksResult(
            IsSuccess: true,
            LinksPerMirrorNumber: linksPerMirror
                .Index()
                .ToDictionary(entry => entry.Index + 1, entry => entry.Item),
            HasBackupLinks: false,
            ErrorMessages: []
        );
    }

    private async Task<LinkCrypterImport> LoadImportAsync(int linkCrypterImportId)
    {
        return await CreateDbContext()
            .LinkCrypterImports.AsSplitQuery()
            .Include(import => import.Containers)
            .Include(import => import.HosterRegistrations)
            .SingleAsync(import => import.Id == linkCrypterImportId);
    }

    private static List<(
        LinkCrypterImportMirrorDecision?,
        LinkCrypterImportUploadByBearcatReason?
    )> GetMirrorDecisions(LinkCrypterImportContainer container)
    {
        return LinkCrypterImportContainerPreview
            .FromJson(container.PreviewJson)
            .Mirrors.Select(mirror => (mirror.Decision, mirror.UploadByBearcatReason))
            .ToList();
    }

    private async Task<Release> LoadReleaseAsync(string releaseName)
    {
        return await CreateDbContext()
            .Releases.AsSplitQuery()
            .Include(release => release.LinkCrypterContainers)
                .ThenInclude(container => container.SourceUploads)
            .Include(release => release.ArchiveConfigs)
                .ThenInclude(archiveConfig => archiveConfig.Archives)
                    .ThenInclude(archive => archive.ArchiveFiles)
            .Include(release => release.UploadConfigs)
                .ThenInclude(uploadConfig => uploadConfig.Uploads)
                    .ThenInclude(upload => upload.UploadedFiles)
                        .ThenInclude(file => file.ArchiveFile)
            .Include(release => release.UploadConfigs)
                .ThenInclude(uploadConfig => uploadConfig.LinkCrypters)
                    .ThenInclude(linkCrypter => linkCrypter.LinkCrypterContainers)
            .SingleAsync(release => release.Name == releaseName);
    }

    private async Task AddExistingLinkCrypterContainerAsync(TemplateSeed seed, string containerUrl)
    {
        var dbContext = CreateDbContext();
        dbContext.LinkCrypterContainers.Add(
            new LinkCrypterContainer
            {
                Scope = LinkCrypterContainerScope.Release,
                LinkCrypterRegistrationId = seed.LinkCrypterRegistrationId,
                ContainerUrl = containerUrl,
                State = LinkCrypterContainerState.Created,
                Errors = [],
                CreatedAt = StartTime,
            }
        );
        await dbContext.SaveChangesAsync();
    }

    private async Task<int> AddLinkCrypterRegistrationAsync()
    {
        var linkCrypterRegistration = new LinkCrypterRegistration
        {
            Name = "Second crypter",
            LinkCrypterClassName = LinkCrypterClassName,
            SerializedConfig = "{}",
            IsActive = true,
        };
        var dbContext = CreateDbContext();
        dbContext.LinkCrypterRegistrations.Add(linkCrypterRegistration);
        await dbContext.SaveChangesAsync();

        return linkCrypterRegistration.Id;
    }

    private async Task AddReleaseAsync(TemplateSeed seed, string releaseName)
    {
        var dbContext = CreateDbContext();
        dbContext.Releases.Add(
            new Release
            {
                Name = releaseName,
                ReleaseType = ReleaseType.Unmanaged,
                ReleaseGroupId = seed.ReleaseGroupId,
                CreatedAt = StartTime,
                UploadConfigs = [],
                ArchiveConfigs = [],
            }
        );
        await dbContext.SaveChangesAsync();
    }

    private async Task RemoveChosenHosterRegistrationAsync(
        int linkCrypterImportId,
        string hosterClassName
    )
    {
        var dbContext = CreateDbContext();
        var choice = await dbContext.LinkCrypterImportHosterRegistrations.SingleAsync(choice =>
            choice.LinkCrypterImportId == linkCrypterImportId
            && choice.HosterClassName == hosterClassName
        );
        choice.HosterRegistrationId = null;
        await dbContext.SaveChangesAsync();
    }

    private async Task<TemplateSeed> AddReleaseTemplateAsync(ReleaseType releaseType)
    {
        var releaseGroup = new ReleaseGroup
        {
            Name = "Movies",
            EnableAutomaticReuploads = true,
            NumberOfHoursUntilReupload = 24,
        };
        var firstHosterRegistration = CreateHosterRegistration(
            "First account",
            FirstHosterClassName
        );
        var secondHosterRegistration = CreateHosterRegistration(
            "Second account",
            SecondHosterClassName
        );
        var thirdHosterRegistration = CreateHosterRegistration(
            "Third account",
            ThirdHosterClassName
        );
        var linkCrypterRegistration = new LinkCrypterRegistration
        {
            Name = "Crypter",
            LinkCrypterClassName = LinkCrypterClassName,
            SerializedConfig = "{}",
            IsActive = true,
        };
        var archiveConfigTemplate =
            releaseType is ReleaseType.Managed
                ? new ArchiveConfigTemplate
                {
                    Name = "RAR",
                    ArchiveFilesBasePath = Path.Combine(tempRootPath, "archives"),
                    ArchiverName = "RarArchiver",
                    ArchiveFileSizeMb = 1024,
                }
                : UnmanagedArchiveConfigTemplates.Create();
        var releaseTemplate = new ReleaseTemplate
        {
            Name = "Movie template",
            ReleaseType = releaseType,
            ReleaseContentType = ReleaseContentType.Movie,
            ReleaseGroup = releaseGroup,
            ArchiveConfigTemplates = [archiveConfigTemplate],
        };
        releaseTemplate.UploadConfigTemplates =
        [
            new UploadConfigTemplate
            {
                ReleaseTemplate = releaseTemplate,
                UploadSource = UploadSource.ArchiveFiles,
                ArchiveConfigTemplate = archiveConfigTemplate,
                HosterRegistration = firstHosterRegistration,
                LinkCrypterTemplates =
                [
                    new UploadConfigLinkCrypterTemplate
                    {
                        LinkCrypterRegistration = linkCrypterRegistration,
                        Password = "template-secret",
                    },
                ],
            },
            new UploadConfigTemplate
            {
                ReleaseTemplate = releaseTemplate,
                UploadSource = UploadSource.ArchiveFiles,
                ArchiveConfigTemplate = archiveConfigTemplate,
                HosterRegistration = secondHosterRegistration,
            },
        ];

        var dbContext = CreateDbContext();
        dbContext.ReleaseTemplates.Add(releaseTemplate);
        dbContext.HosterRegistrations.Add(thirdHosterRegistration);
        await dbContext.SaveChangesAsync();

        return new TemplateSeed(
            ReleaseTemplateId: releaseTemplate.Id,
            ReleaseGroupId: releaseGroup.Id,
            LinkCrypterRegistrationId: linkCrypterRegistration.Id,
            FirstHosterRegistrationId: firstHosterRegistration.Id,
            SecondHosterRegistrationId: secondHosterRegistration.Id
        );
    }

    private async Task AddThirdHosterUploadConfigTemplateAsync(TemplateSeed seed)
    {
        var dbContext = CreateDbContext();
        var releaseTemplate = await dbContext
            .ReleaseTemplates.Include(template => template.ArchiveConfigTemplates)
            .SingleAsync(template => template.Id == seed.ReleaseTemplateId);
        var thirdHosterRegistration = await dbContext.HosterRegistrations.SingleAsync(
            registration => registration.HosterClassName == ThirdHosterClassName
        );
        dbContext.UploadConfigTemplates.Add(
            new UploadConfigTemplate
            {
                ReleaseTemplate = releaseTemplate,
                UploadSource = UploadSource.ArchiveFiles,
                ArchiveConfigTemplate = releaseTemplate.ArchiveConfigTemplates.Single(),
                HosterRegistration = thirdHosterRegistration,
            }
        );
        await dbContext.SaveChangesAsync();
    }

    private static HosterRegistration CreateHosterRegistration(string name, string hosterClassName)
    {
        return new HosterRegistration
        {
            Name = name,
            SerializedConfig = "{}",
            HosterClassName = hosterClassName,
            IsActive = true,
        };
    }

    private sealed record TemplateSeed(
        int ReleaseTemplateId,
        int ReleaseGroupId,
        int LinkCrypterRegistrationId,
        int FirstHosterRegistrationId,
        int SecondHosterRegistrationId
    );
}
