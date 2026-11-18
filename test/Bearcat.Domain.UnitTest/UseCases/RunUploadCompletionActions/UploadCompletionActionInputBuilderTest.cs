using Bearcat.Abstractions.ImageHoster;
using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Abstractions.UploadCompletionAction.Dto;
using Bearcat.Domain.Shared.ForumPostRendering;
using Bearcat.Domain.UseCases.ManageReleaseCollections.ForumPostRendering;
using Bearcat.Domain.UseCases.ManageReleaseCollections.Repositories;
using Bearcat.Domain.UseCases.ManageReleases.ForumPostRendering;
using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Domain.UseCases.RunUploadCompletionActions;
using Bearcat.Domain.UseCases.RunUploadCompletionActions.Models;
using Bearcat.Domain.ValueObjects;
using Moq;
using Shouldly;

namespace Bearcat.Domain.UnitTest.UseCases.RunUploadCompletionActions;

public class UploadCompletionActionInputBuilderTest
{
    private const int ReleaseCollectionId = 4;

    private const int FirstReleaseId = 7;

    private const int SecondReleaseId = 8;

    private const int UploadSlotId = 5;

    [Test]
    public async Task BuildAsync_ReleaseCollectionWithSlotUploadsOfSeveralReleases_CombinesSlotUploadsPerHosterIntoOneMirror()
    {
        // Arrange
        var slotLinkCrypter = new ReleaseForumPostLinkCrypterReadModel(
            LinkCrypterClassName: "FileCrypt",
            Name: "FileCrypt account",
            Password: null,
            ContainerUrl: "https://filecrypt.example/container",
            StatusImageId: null,
            CreatedAt: DateTime.UnixEpoch
        );

        var uploadsByReleaseId = new Dictionary<int, IReadOnlyList<ReleaseForumPostUploadReadModel>>
        {
            {
                FirstReleaseId,
                [
                    CreateUpload(
                        collectionUploadSlotId: UploadSlotId,
                        links:
                        [
                            "https://rapidgator.example/e01-1",
                            "https://rapidgator.example/e01-2",
                        ],
                        fileSizesBytes: [100, 300],
                        archivePassword: "secret"
                    ),
                    CreateUpload(
                        collectionUploadSlotId: null,
                        links: ["https://rapidgator.example/e01-standalone"],
                        fileSizesBytes: [500],
                        archivePassword: "secret"
                    ),
                ]
            },
            {
                SecondReleaseId,
                [
                    CreateUpload(
                        collectionUploadSlotId: UploadSlotId,
                        links: ["https://rapidgator.example/e02-1"],
                        fileSizesBytes: [null],
                        archivePassword: "other secret"
                    ),
                ]
            },
        };

        var inputBuilder = CreateInputBuilder(uploadsByReleaseId, slotLinkCrypter);

        // Act
        var input = await inputBuilder.BuildAsync(
            new UploadCompletionActionDueReleaseCollection(
                ReleaseCollectionId: ReleaseCollectionId,
                IsReupload: false,
                NewestUploadedAt: DateTime.UnixEpoch
            ),
            CancellationToken.None
        );

        // Assert
        input.Release.Name.ShouldBe("Bearcat.Collection.S01");
        input.Release.ContentType.ShouldBe("TvShowEpisode");

        input.CollectionReleaseNames.ShouldBe([
            "Bearcat.Collection.S01E01",
            "Bearcat.Collection.S01E02",
        ]);

        input.AllHostersLinkCrypterContainers.ShouldBeEmpty();

        var mirror = input.Mirrors.ShouldHaveSingleItem();
        mirror.HosterClassName.ShouldBe("RapidGator");

        mirror.Links.ShouldBe([
            "https://rapidgator.example/e01-1",
            "https://rapidgator.example/e01-2",
            "https://rapidgator.example/e02-1",
        ]);

        mirror.TotalSizeBytes.ShouldBeNull();
        mirror.LargestPartSizeBytes.ShouldBe(300);
        mirror.PartCount.ShouldBe(3);
        mirror.ArchivePassword.ShouldBeNull();

        mirror.LinkCrypterContainers.ShouldBe([
            new UploadCompletionActionLinkCrypterContainerDto(
                LinkCrypterClassName: "FileCrypt",
                LinkCrypterName: "filecrypt.cc",
                Url: "https://filecrypt.example/container"
            ),
        ]);
    }

    private static ReleaseForumPostUploadReadModel CreateUpload(
        int? collectionUploadSlotId,
        IReadOnlyList<string> links,
        IReadOnlyList<long?> fileSizesBytes,
        string archivePassword
    )
    {
        return new ReleaseForumPostUploadReadModel(
            UploadConfigName: "Rapidgator upload",
            CollectionUploadSlotId: collectionUploadSlotId,
            HosterClassName: "RapidGator",
            HosterName: "rapidgator.net",
            HosterRegistrationName: "Rapidgator account",
            IsVideoUpload: false,
            ArchiveFormat: "RAR",
            ArchivePassword: archivePassword,
            UploadedAt: DateTime.UnixEpoch,
            Links: links,
            EmbedLinks: [],
            FileSizesBytes: fileSizesBytes,
            LinkCrypters: []
        );
    }

    private static UploadCompletionActionInputBuilder CreateInputBuilder(
        Dictionary<int, IReadOnlyList<ReleaseForumPostUploadReadModel>> uploadsByReleaseId,
        ReleaseForumPostLinkCrypterReadModel slotLinkCrypter
    )
    {
        var collectionRepository = new Mock<IReleaseCollectionForumPostRepository>();

        collectionRepository
            .Setup(repo => repo.GetAsync(ReleaseCollectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new CollectionForumPostReadModel(
                    Name: "Bearcat.Collection.S01",
                    Key: "bearcat-collection-s01",
                    ReleaseContentType: ReleaseContentType.TvShowEpisode,
                    ReleaseGroupName: "Bearcat",
                    PrimaryLanguageCode: null,
                    Series: null,
                    ReleaseIds: [FirstReleaseId, SecondReleaseId],
                    UploadSlots:
                    [
                        new CollectionForumPostUploadSlotReadModel(
                            Id: UploadSlotId,
                            Name: "1080p",
                            Key: "1080p",
                            LinkCrypters: [slotLinkCrypter]
                        ),
                    ]
                )
            );

        var uploadRepository = new Mock<IReleaseForumPostUploadRepository>();

        uploadRepository
            .Setup(repo =>
                repo.GetForumPostUploadsByReleaseIdAsync(
                    It.IsAny<IReadOnlyList<int>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(uploadsByReleaseId);

        uploadRepository
            .Setup(repo =>
                repo.GetForumPostAllHostersLinkCryptersByReleaseIdAsync(
                    It.IsAny<IReadOnlyList<int>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new Dictionary<int, IReadOnlyList<ReleaseForumPostLinkCrypterReadModel>>()
            );

        var imageLinkRepository = new Mock<IForumPostImageLinkRepository>();

        imageLinkRepository
            .Setup(repo =>
                repo.GetReleaseImageLinksByReleaseIdAsync(
                    It.IsAny<IReadOnlyList<int>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new Dictionary<int, IReadOnlyList<ForumPostImageLinkReadModel>>());

        imageLinkRepository
            .Setup(repo =>
                repo.GetCollectionImageLinksAsync(
                    ReleaseCollectionId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([]);

        var linkCrypter = new Mock<ILinkCrypter>();
        linkCrypter.Setup(crypter => crypter.Name).Returns("filecrypt.cc");

        var linkCrypterFactory = new Mock<ILinkCrypterFactory>();

        linkCrypterFactory.Setup(factory => factory.Get("FileCrypt")).Returns(linkCrypter.Object);

        return new UploadCompletionActionInputBuilder(
            releaseForumPostRenderModelBuilder: new ReleaseForumPostRenderModelBuilder(
                releaseReadRepository: CreateReleaseReadRepository(),
                uploadRepository: uploadRepository.Object,
                imageLinkRepository: imageLinkRepository.Object
            ),
            releaseCollectionForumPostRepository: collectionRepository.Object,
            imageLinkRepository: imageLinkRepository.Object,
            imageHosterFactory: Mock.Of<IImageHosterFactory>(),
            linkCrypterFactory: linkCrypterFactory.Object
        );
    }

    private static IReleaseReadRepository CreateReleaseReadRepository()
    {
        var releaseReadRepository = new Mock<IReleaseReadRepository>
        {
            DefaultValueProvider = DefaultValueProvider.Mock,
        };

        releaseReadRepository
            .Setup(repo =>
                repo.GetReleasesByIdAsync(
                    It.IsAny<IReadOnlyList<int>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new Dictionary<int, ReleaseReadModel>
                {
                    { FirstReleaseId, CreateRelease(FirstReleaseId, "Bearcat.Collection.S01E01") },
                    {
                        SecondReleaseId,
                        CreateRelease(SecondReleaseId, "Bearcat.Collection.S01E02")
                    },
                }
            );

        return releaseReadRepository.Object;
    }

    private static ReleaseReadModel CreateRelease(int releaseId, string name)
    {
        return new ReleaseReadModel(
            ReleaseId: releaseId,
            Name: name,
            ReleaseType: ReleaseType.Managed,
            ReleaseContentType: ReleaseContentType.TvShowEpisode,
            PrimaryLanguageCode: null,
            ReleaseGroupId: 1,
            ReleaseGroupName: "Bearcat",
            ReleaseFolderPath: null,
            UploadConfigsCount: 0,
            OnlineUploadConfigsCount: 0,
            ExcludeFromAutoCleanup: false,
            UploadsPostedAt: null,
            HasUploadConfigsWaitingForInitialUpload: false,
            CreateScreenshots: false,
            ScreenshotCount: 0,
            CreateScreenshotGrid: false,
            ScreenshotGridColumns: 0,
            ScreenshotGridRows: 0
        );
    }
}
